using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("call")]
internal class CallCommand : ICommand
{
    public Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections) =>
        // TODO: Implement in Step 8
        throw new NotImplementedException("CallCommand - to be implemented in Step 8");
}
