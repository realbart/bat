using Bat.Execution;
using Bat.Nodes;
using Bat.Tokens;
using Context;

namespace Bat.Commands;

[BuiltInCommand("goto")]
internal class GotoCommand : ICommand
{
    public Task<int> ExecuteAsync(IContext context, IReadOnlyList<IToken> arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections) =>
        // TODO: Implement in Step 8
        throw new NotImplementedException("GotoCommand - to be implemented in Step 8");
}
