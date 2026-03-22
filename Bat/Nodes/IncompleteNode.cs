using Bat.Console;
using Bat.Tokens;

namespace Bat.Nodes;

/// <summary>
/// Incomplete command - needs more input (open block or trailing continuation)
/// </summary>
internal record IncompleteNode(IReadOnlyList<IToken> TokensSoFar) : ICommandNode
{
    public IReadOnlyList<Redirection> Redirections { get; init; } = [];
    public IEnumerable<IToken> GetTokens() => TokensSoFar;
}
