using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("goto")]
internal class GotoCommand : ICommand
{
    internal const int GotoSentinel = int.MinValue + 2;

    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        // In REPL mode (no label positions), GOTO is a no-op
        if (batchContext.LabelPositions == null) return 0;

        var label = arguments.FullArgument.TrimStart(':').Trim();
        if (label.Length == 0)
        {
            await batchContext.Console.Error.WriteLineAsync("The syntax of the command is incorrect.");
            return 1;
        }

        // :eof always works — jumps to end of file
        if (label.Equals("eof", StringComparison.OrdinalIgnoreCase))
        {
            batchContext.FilePosition = batchContext.FileContent?.Length ?? 0;
            return GotoSentinel;
        }

        if (!batchContext.LabelPositions.TryGetValue(label, out var position))
        {
            await batchContext.Console.Error.WriteLineAsync($"The system cannot find the batch label specified - {label}");
            return 1;
        }

        batchContext.FilePosition = position;
        return GotoSentinel;
    }
}
