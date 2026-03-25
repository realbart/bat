using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("exit", Flags = "B")]
[BuiltInCommand("quit", Flags = "B")]
internal class ExitCommand : ICommand
{
    internal const int ExitSentinel = int.MinValue;
    internal const int ExitBatchSentinel = int.MinValue + 1;

    public Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        var context = batchContext.Context!;

        var batchOnly = arguments.GetFlagValue('B');

        if (int.TryParse(arguments.Positionals.FirstOrDefault(), out var code))
            context.ErrorCode = code;

        if (batchOnly && batchContext.IsBatchFile)
            return Task.FromResult(ExitBatchSentinel);

        return Task.FromResult(ExitSentinel);
    }
}
