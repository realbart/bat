using Bat.Execution;
using Bat.Nodes;
using Bat.Tokens;
using Context;

namespace Bat.Commands;

[BuiltInCommand("for")]
internal class ForCommand : ICommand
{
    public Task<int> ExecuteAsync(IContext context, IReadOnlyList<IToken> arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        // TODO: Implement in command implementation steps (38)
        throw new NotImplementedException("ForCommand - to be implemented in Step 38");
    }
}
