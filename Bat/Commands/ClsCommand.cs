using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("cls")]
internal class ClsCommand : ICommand
{
    private const string HelpText =
        """
        Clears the screen.

        CLS
        """;

    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        if (arguments.IsHelpRequest) { await batchContext.Console.Out.WriteLineAsync(HelpText); return 0; }
        await batchContext.Console.Out.WriteAsync("\x1b[2J\x1b[H");
        return 0;
    }
}
