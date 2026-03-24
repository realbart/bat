using Bat.Execution;
using Bat.Nodes;
using Bat.Tokens;
using Context;

namespace Bat.Commands;

[BuiltInCommand("endlocal")]
internal class EndLocalCommand : ICommand
{
    public Task<int> ExecuteAsync(IContext context, IReadOnlyList<IToken> arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections) =>
        // TODO: Implement in Step 11
        throw new NotImplementedException("EndLocalCommand - to be implemented in Step 11");
}
