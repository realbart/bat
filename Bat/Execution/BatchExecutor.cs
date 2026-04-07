using Bat.Commands;
using Bat.Console;
using Bat.Nodes;
using Bat.Parsing;
using Context;

namespace Bat.Execution;

/// <summary>
/// Executes batch files (.bat, .cmd) using position-based reading with
/// GOTO/CALL support, label scanning, and SHIFT.
/// </summary>
internal class BatchExecutor(IConsole console) : IExecutor
{
    internal const int MaxNesting = 16;

    public async Task<int> ExecuteAsync(string executablePath, string arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        var context = batchContext.Context;
        var (drive, path) = ParseNativePath(executablePath);
        var content = context.FileSystem.ReadAllText(drive, path);

        var childContext = new BatchContext
        {
            Context = context,
            Console = console,
            Parameters = CreateParameters(executablePath, ParseArguments(arguments)),
            BatchFilePath = executablePath,
            FileContent = content,
            FilePosition = 0,
            LabelPositions = ScanLabels(content),
            Prev = batchContext.IsBatchFile ? batchContext : null,
        };

        if (NestingDepth(childContext) > MaxNesting)
        {
            await console.Error.WriteLineAsync("Maximum batch nesting depth exceeded.");
            return 1;
        }

        return await ExecuteBatchLoopAsync(childContext);
    }

    internal static async Task<int> ExecuteBatchLoopAsync(BatchContext bc)
    {
        var context = bc.Context;
        var content = bc.FileContent!;

        while (bc.FilePosition < content.Length)
        {
            var line = ReadNextLine(content, ref bc);
            bc.LineNumber++;

            if (string.IsNullOrWhiteSpace(line)) continue;

            // Label lines are skipped during execution
            if (line.TrimStart().StartsWith(':')) continue;

            var expanded = Expander.ExpandBatchParameters(line, bc);
            expanded = Expander.ExpandEnvironmentVariables(expanded, context);

            var trimmedExpanded = expanded.TrimStart();
            var isQuiet = trimmedExpanded.StartsWith('@');
            if (context.EchoEnabled && !isQuiet)
                await bc.Console.Out.WriteAsync($"\r\n{context.CurrentPathDisplayName}>{trimmedExpanded}\r\n");

            var parser = new Parser();
            parser.Append(expanded);
            var result = parser.ParseCommand();

            if (result.HasError)
            {
                await bc.Console.Error.WriteLineAsync($"Syntax error in: {trimmedExpanded}");
                continue;
            }

            var exitCode = await Dispatcher.ExecuteNodeAsync(bc, result.Root);
            if (exitCode == ExitCommand.ExitSentinel)
            {
                EndLocalCommand.UnwindSetLocalStack(bc);
                return context.ErrorCode;
            }
            if (exitCode == ExitCommand.ExitBatchSentinel)
            {
                EndLocalCommand.UnwindSetLocalStack(bc);
                return context.ErrorCode;
            }
            if (exitCode == GotoCommand.GotoSentinel) continue;
        }

        EndLocalCommand.UnwindSetLocalStack(bc);
        return context.ErrorCode;
    }

    internal static Dictionary<string, int> ScanLabels(string content)
    {
        var labels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var pos = 0;

        while (pos < content.Length)
        {
            var lineStart = pos;
            var lineEnd = content.IndexOfAny(['\r', '\n'], pos);
            if (lineEnd < 0) lineEnd = content.Length;

            var line = content[pos..lineEnd].TrimStart();
            if (line.StartsWith(':') && line.Length > 1)
            {
                var labelEnd = line.IndexOfAny([' ', '\t', ';'], 1);
                var name = labelEnd < 0 ? line[1..] : line[1..labelEnd];
                if (name.Length > 0)
                    labels.TryAdd(name, lineStart);
            }

            pos = lineEnd;
            if (pos < content.Length && content[pos] == '\r') pos++;
            if (pos < content.Length && content[pos] == '\n') pos++;
        }

        return labels;
    }

    private static string ReadNextLine(string content, ref BatchContext bc)
    {
        if (bc.FilePosition >= content.Length) return "";

        var lineEnd = content.IndexOfAny(['\r', '\n'], bc.FilePosition);
        if (lineEnd < 0) lineEnd = content.Length;

        var line = content[bc.FilePosition..lineEnd];

        bc.FilePosition = lineEnd;
        if (bc.FilePosition < content.Length && content[bc.FilePosition] == '\r') bc.FilePosition++;
        if (bc.FilePosition < content.Length && content[bc.FilePosition] == '\n') bc.FilePosition++;

        return line;
    }

    private static int NestingDepth(BatchContext bc)
    {
        var depth = 0;
        var current = bc;
        while (current != null)
        {
            depth++;
            current = current.Prev;
        }
        return depth;
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

    internal static (char Drive, string[] Path) ParseNativePath(string nativePath)
    {
        if (nativePath.Length < 2 || !char.IsLetter(nativePath[0]) || nativePath[1] != ':') return ('Z', []);

        var drive = char.ToUpperInvariant(nativePath[0]);
        var remainder = nativePath.Length > 3 ? nativePath[3..] : "";
        var segments = remainder.Length > 0 ? remainder.Split('\\', StringSplitOptions.RemoveEmptyEntries) : [];
        return (drive, segments);
    }
}
