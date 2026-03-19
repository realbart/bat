using Bat.Tokens;

namespace Bat.Nodes;

/// <summary>
/// A simple command like "echo hello" or "xcopy /s"
/// </summary>
internal record SimpleCommandNode(IReadOnlyList<IToken> Tokens) : ICommandNode
{
    public IEnumerable<IToken> GetTokens() => Tokens;
}
