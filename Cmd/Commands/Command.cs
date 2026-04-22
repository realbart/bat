using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

internal interface ICommand
{
    Task<int> ExecuteAsync(
        IArgumentSet arguments,
        BatchContext batchContext,
        IReadOnlyList<Redirection> redirections
    );
}
