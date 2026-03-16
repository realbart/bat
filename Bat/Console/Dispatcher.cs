using Context;

namespace Bat.Console;

internal interface IDispatcher
{
    Task<bool> ExecuteCommandAsync(IContext context, IConsole console, string? command);
}

internal class Dispatcher : IDispatcher
{
    public async Task<bool> ExecuteCommandAsync(IContext context, IConsole console, string? command)
    {
        if (command == null) return false;
        // Implementation of command execution.
        // return false if the command causes an exit.
        return true;
    }
}
