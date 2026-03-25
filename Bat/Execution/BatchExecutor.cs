using Bat.Commands;
using Bat.Console;
using Bat.Nodes;
using Bat.Parsing;
using Context;

namespace Bat.Execution;

/// <summary>
/// Executes batch files (.bat, .cmd) line-by-line without GOTO/CALL support.
/// Advanced features (labels, subroutines) are implemented in Step 8.
/// </summary>
internal class BatchExecutor(IConsole console) : IExecutor
{
    public async Task<int> ExecuteAsync(string executablePath, string arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        var context = batchContext.Context;
        var (drive, path) = ParseNativePath(executablePath);
        var content = context.FileSystem.ReadAllText(drive, path);
        var lines = content.Split(["\r\n", "\n"], StringSplitOptions.None);

        var childContext = new BatchContext
        {
            Context = context,
            Console = console,
            Parameters = CreateParameters(Path.GetFileName(executablePath), ParseArguments(arguments)),
            BatchFilePath = executablePath
        };

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var expanded = Expander.ExpandBatchParameters(line, childContext);
            expanded = Expander.ExpandEnvironmentVariables(expanded, context);

            var parser = new Parser();
            parser.Append(expanded);
            var result = parser.ParseCommand();

            if (result.HasError)
            {
                await console.Error.WriteLineAsync("Syntax error");
                return 1;
            }

            var exitCode = await Dispatcher.ExecuteNodeAsync(childContext, result.Root);
            if (exitCode == ExitCommand.ExitSentinel) return context.ErrorCode;
            if (exitCode == ExitCommand.ExitBatchSentinel) return 0;
        }

        return context.ErrorCode;
    }

    private static string[] ParseArguments(string arguments) =>
        string.IsNullOrWhiteSpace(arguments) ? [] : arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static string?[] CreateParameters(string fileName, string[] args)
    {
        var parameters = new string?[10];
        parameters[0] = fileName;
        for (var i = 0; i < args.Length && i < 9; i++) parameters[i + 1] = args[i];
        return parameters;
    }

    private static (char Drive, string[] Path) ParseNativePath(string nativePath)
    {
        if (nativePath.Length < 2 || !char.IsLetter(nativePath[0]) || nativePath[1] != ':') return ('Z', []);

        var drive = char.ToUpperInvariant(nativePath[0]);
        var remainder = nativePath.Length > 3 ? nativePath[3..] : "";
        var segments = remainder.Length > 0 ? remainder.Split('\\', StringSplitOptions.RemoveEmptyEntries) : [];
        return (drive, segments);
    }
}
