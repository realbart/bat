using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("else")]
internal class ElseCommand : ICommand
{
    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        // TODO: Implement ELSE as part of IF in Step 37
        await batchContext.Console.Error.WriteLineAsync("Syntax error.");
        return 1;
    }
}
