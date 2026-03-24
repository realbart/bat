using Bat.Execution;
using Bat.Nodes;
using Bat.Tokens;
using Context;

namespace Bat.Commands;

[BuiltInCommand("else")]
internal class ElseCommand : ICommand
{
    public Task<int> ExecuteAsync(IContext context, IReadOnlyList<IToken> arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections) =>
        // TODO: Part of IF implementation (Step 37)
        throw new NotImplementedException("ElseCommand - part of IF implementation");
}
