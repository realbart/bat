using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("if")]
internal class IfCommand : ICommand
{
    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        // TODO: Implement IF in Step 37
        await batchContext.Console.Error.WriteLineAsync("IF: not yet implemented (Step 37).");
        return 1;
    }
}
