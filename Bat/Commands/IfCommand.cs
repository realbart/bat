using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("if")]
internal class IfCommand : ICommand
{
    public Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections) =>
        // TODO: Implement in command implementation steps (37)
        throw new NotImplementedException("IfCommand - to be implemented in Step 37");
}
