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
        if (int.TryParse(arguments.Positionals.FirstOrDefault(), out var code)) batchContext.Context.ErrorCode = code;
        if (arguments.GetFlagValue('B') && batchContext.IsBatchFile) return Task.FromResult(ExitBatchSentinel);
        return Task.FromResult(ExitSentinel);
    }
}
