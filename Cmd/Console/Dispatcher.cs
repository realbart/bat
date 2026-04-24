using System.Reflection;
using Bat.Commands;
using Bat.Execution;
using Bat.Nodes;
using Bat.Parsing;
using Bat.Tokens;
using Context;

namespace Bat.Console;

internal interface IDispatcher
{
    Task<bool> ExecuteCommandAsync(IContext context, ParsedCommand command);
}

internal class Dispatcher : IDispatcher
{
    public async Task<bool> ExecuteCommandAsync(IContext context, ParsedCommand command)
    {
        var bc = ReplBatchContext.Value;
        bc.Context = context;
        // In REPL mode, we don't clear the stack between commands to allow setlocal/endlocal to span across lines
        var exitCode = await ExecuteNodeAsync(bc, command.Root);
        if (exitCode == ExitCommand.ExitSentinel)
            return false;
        context.ErrorCode = exitCode;
        return true;
    }

    internal static async Task<int> ExecuteNodeAsync(BatchContext bc, ICommandNode node)
    {
        switch (node)
        {
            case EmptyCommandNode:
                return 0;

            case QuietNode quiet:
                return await ExecuteNodeAsync(bc, quiet.Subcommand);

            case BlockNode block:
            {
                var last = 0;
                foreach (var sub in block.Subcommands)
                    last = await ExecuteNodeAsync(bc, sub);
                return last;
            }

            case MultiNode multi:
                await ExecuteNodeAsync(bc, multi.Left);
                return await ExecuteNodeAsync(bc, multi.Right);

            case AndNode and:
            {
                var left = await ExecuteNodeAsync(bc, and.Left);
                if (left != 0) return left;
                return await ExecuteNodeAsync(bc, and.Right);
            }

            case OrNode or:
            {
                var left = await ExecuteNodeAsync(bc, or.Left);
                if (left == 0) return left;
                return await ExecuteNodeAsync(bc, or.Right);
            }

            case PipeNode pipe:
                return await ExecutePipeAsync(bc, pipe);

            case CommandNode cmd:
                return await ExecuteCommandNodeAsync(bc, cmd);

            case IfCommandNode @if:
                return await ExecuteIfNodeAsync(bc, @if);

            case IncompleteNode:
                await bc.Console!.Error.WriteLineAsync("Unexpected end of command.");
                return 1;

            default:
                return 0;
        }
    }

    private static (char Drive, string[] Segments) ParseNativePath(string path, BatchContext bc)
    {
        if (path.StartsWith('\\'))
            return (bc.Context.CurrentDrive, path[1..].Split('\\', StringSplitOptions.RemoveEmptyEntries));

        if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':')
        {
            var drive = char.ToUpperInvariant(path[0]);
            var remainder = path.Length >= 3 && path[2] == '\\' ? path[3..] : path[2..];
            return (drive, remainder.Split('\\', StringSplitOptions.RemoveEmptyEntries));
        }

