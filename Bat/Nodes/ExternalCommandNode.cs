using Bat.Execution;
using Bat.Tokens;
using Context;

namespace Bat.Nodes;

/// <summary>
/// Command node for external executables (e.g., notepad.exe, xcopy.exe).
/// Execution will be implemented in Step 6.
/// </summary>
internal record ExternalCommandNode(
    CommandToken CommandToken,
    IReadOnlyList<IToken> Arguments,
    IReadOnlyList<Redirection> Redirections) : ICommandNode
{
    public IEnumerable<IToken> GetTokens()
    {
        yield return CommandToken;
        foreach (var t in Arguments) yield return t;
        foreach (var r in Redirections)
        {
            yield return r.Token;
            foreach (var t in r.Target) yield return t;
        }
    }

    public Task<int> ExecuteAsync(IContext ctx, BatchContext bc)
    {
        // TODO: Step 6 — this node type is superseded by CommandNode + Dispatcher
        return Task.FromResult(0);
    }
}
