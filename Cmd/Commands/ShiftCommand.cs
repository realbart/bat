using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("shift")]
internal class ShiftCommand : ICommand
{
    private const string HelpText =
        """
        Changes the position of replaceable parameters in a batch file.

        SHIFT [/n]

        If Command Extensions are enabled the SHIFT command supports
        the /n switch which tells the command to start shifting at the
        nth argument, where n may be between zero and eight.  For example:

            SHIFT /2

        would shift %3 to %2, %4 to %3, etc. and leave %0 and %1 unaffected.
        """;

    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        if (arguments.IsHelpRequest) { await batchContext.Console.Out.WriteLineAsync(HelpText); return 0; }

        var n = 0;
        var full = arguments.FullArgument.Trim();
        if (full.Length == 2 && (full[0] == '/' || full[0] == '-') && char.IsAsciiDigit(full[1]))
            n = full[1] - '0';

        if (n == 0)
        {
            batchContext.ShiftOffset++;
        }
        else
        {
            var actualStart = batchContext.ShiftOffset + n;
            for (var i = actualStart; i < 9; i++)
                batchContext.Parameters[i] = batchContext.Parameters[i + 1];
            batchContext.Parameters[9] = null;
        }

        return 0;
    }
}
