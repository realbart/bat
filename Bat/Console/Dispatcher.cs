using Bat.Parsing;
using Context;

namespace Bat.Console;

internal interface IDispatcher
{
    Task<bool> ExecuteCommandAsync(IContext context, IConsole console, ParsedCommand command);
}

internal class Dispatcher : IDispatcher
{
    public async Task<bool> ExecuteCommandAsync(IContext context, IConsole console, ParsedCommand command) =>
        // do stuff
        true;
}