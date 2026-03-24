using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("cls")]
internal class ClsCommand : ICommand
{
    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        await batchContext.Console!.Out.WriteAsync("\x1b[2J\x1b[H");
        return 0;
    }
}
