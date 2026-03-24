using Bat.Parsing;
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


    public async Task<ParsedCommand> GetCommandAsync(IContext context)
    {
        do
        {
            var parser = new Parser();
            await console.Out.WriteAsync(context.CurrentPathDisplayName + ">");
            parser.Append(await ReadLine(context));
            while (parser.ErrorMessage is null && parser.IsIncomplete)
            {
                await console.Out.WriteAsync("More? ");
                parser.Append(await ReadLine(context));
            }
            if (parser.ErrorMessage is null) return parser.ParseCommand();
            await console.Error.WriteLineAsync(parser.ErrorMessage);
        } while (true);
    }

    public async Task<string> ReadLine(IContext context) =>
        // todo: character for characer to support autocompletion and history
        (await console.In.ReadLineAsync()) ?? string.Empty;
}