        return (bc.Context.CurrentDrive, [.. bc.Context.CurrentPath, .. path.Split('\\', StringSplitOptions.RemoveEmptyEntries)]);
    }

    private static async Task<int> ExecuteIfNodeAsync(BatchContext bc, IfCommandNode @if)
    {
        var conditionMet = false;
        var left = BatchExecutor.Expand(string.Join("", @if.LeftArg.Select(t => t.Raw)), bc);
        var right = BatchExecutor.Expand(string.Join("", @if.RightArg.Select(t => t.Raw)), bc);

        var ignoreCase = (@if.Flags & IfFlags.IgnoreCase) != 0;
        var negate = (@if.Flags & IfFlags.Negate) != 0;

        switch (@if.Operator)
        {
            case IfOperator.ErrorLevel:
                if (int.TryParse(right, out var level))
                    conditionMet = bc.Context.ErrorCode >= level;
                break;
            case IfOperator.Exist:
                var (drive, segments) = ParseNativePath(right, bc);
                conditionMet = await bc.Context.FileSystem.FileExistsAsync(drive, segments) || await bc.Context.FileSystem.DirectoryExistsAsync(drive, segments);
                break;
            case IfOperator.Defined:
                conditionMet = bc.Context.EnvironmentVariables.ContainsKey(right);
                break;
            case IfOperator.StringEqual:
                conditionMet = string.Equals(left, right, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
                break;
            case IfOperator.Equ:
                if (long.TryParse(left, out var l1) && long.TryParse(right, out var r1)) conditionMet = l1 == r1;
                else conditionMet = string.Equals(left, right, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
                break;
            case IfOperator.Neq:
                if (long.TryParse(left, out var l2) && long.TryParse(right, out var r2)) conditionMet = l2 != r2;
                else conditionMet = !string.Equals(left, right, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
                break;
            case IfOperator.Lss:
                if (long.TryParse(left, out var l3) && long.TryParse(right, out var r3)) conditionMet = l3 < r3;
                else conditionMet = string.Compare(left, right, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) < 0;
                break;
            case IfOperator.Leq:
                if (long.TryParse(left, out var l4) && long.TryParse(right, out var r4)) conditionMet = l4 <= r4;
                else conditionMet = string.Compare(left, right, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) <= 0;
                break;
            case IfOperator.Gtr:
                if (long.TryParse(left, out var l5) && long.TryParse(right, out var r5)) conditionMet = l5 > r5;
                else conditionMet = string.Compare(left, right, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) > 0;
                break;
            case IfOperator.Geq:
                if (long.TryParse(left, out var l6) && long.TryParse(right, out var r6)) conditionMet = l6 >= r6;
                else conditionMet = string.Compare(left, right, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) >= 0;
                break;
        }

        if (negate) conditionMet = !conditionMet;

        if (conditionMet)
            return await ExecuteNodeAsync(bc, @if.ThenBranch);
        else if (@if.ElseBranch != null)
            return await ExecuteNodeAsync(bc, @if.ElseBranch);

        return 0;
    }

    private static async Task<int> ExecuteCommandNodeAsync(BatchContext bc, CommandNode cmd)
    {
        if (cmd.Head is IBuiltInCommandToken builtIn) return await ExecuteBuiltInAsync(bc, builtIn, cmd);

        var rawName = cmd.Head.Raw;

        // Drive switch: X: (single letter + colon, nothing else)
        if (rawName.Length == 2 && char.IsAsciiLetter(rawName[0]) && rawName[1] == ':')
        {
            var targetDrive = char.ToUpperInvariant(rawName[0]);
            if (await bc.Context.FileSystem.DirectoryExistsAsync(targetDrive, []))
            {
                bc.Context.SetCurrentDrive(targetDrive);
                return 0;
            }
            await bc.Console.Error.WriteLineAsync("The system cannot find the drive specified.");
            return 1;
        }

        var splitAt = rawName.IndexOfAny(['/', '\\', '.']);
        if (splitAt > 0 && IsPathSuffix(rawName[splitAt..]))
        {
            var result = await TryExecuteSplitCommandAsync(bc, rawName, splitAt, cmd);
            if (result.HasValue) return result.Value;
        }

        // "bat" is an alias for "cmd" — nested shell via satellite, not a native process
        var resolvedName = rawName.Equals("bat", StringComparison.OrdinalIgnoreCase) ? "cmd" : rawName;

        var executablePath = await ExecutableResolver.ResolveAsync(resolvedName, bc.Context);
        if (executablePath == null)
        {
            await bc.Console.Error.WriteLineAsync($"'{rawName}' is not recognized as an internal or external command,");
            await bc.Console.Error.WriteLineAsync("operable program or batch file.");
            return 1;
        }

        var (folderFound, _) = bc.Context.TryGetCurrentFolder();
        if (!folderFound)
        {
            await bc.Console.Error.WriteLineAsync("The current directory is invalid.");
            return 1;
        }

        return await WithRedirections(bc, cmd.Redirections, async () =>
        {
            var executor = GetExecutor(bc.Console, executablePath, bc.Context.FileSystem);
            var arguments = string.Join(" ", cmd.Tail.OfType<TextToken>().Select(t => t.Value));
            return await executor.ExecuteAsync(executablePath, arguments, bc, cmd.Redirections);
        });
    }

    private static async Task<int> ExecutePipeAsync(BatchContext bc, PipeNode pipe)
    {
        var captured = new StringWriter();
        var prevConsole = bc.Context.Console;
        
        // Left side
        var ctxL = bc.Context.StartNew(prevConsole.WithOutput(captured));
        var bctxL = new BatchContext { Context = ctxL };
        await ExecuteNodeAsync(bctxL, pipe.Left);
        bc.Context.ApplySnapshot(ctxL);
        bc.Context.ErrorCode = ctxL.ErrorCode;

        // Right side
        var input = captured.ToString();
        var ctxR = bc.Context.StartNew(prevConsole.WithInput(new StringReader(input)));
        var bctxR = new BatchContext { Context = ctxR };
        var result = await ExecuteNodeAsync(bctxR, pipe.Right);
        bc.Context.ApplySnapshot(ctxR);
        return result;
    }

    private static async Task<int> WithRedirections(BatchContext bc, IReadOnlyList<Redirection> redirections, Func<Task<int>> action)
    {
        // todo: fix this is a thread-safe manner
        // (using a new BatchContext copy for each redirection)
        if (redirections.Count == 0) return await action();
        var prevContext = bc.Context;
        var newContext = bc.Context.StartNew();
        using var rh = RedirectionHandler.Apply(redirections, newContext, bc.Console);
        newContext = newContext.StartNew(rh.Console);
        bc.Context = newContext;
        try
        {
            var result = await action();
            prevContext.ApplySnapshot(newContext);
            return result;
        }
        finally
        {
            bc.Context = prevContext;
        }
    }

    private static async Task<int> ExecuteBuiltInAsync(BatchContext bc, IBuiltInCommandToken builtIn, CommandNode cmd)
    {
        var rawArgs = cmd.Tail.SkipWhile(static t => t is WhitespaceToken).ToList();
        var args = ArgumentSet.Parse(rawArgs, builtIn.Spec);
        if (args.ErrorMessage != null)
        {
            await bc.Console.Error.WriteLineAsync(args.ErrorMessage);
            return 1;
        }
        return await WithRedirections(bc, cmd.Redirections, () =>
            builtIn.CreateCommand().ExecuteAsync(args, bc, cmd.Redirections));
    }

    private static async Task<int?> TryExecuteSplitCommandAsync(BatchContext bc, string rawName, int splitAt, CommandNode cmd)
    {
        var commandType = BuiltInCommandRegistry.GetCommandType(rawName[..splitAt]);
        if (commandType == null) return null;

        // CMD treats . as a word separator only for ECHO (echo. → blank line, echo.text → outputs "text")
        // For other commands (cd.., cd.\sub), . is part of the path argument.
        var isEchoDotSeparator = rawName[splitAt] == '.' && commandType == typeof(EchoCommand);
        var suffix = isEchoDotSeparator ? rawName[(splitAt + 1)..] : rawName[splitAt..];

        var spec = ArgumentSpec.From(commandType.GetCustomAttributes<BuiltInCommandAttribute>());
        var allArgs = new List<IToken>();
        if (suffix.Length > 0) allArgs.Add(Token.Text(suffix));
        allArgs.AddRange(cmd.Tail.SkipWhile(static t => t is WhitespaceToken));

        // echo. (dot separator, empty suffix, no tail text) → output blank line, not echo status
        if (isEchoDotSeparator && allArgs.All(static t => t is WhitespaceToken))
        {
            return await WithRedirections(bc, cmd.Redirections, async () =>
            {
                await bc.Console.Out.WriteLineAsync();
                return 0;
            });
        }

        var args = ArgumentSet.Parse(allArgs, spec);
        if (args.ErrorMessage != null)
        {
            await bc.Console.Error.WriteLineAsync(args.ErrorMessage);
            return 1;
        }
        return await WithRedirections(bc, cmd.Redirections, () =>
            ((ICommand)Activator.CreateInstance(commandType)!).ExecuteAsync(args, bc, cmd.Redirections));
    }

    private static IExecutor GetExecutor(IConsole console, string executablePath, global::Context.IFileSystem fileSystem)
    {
        var ext = Path.GetExtension(executablePath).ToLowerInvariant();

        if (ext is ".bat" or ".cmd")
            return new BatchExecutor();

        var hostPath = Context.PathTranslator.TranslateBatPathToHost(executablePath, fileSystem);
        var peType = ExecutableTypeDetector.GetExecutableType(hostPath);

        return peType switch
        {
            ExecutableType.DotNetAssembly => new DotNetLibraryExecutor(new PtyNativeExecutor(waitForExit: true, isGuiApp: false)),
            ExecutableType.PrefixedDotNetAssembly => new DotNetLibraryExecutor(new PtyNativeExecutor(waitForExit: true, isGuiApp: false), isPrefixed: true),
            ExecutableType.WindowsGui => new PtyNativeExecutor(waitForExit: false, isGuiApp: true),
            ExecutableType.WindowsConsole => new PtyNativeExecutor(waitForExit: true, isGuiApp: false),
            ExecutableType.Document => new PtyNativeExecutor(waitForExit: false, isGuiApp: true),
            _ => new PtyNativeExecutor(waitForExit: true, isGuiApp: false)
        };
    }

    /// <summary>
    /// Returns true when the suffix after a command name should be treated as a path argument
    /// rather than a file extension. Handles: /switch, \path, .. and .\ relative paths, and lone dot.
    /// Prevents splitting on .exe, .bat etc (file extension on an external program name).
    /// </summary>
    private static bool IsPathSuffix(string suffix) =>
        suffix[0] is '/' or '\\' ||
        (suffix[0] == '.' && (suffix.Length == 1 || suffix[1] is '.' or '\\'));
}
