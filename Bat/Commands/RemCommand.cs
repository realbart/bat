using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("rem")]
internal class RemCommand : ICommand
{
    private const string HelpText =
        """
        Records comments (remarks) in a batch file or CONFIG.SYS.

        REM [comment]
        """;

    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        if (arguments.IsHelpRequest) { await batchContext.Console.Out.WriteLineAsync(HelpText); return 0; }
        return 0;
    }
}
