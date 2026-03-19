using Bat.Tokens;

namespace Bat.Nodes;

/// <summary>
/// A for loop: for %%i in (set) do command
/// </summary>
internal record ForCommandNode(
    IReadOnlyList<IToken> HeaderTokens,  // "for %%i in"
    IReadOnlyList<IToken> SetTokens,      // contents of (...)
    ICommandNode Body) : ICommandNode
{
    public IEnumerable<IToken> GetTokens() =>
        [..HeaderTokens, Token.BlockStart, ..SetTokens, Token.BlockEnd, ..Body.GetTokens()];
}
