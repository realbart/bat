using Bat.Console;
using Bat.Context;
using Context;

namespace Bat.Tokens;

public static class Program
{
    internal static IRepl Repl { get; } = new Repl(new Console.Console(), new Dispatcher());

    public static Task<int> Main(params string[] args) => Main(ContextFactory.CreateContext(), args);

    public static async Task<int> Main(IContext context, params string[] args)
    {
        await Repl.StartAsync(context);
        return context.ErrorCode;
    }
}