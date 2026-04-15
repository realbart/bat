using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("goto")]
internal class GotoCommand : ICommand
{
    internal const int GotoSentinel = int.MinValue + 2;

    private const string HelpText =
        """
        Directs cmd.exe to a labeled line in a batch program.

        GOTO label

          label   Specifies a text string used in the batch program as a label.

        You type a label on a line by itself, beginning with a colon.

        If Command Extensions are enabled GOTO changes as follows:

        GOTO command now accepts a target label of :EOF which transfers control
        to the end of the current batch script file.  This is an easy way to
        exit a batch script file without defining a label.  Type CALL /?  for a
        description of extensions to the CALL command that make this feature
        useful.

        Executing GOTO :EOF after a CALL with a target label will return control
        to the statement immediately following the CALL.
        """;

    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        if (arguments.IsHelpRequest)
        {
            await batchContext.Console.Out.WriteLineAsync(HelpText);
            return 0;
        }

        // In REPL mode (no label positions), GOTO is a no-op
        if (batchContext.LabelPositions == null) return 0;

        var rawLabel = arguments.FullArgument.Trim();
        var label = rawLabel.TrimStart(':');
        if (label.Length == 0)
        {
            await batchContext.Console.Error.WriteLineAsync("The syntax of the command is incorrect.");
            return 1;
        }

        // :eof only works when the colon is explicitly given (goto :eof), not goto eof
        if (rawLabel.StartsWith(':') && label.Equals("eof", StringComparison.OrdinalIgnoreCase))
        {
            batchContext.FilePosition = batchContext.FileContent?.Length ?? 0;
            return GotoSentinel;
        }

        if (!batchContext.LabelPositions.TryGetValue(label, out var position))
        {
            await batchContext.Console.Error.WriteLineAsync($"The system cannot find the batch label specified - {label}");
            batchContext.Context.ErrorCode = 1;
            return ExitCommand.ExitBatchSentinel;
        }

        batchContext.FilePosition = position;
        return GotoSentinel;
    }
}
