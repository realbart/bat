using Context;

namespace Bat.Console;

internal class Repl(IConsole console, IDispatcher dispatcher) : IRepl
{
    public async Task StartAsync(IContext context)
    {
#pragma warning disable S1116 // the code is not in the body
        while (await dispatcher.ExecuteCommandAsync(context, console, await GetCommandAsync(context))) ;
#pragma warning restore S1116
    }


    public async Task<TokenSet> GetCommandAsync(IContext context)
    {
        await console.Out.WriteAsync(context.CurrentPathDisplayName + ">");
        var command = Tokenizer.Tokenize(context, await ReadLine(context));
        while (!command.IsComplete)
        {
            await console.Out.WriteAsync("More? ");
            command = Tokenizer.Tokenize(context, await ReadLine(context), command);
        }
        return command;
    }

    public async Task<string> ReadLine(IContext context)
    {
        // todo: character for characer to support autocompletion and history
        return (await console.In.ReadLineAsync())?? string.Empty;
    }
}