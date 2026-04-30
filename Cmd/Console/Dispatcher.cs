using System.Reflection;
using Bat.Commands;
using Bat.Execution;
using Bat.Nodes;
using Bat.Parsing;
using Bat.Tokens;
using BatD.Files;
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

            case ForCommandNode @for:
                return await ExecuteForNodeAsync(bc, @for);

            case IfCommandNode @if:
                return await ExecuteIfNodeAsync(bc, @if);

            case IncompleteNode:
                await bc.Console!.Error.WriteLineAsync("Unexpected end of command.");
                return 1;

            default:
                return 0;
        }
    }

    private static BatPath ParseNativePath(string path, BatchContext bc)
    {
        if (path.StartsWith('\\'))
            return new BatPath(bc.Context.CurrentDrive, path[1..].Split('\\', StringSplitOptions.RemoveEmptyEntries));

        if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':')
        {
            var drive = char.ToUpperInvariant(path[0]);
            var remainder = path.Length >= 3 && path[2] == '\\' ? path[3..] : path[2..];
            return new BatPath(drive, remainder.Split('\\', StringSplitOptions.RemoveEmptyEntries));
        }

        return new BatPath(bc.Context.CurrentDrive, [.. bc.Context.CurrentPath, .. path.Split('\\', StringSplitOptions.RemoveEmptyEntries)]);
    }

    private static string ExtractValue(IReadOnlyList<IToken> tokens)
    {
        var result = new System.Text.StringBuilder();
        foreach (var token in tokens)
        {
            if (token is QuotedTextToken qt)
                result.Append(qt.Value);
            else if (token is TextToken tt)
                result.Append(tt.Value);
            else
                result.Append(token.Raw);
        }
        return result.ToString();
    }

    private static async Task<int> ExecuteForNodeAsync(BatchContext bc, ForCommandNode @for)
    {
        var varName = @for.Variable.ToString();  // stored as plain letter, expanded via %var%
        var listText = string.Concat(@for.List.TakeWhile(t => !(t is TextToken tt && tt.Value.Equals("do", StringComparison.OrdinalIgnoreCase))).Select(t => t.Raw));

        async Task<int> RunBody(string value)
        {
            var savedVar = bc.Context.EnvironmentVariables.TryGetValue(varName, out var v) ? v : null;
            bc.Context.EnvironmentVariables[varName] = value;
            try
            {
                // Re-expand and re-parse the body so that %%variable gets substituted
                var bodyText = string.Concat(@for.Body.GetTokens().Select(t => t.Raw));
                var expanded = BatchExecutor.Expand(bodyText, bc);
                var parser = new Parser();
                parser.Append(expanded);
                var parsed = parser.ParseCommand();
                if (parsed.HasError || parsed.Root is EmptyCommandNode) return 0;
                return await ExecuteNodeAsync(bc, parsed.Root);
            }
            finally
            {
                if (savedVar == null) bc.Context.EnvironmentVariables.Remove(varName);
                else bc.Context.EnvironmentVariables[varName] = savedVar;
            }
        }

        // Expand the list items (env vars and batch params)
        var rawItems = @for.List
            .TakeWhile(t => !(t is TextToken tt && tt.Value.Equals("do", StringComparison.OrdinalIgnoreCase)))
            .Select(t => t.Raw)
            .ToList();

        // ── /L numeric loop: for /l %%n in (start,step,end) do ────────────────
        if (@for.Switches.HasFlag(ForSwitches.Loop))
        {
            var combined = BatchExecutor.Expand(string.Concat(rawItems), bc).Trim();
            var parts = combined.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length >= 3
                && int.TryParse(parts[0], out var start)
                && int.TryParse(parts[1], out var step)
                && int.TryParse(parts[2], out var end))
            {
                var last = 0;
                if (step > 0) for (var n = start; n <= end; n += step) last = await RunBody(n.ToString());
                else if (step < 0) for (var n = start; n >= end; n += step) last = await RunBody(n.ToString());
            }
            return bc.Context.ErrorCode;
        }

        // ── /F file/string/command processing ─────────────────────────────────
        if (@for.Switches.HasFlag(ForSwitches.F))
        {
            var paramsText = BatchExecutor.Expand(string.Concat(@for.Params.Select(t => t.Raw)), bc).Trim();
            var source = BatchExecutor.Expand(string.Concat(rawItems), bc).Trim();

            // Parse /F options string (e.g. "tokens=1,2 delims=, skip=1")
            string? optionsStr = null;
            if (paramsText.StartsWith('"') || paramsText.StartsWith('\''))
                optionsStr = paramsText[1..Math.Max(1, paramsText.LastIndexOfAny(['"', '\'']))];

            var delims = " \t";
            var tokens = new[] { 1 };
            var skip = 0;
            var useBackq = false;
            var eol = ';';

            if (optionsStr != null)
            {
                foreach (var opt in optionsStr.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = opt.Split('=', 2);
                    switch (kv[0].ToLowerInvariant())
                    {
                        case "delims": delims = kv.Length > 1 ? kv[1] : ""; break;
                        case "tokens":
                            if (kv.Length > 1) tokens = kv[1].Split(',')
                                .SelectMany(t => t.Contains('-')
                                    ? Enumerable.Range(int.Parse(t.Split('-')[0]), int.Parse(t.Split('-')[1]) - int.Parse(t.Split('-')[0]) + 1)
                                    : t == "*" ? [-1] : [int.Parse(t)])
                                .ToArray();
                            break;
                        case "skip": if (kv.Length > 1) int.TryParse(kv[1], out skip); break;
                        case "eol": if (kv.Length > 1 && kv[1].Length > 0) eol = kv[1][0]; break;
                        case "usebackq": useBackq = true; break;
                    }
                }
            }

            IEnumerable<string> lines;
            if (source.StartsWith('"') && source.EndsWith('"'))
            {
                // "string" → process as a single line
                lines = [source[1..^1]];
            }
            else if (useBackq && source.StartsWith('`') && source.EndsWith('`'))
            {
                // `command` → capture output
                var cmd = source[1..^1];
                var output = await RunCaptureAsync(cmd, bc);
                lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            }
            else if (useBackq && source.StartsWith('"') && source.EndsWith('"'))
            {
                // "file" with usebackq → read file
                var filePath = BatchExecutor.Expand(source[1..^1], bc);
                lines = File.Exists(filePath) ? await File.ReadAllLinesAsync(filePath) : [];
            }
            else
            {
                // bare word → command output
                var cmd = source;
                var output = await RunCaptureAsync(cmd, bc);
                lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            }

            var lineNum = 0;
            foreach (var line in lines)
            {
                lineNum++;
                if (lineNum <= skip) continue;
                if (line.Length > 0 && line[0] == eol) continue;

                var parts = delims.Length == 0
                    ? [line]
                    : line.Split(delims.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                // Assign tokens to successive loop variables
                var varChar = @for.Variable;
                foreach (var tokenIdx in tokens)
                {
                    string value;
                    if (tokenIdx == -1) // "*" = rest of line
                    {
                        var firstDelim = line.IndexOfAny(delims.ToCharArray());
                        value = firstDelim >= 0 ? line[(firstDelim + 1)..].TrimStart(delims.ToCharArray()) : line;
                    }
                    else
                    {
                        value = tokenIdx <= parts.Length ? parts[tokenIdx - 1] : "";
                    }

                    bc.Context.EnvironmentVariables[$"{varChar}"] = value;
                        varChar = (char)(varChar + 1);
                }

                await ExecuteNodeAsync(bc, @for.Body);
            }

            bc.Context.EnvironmentVariables.Remove(varName);
            return bc.Context.ErrorCode;
        }

        // ── /D or /R or plain wildcard set ────────────────────────────────────
        var isDir = @for.Switches.HasFlag(ForSwitches.Dirs);
        var isRecursive = @for.Switches.HasFlag(ForSwitches.Recursive);

        // Get optional /R root path from Params
        var rootPath = isRecursive
            ? BatchExecutor.Expand(string.Concat(@for.Params
                .SkipWhile(t => t.Raw.Equals("/R", StringComparison.OrdinalIgnoreCase))
                .Skip(1)
                .TakeWhile(t => t is not WhitespaceToken)
                .Select(t => t.Raw)), bc).Trim()
            : null;

        // Tokenize the list (space/comma separated, respecting quotes)
        var expandedList = string.Concat(rawItems.Select(i => BatchExecutor.Expand(i, bc)));
        var setItems = TokenizeSet(expandedList);

        foreach (var item in setItems)
        {
            // Check if it's a wildcard pattern
            if (item.Contains('*') || item.Contains('?'))
            {
                var (drive, dir) = ParsePatternPath(item, bc);
                var pattern = Path.GetFileName(item.Replace('/', '\\'));

                if (isRecursive)
                {
                    await foreach (var entry in bc.Context.FileSystem.EnumerateEntriesAsync(new BatPath(drive, dir), "*"))
                    {
                        if (entry.IsDirectory)
                        {
                            string[] subPath = [..dir, entry.Name];
                            await foreach (var subEntry in bc.Context.FileSystem.EnumerateEntriesAsync(new BatPath(drive, subPath), pattern))
                            {
                                if (isDir == subEntry.IsDirectory || (!isDir && !subEntry.IsDirectory))
                                {
                                    var fullBatPath = $"{drive}:\\" + string.Join("\\", [..subPath, subEntry.Name]);
                                    await RunBody(ApplyTildeExpansion(fullBatPath));
                                }
                            }
                        }
                    }
                }
                else
                {
                    await foreach (var entry in bc.Context.FileSystem.EnumerateEntriesAsync(new BatPath(drive, dir), pattern))
                    {
                        if (isDir ? entry.IsDirectory : !entry.IsDirectory)
                        {
                            var fullBatPath = $"{drive}:\\" + string.Join("\\", [..dir, entry.Name]);
                            await RunBody(ApplyTildeExpansion(fullBatPath));
                        }
                    }
                }
            }
            else if (!isDir && !isRecursive)
            {
                // Plain value — no filesystem lookup, just substitute
                await RunBody(item);
            }
        }

        return bc.Context.ErrorCode;
    }

    /// <summary>Tokenizes a FOR set string, respecting double-quoted items.</summary>
    private static List<string> TokenizeSet(string set)
    {
        var result = new List<string>();
        var i = 0;
        while (i < set.Length)
        {
            if (char.IsWhiteSpace(set[i]) || set[i] == ',') { i++; continue; }
            if (set[i] == '"')
            {
                var end = set.IndexOf('"', i + 1);
                if (end < 0) end = set.Length - 1;
                result.Add(set[(i + 1)..end]);
                i = end + 1;
            }
            else
            {
                var start = i;
                while (i < set.Length && set[i] != ' ' && set[i] != ',' && set[i] != '\t') i++;
                result.Add(set[start..i]);
            }
        }
        return result;
    }

    /// <summary>Returns the BAT drive+dir for a pattern like C:\dir\*.txt or relative *.bat.</summary>
    private static (char Drive, string[] Dir) ParsePatternPath(string pattern, BatchContext bc)
    {
        var full = pattern.Replace('/', '\\');
        char drive;
        string rest;

        if (full.Length >= 2 && char.IsLetter(full[0]) && full[1] == ':')
        {
            drive = char.ToUpperInvariant(full[0]);
            rest = full.Length > 3 ? full[3..] : "";
        }
        else if (full.StartsWith('\\'))
        {
            drive = bc.Context.CurrentDrive;
            rest = full[1..];
        }
        else
        {
            drive = bc.Context.CurrentDrive;
            rest = string.Join("\\", bc.Context.CurrentPath) + (bc.Context.CurrentPath.Length > 0 ? "\\" : "") + full;
        }

        var parts = rest.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        var dir = parts.Length > 1 ? parts[..^1] : [];
        return (drive, dir);
    }

    /// <summary>Strips tilde-expansion directives if the value is a quoted path (for %%~nxf).</summary>
    private static string ApplyTildeExpansion(string value) => value;

    /// <summary>Runs a command string and captures its stdout output.</summary>
    private static async Task<string> RunCaptureAsync(string command, BatchContext bc)
    {
        var sw = new StringWriter();
        var captureConsole = bc.Context.Console.WithOutput(sw);
        var captureCtx = bc.Context.StartNew(captureConsole);
        var captureBc = new BatchContext { Context = captureCtx };

        var parser = new Parser();
        parser.Append(command);
        var result = parser.ParseCommand();
        if (!result.HasError && result.Root is not EmptyCommandNode)
            await ExecuteNodeAsync(captureBc, result.Root);

        return sw.ToString();
    }

    private static async Task<int> ExecuteIfNodeAsync(BatchContext bc, IfCommandNode @if)
    {
        var conditionMet = false;
        var left = ExtractValue(@if.LeftArg);
        var right = ExtractValue(@if.RightArg);
        left = BatchExecutor.Expand(left, bc);
        right = BatchExecutor.Expand(right, bc);

        var ignoreCase = (@if.Flags & IfFlags.IgnoreCase) != 0;
        var negate = (@if.Flags & IfFlags.Negate) != 0;

        switch (@if.Operator)
        {
            case IfOperator.ErrorLevel:
                if (int.TryParse(right, out var level))
                    conditionMet = bc.Context.ErrorCode >= level;
                break;
            case IfOperator.Exist:
                var existPath = ParseNativePath(right, bc);
                conditionMet = await bc.Context.FileSystem.FileExistsAsync(existPath) || await bc.Context.FileSystem.DirectoryExistsAsync(existPath);
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
            if (await bc.Context.FileSystem.DirectoryExistsAsync(new BatPath(targetDrive, [])))
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

        var (folderFound, _) = await bc.Context.TryGetCurrentFolderAsync();
        if (!folderFound)
        {
            await bc.Console.Error.WriteLineAsync("The current directory is invalid.");
            return 1;
        }

        return await WithRedirections(bc, cmd.Redirections, async () =>
        {
            var executor = await GetExecutor(bc.Console, executablePath, bc.Context.FileSystem);
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

    private static async Task<IExecutor> GetExecutor(IConsole console, string executablePath, global::Context.IFileSystem fileSystem)
    {
        var ext = Path.GetExtension(executablePath).ToLowerInvariant();

        if (ext is ".bat" or ".cmd")
            return new BatchExecutor();

        var hostPath = await PathTranslator.TranslateBatPathToHost(executablePath, fileSystem);
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
