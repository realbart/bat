using Context;

namespace Bat.Console;

internal class Repl(IConsole console, IDispatcher dispatcher) : IRepl
{
    public async Task StartAsync(IContext context)
    {
#pragma warning disable S1116 // the code is not in the body
        while (await dispatcher.ExecuteCommandAsync(context, console, await ReadLine(context))) ;
#pragma warning restore S1116
    }

    public Task<string?> ReadLine(IContext context)
    {
        console.Write(context.CurrentPathDisplayName + "> ");
        return Task.FromResult(console.ReadLine());
    }
}
