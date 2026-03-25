using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("echo")]
internal class EchoCommand : ICommand
{
    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        var context = batchContext.Context;
        var console = batchContext.Console;
        var args = arguments.FullArgument;

        if (args.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            context.EchoEnabled = true;
            return 0;
        }
        if (args.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            context.EchoEnabled = false;
            return 0;
        }
        if (args.Length == 0)
        {
            await console.Out.WriteLineAsync($"ECHO is {(context.EchoEnabled ? "on" : "off")}.");
            return 0;
        }

        await console.Out.WriteLineAsync(args);
        return 0;
    }
}
