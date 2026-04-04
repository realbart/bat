using Bat.Execution;
using Bat.Parsing;
using Context;

namespace Bat.Console;

internal class Repl(IConsole console, IDispatcher dispatcher) : IRepl
{
    private readonly LineEditor _lineEditor = new();

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
            var prompt = PromptExpander.Expand(context);
            var line = await ReadLine(prompt, console, context);
            if (line is null) continue;
            parser.Append(line);
            while (parser.ErrorMessage is null && parser.IsIncomplete)
            {
                var more = await ReadLine("More? ", console, context);
                if (more is null) break;
                parser.Append(more);
            }
            if (parser.IsIncomplete) continue;
            if (parser.ErrorMessage is null) return parser.ParseCommand();
            await console.Error.WriteLineAsync(parser.ErrorMessage);
        } while (true);
    }

    public async Task<string?> ReadLine(string prompt, IConsole console, IContext context)
    {
        if (!console.IsInteractive)
        {
            await console.Out.WriteAsync(prompt);
            return await console.In.ReadLineAsync();
        }
        return _lineEditor.ReadLine(prompt, console, context);
    }
}