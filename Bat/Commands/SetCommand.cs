using Bat.Execution;
using Bat.Nodes;
using Bat.Tokens;
using Context;

namespace Bat.Commands;

[BuiltInCommand("set")]
internal class SetCommand : ICommand
{
    public Task<int> ExecuteAsync(IContext context, IReadOnlyList<IToken> arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        // TODO: Implement in Step 4
        throw new NotImplementedException("SetCommand - to be implemented in Step 4");
    }
}
