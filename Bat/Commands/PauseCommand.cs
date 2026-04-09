using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("pause")]
internal class PauseCommand : ICommand
{
    private const string HelpText =
        """
        Suspends processing of a batch program and displays a message.

        PAUSE
        """;

    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        if (arguments.IsHelpRequest) { await batchContext.Console.Out.WriteLineAsync(HelpText); return 0; }
        await batchContext.Console.Out.WriteAsync("Press any key to continue . . . ");
        batchContext.Console.In.Read();
        await batchContext.Console.Out.WriteLineAsync();
        return 0;
    }
}
