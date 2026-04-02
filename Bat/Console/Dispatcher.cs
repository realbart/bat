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
    Task<bool> ExecuteCommandAsync(IContext context, IConsole console, ParsedCommand command);
}

internal class Dispatcher : IDispatcher
{
    public async Task<bool> ExecuteCommandAsync(IContext context, IConsole console, ParsedCommand command)
    {
        var bc = ReplBatchContext.Value;
        bc.Context = context;
        bc.Console = console;
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

            case PipeNode:
                // Pipe execution not yet implemented (Step 6)
                return 0;

            case CommandNode cmd:
                return await ExecuteCommandNodeAsync(bc, cmd);

            case IncompleteNode:
                await bc.Console!.Error.WriteLineAsync("Unexpected end of command.");
                return 1;

            default:
                return 0;
        }
    }

    private static async Task<int> ExecuteCommandNodeAsync(BatchContext bc, CommandNode cmd)
    {
        if (cmd.Head is IBuiltInCommandToken builtIn) return await ExecuteBuiltInAsync(bc, builtIn, cmd);

        var rawName = cmd.Head.Raw;

        // Drive switch: X: (single letter + colon, nothing else)
        if (rawName.Length == 2 && char.IsAsciiLetter(rawName[0]) && rawName[1] == ':')
        {
            var targetDrive = char.ToUpperInvariant(rawName[0]);
            if (bc.Context.FileSystem.DirectoryExists(targetDrive, []))
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

        var executablePath = ExecutableResolver.Resolve(rawName, bc.Context);
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

        var executor = GetExecutor(bc.Console, executablePath, bc.Context.FileSystem);
        var arguments = string.Join(" ", cmd.Tail.OfType<TextToken>().Select(t => t.Value));
        return await executor.ExecuteAsync(executablePath, arguments, bc, cmd.Redirections);
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
        return await builtIn.CreateCommand().ExecuteAsync(args, bc, cmd.Redirections);
    }

    private static async Task<int?> TryExecuteSplitCommandAsync(BatchContext bc, string rawName, int splitAt, CommandNode cmd)
    {
        var commandType = BuiltInCommandRegistry.GetCommandType(rawName[..splitAt]);
        if (commandType == null) return null;

        var spec = ArgumentSpec.From(commandType.GetCustomAttributes<BuiltInCommandAttribute>());
        var allArgs = new List<IToken> { Token.Text(rawName[splitAt..]) };
        allArgs.AddRange(cmd.Tail.SkipWhile(static t => t is WhitespaceToken));
        var args = ArgumentSet.Parse(allArgs, spec);
        if (args.ErrorMessage != null)
        {
            await bc.Console.Error.WriteLineAsync(args.ErrorMessage);
            return 1;
        }
        return await ((ICommand)Activator.CreateInstance(commandType)!).ExecuteAsync(args, bc, cmd.Redirections);
    }

    private static IExecutor GetExecutor(IConsole console, string executablePath, global::Context.IFileSystem fileSystem)
    {
        var ext = Path.GetExtension(executablePath).ToLowerInvariant();

        if (ext is ".bat" or ".cmd")
            return new BatchExecutor(console);

        var hostPath = Context.PathTranslator.TranslateBatPathToHost(executablePath, fileSystem);
        var peType = ExecutableTypeDetector.GetExecutableType(hostPath);

        return peType switch
        {
            ExecutableType.DotNetAssembly => new DotNetLibraryExecutor(new NativeExecutor(waitForExit: true, isGuiApp: false)),
            ExecutableType.WindowsGui => new NativeExecutor(waitForExit: false, isGuiApp: true),
            ExecutableType.WindowsConsole => new NativeExecutor(waitForExit: true, isGuiApp: false),
            ExecutableType.Document => new NativeExecutor(waitForExit: false, isGuiApp: true),
            _ => new NativeExecutor(waitForExit: true, isGuiApp: false)
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
