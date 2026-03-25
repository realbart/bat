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
        if (cmd.Head is IBuiltInCommandToken builtIn)
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

        // Try splitting command/switch combos like dir/w or dir\path
        var rawName = cmd.Head.Raw;
        var splitAt = rawName.IndexOfAny(['/', '\\']);
        if (splitAt > 0)
        {
            var commandType = BuiltInCommandRegistry.GetCommandType(rawName[..splitAt]);
            if (commandType != null)
            {
                var spec = ArgumentSpec.From(commandType.GetCustomAttributes<BuiltInCommandAttribute>());
                var suffixToken = Token.Text(rawName[splitAt..]);
                var allArgs = new List<IToken> { suffixToken };
                allArgs.AddRange(cmd.Tail.SkipWhile(static t => t is WhitespaceToken));
                var args = ArgumentSet.Parse(allArgs, spec);
                if (args.ErrorMessage != null)
                {
                    await bc.Console.Error.WriteLineAsync(args.ErrorMessage);
                    return 1;
                }
                return await ((ICommand)Activator.CreateInstance(commandType)!).ExecuteAsync(args, bc, cmd.Redirections);
            }
        }

        var executablePath = ExecutableResolver.Resolve(rawName, bc.Context);
        if (executablePath == null)
        {
            await bc.Console.Error.WriteLineAsync($"'{rawName}' is not recognized as an internal or external command,");
            await bc.Console.Error.WriteLineAsync("operable program or batch file.");
            return 1;
        }

        var executor = GetExecutor(bc.Console, executablePath, bc.Context.FileSystem);
        var argumentTokens = cmd.Tail.OfType<TextToken>().Select(t => t.Value).ToArray();
        var arguments = string.Join(" ", argumentTokens);
        return await executor.ExecuteAsync(executablePath, arguments, bc, cmd.Redirections);
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
            _ => new NativeExecutor(waitForExit: true, isGuiApp: false)
        };
    }
}
