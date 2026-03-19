using Bat.Console;
using Bat.Tokens;

namespace Bat.Nodes;

/// <summary>
/// Incomplete command - needs more input
/// </summary>
internal record IncompleteNode(IReadOnlyList<IToken> TokensSoFar) : ICommandNode
{
    public IEnumerable<IToken> GetTokens() => TokensSoFar;
}
