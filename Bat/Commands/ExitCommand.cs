using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("exit", Flags = "B")]
[BuiltInCommand("quit", Flags = "B")]
internal class ExitCommand : ICommand
{
    internal const int ExitSentinel = int.MinValue;

    public Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        var context = batchContext.Context!;

        bool batchOnly = arguments.GetFlagValue('B');

        if (int.TryParse(arguments.Positionals.FirstOrDefault(), out int code))
            context.ErrorCode = code;

        // /B in batch context returns from batch but keeps interpreter running;
        // in REPL (or when not in batch), treat identical to EXIT.
        if (batchOnly && batchContext.IsBatchFile)
            return Task.FromResult(0);

        return Task.FromResult(ExitSentinel);
    }
}
