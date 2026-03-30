using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("endlocal")]
internal class EndLocalCommand : ICommand
{
    public Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        // TODO: Implement ENDLOCAL in Step 10 (restore environment snapshot)
        return Task.FromResult(0);
    }
}
