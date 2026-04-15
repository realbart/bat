using Bat.Execution;
using Bat.Parsing;
using Context;
using System.Text;

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

    public async Task ExecuteCommandAsync(IContext context, string command)
    {
        var expanded = Expander.ExpandEnvironmentVariables(command, context);
        if (context.DelayedExpansion)
            expanded = Expander.ExpandDelayedVariables(expanded, context);
        var parser = new Parser();
        parser.Append(expanded);
        await dispatcher.ExecuteCommandAsync(context, console, parser.ParseCommand());
    }

    public async Task ExecuteBatchAsync(IContext context, string batchFilePath)
    {
        var bc = ReplBatchContext.Value;
        bc.Context = context;
        bc.Console = console;
        var executor = new BatchExecutor(console);
        await executor.ExecuteAsync(batchFilePath, "", bc, []);
    }


    public async Task<ParsedCommand> GetCommandAsync(IContext context)
    {
        do
        {
            var parser = new Parser();
            var prompt = "\r\n" + PromptExpander.Expand(context);
            var line = await ReadLine(prompt, console, context);
            if (line is null) continue;

            if (!string.IsNullOrWhiteSpace(line))
            {
                context.CommandHistory.Add(line);
                while (context.CommandHistory.Count > context.HistorySize)
                    context.CommandHistory.RemoveAt(0);
            }

            line = ExpandMacro(line, context);
            var expanded = Expander.ExpandEnvironmentVariables(line, context);
            if (context.DelayedExpansion)
                expanded = Expander.ExpandDelayedVariables(expanded, context);
            parser.Append(expanded);
            while (parser.ErrorMessage is null && parser.IsIncomplete)
            {
                var more = await ReadLine("More? ", console, context);
                if (more is null) break;
                var expandedMore = Expander.ExpandEnvironmentVariables(more, context);
                if (context.DelayedExpansion)
                    expandedMore = Expander.ExpandDelayedVariables(expandedMore, context);
                parser.Append(expandedMore);
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
        _lineEditor.HistorySize = context.HistorySize;
        return _lineEditor.ReadLine(prompt, console, context);
    }

    private static string ExpandMacro(string line, IContext context)
    {
        if (context.Macros.Count == 0) return line;

        var trimmed = line.TrimStart();
        if (trimmed.Length == 0) return line;

        var wordEnd = 0;
        while (wordEnd < trimmed.Length && !char.IsWhiteSpace(trimmed[wordEnd]))
            wordEnd++;

        var firstWord = trimmed[..wordEnd];
        if (!context.Macros.TryGetValue(firstWord, out var macroText))
            return line;

        var argsText = wordEnd < trimmed.Length ? trimmed[wordEnd..].TrimStart() : "";
        var argParts = string.IsNullOrEmpty(argsText)
            ? []
            : argsText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var result = new StringBuilder();
        for (var i = 0; i < macroText.Length; i++)
        {
            if (macroText[i] == '$' && i + 1 < macroText.Length)
            {
                var next = char.ToUpperInvariant(macroText[i + 1]);
                if (next == 'T') { result.Append('&'); i++; continue; }
                if (next == '*') { result.Append(argsText); i++; continue; }
                if (next == '$') { result.Append('$'); i++; continue; }
                if (next >= '1' && next <= '9')
                {
                    var idx = next - '1';
                    if (idx < argParts.Length) result.Append(argParts[idx]);
                    i++;
                    continue;
                }
            }
            result.Append(macroText[i]);
        }
        return result.ToString();
    }
}