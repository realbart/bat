using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("else")]
internal class ElseCommand : ICommand
{
    public Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections) =>
        // TODO: Part of IF implementation (Step 37)
        throw new NotImplementedException("ElseCommand - part of IF implementation");
}
