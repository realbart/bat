using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("setlocal")]
internal class SetLocalCommand : ICommand
{
    public Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        // TODO: Implement SETLOCAL in Step 10 (environment snapshot)
        return Task.FromResult(0);
    }
}
