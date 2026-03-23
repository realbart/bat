using Bat.Execution;
using Bat.Nodes;
using Bat.Tokens;
using Context;

namespace Bat.Commands;

internal interface ICommand
{
    Task<int> ExecuteAsync(
        IContext context,
        IReadOnlyList<IToken> arguments,
        BatchContext batchContext,
        IReadOnlyList<Redirection> redirections
    );
}
