using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("goto")]
internal class GotoCommand : ICommand
{
    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        // TODO: Implement GOTO in Step 8 (requires ScanLabels + batch file context)
        await batchContext.Console.Error.WriteLineAsync("GOTO: requires a batch file context (Step 8).");
        return 1;
    }
}
