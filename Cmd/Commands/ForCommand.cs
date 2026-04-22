using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("for")]
internal class ForCommand : ICommand
{
    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        // TODO: Implement FOR in Step 38
        await batchContext.Console.Error.WriteLineAsync("FOR: not yet implemented (Step 38).");
        return 1;
    }
}
