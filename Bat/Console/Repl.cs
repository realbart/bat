using Context;

namespace Bat.Console;

internal class Repl(ITokenizer tokenizer, IConsole console, IDispatcher dispatcher) : IRepl
{
    public async Task StartAsync(IContext context)
    {
#pragma warning disable S1116 // the code is not in the body
        while (await dispatcher.ExecuteCommandAsync(context, console, await GetTokensAsync(context))) ;
#pragma warning restore S1116
    }


    public async Task<TokenSet> GetTokensAsync(IContext context)
    {
        await console.Out.WriteAsync(context.CurrentPathDisplayName + ">");
        var tokens = tokenizer.Tokenize(await ReadLine(context));
        while (tokens.IsComplete)
        {
            await console.Out.WriteAsync("More? ");
            tokens = tokenizer.Tokenize(tokens, await ReadLine(context));
        }
        return tokens;
    }

    public async Task<string> ReadLine(IContext context)
    {
        // todo: character for characer to support autocompletion and history
        return (await console.In.ReadLineAsync())?? string.Empty;
    }
}