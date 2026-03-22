using Bat.Tokens;

namespace Bat.Nodes;

/// <summary>
/// cmd1 &amp; cmd2 — matches ReactOS C_MULTI.
/// SeparatorTokens holds the whitespace + operator + whitespace tokens between left and right.
/// </summary>
internal record MultiNode(
    ICommandNode Left,
    IReadOnlyList<IToken> SeparatorTokens,
    ICommandNode Right,
    IReadOnlyList<Redirection> Redirections) : ICommandNode
{
    public IEnumerable<IToken> GetTokens()
    {
        foreach (var t in Left.GetTokens()) yield return t;
        foreach (var t in SeparatorTokens) yield return t;
        foreach (var t in Right.GetTokens()) yield return t;
        foreach (var r in Redirections) { yield return r.Token; foreach (var t in r.Target) yield return t; }
    }
}

/// <summary>
/// cmd1 &amp;&amp; cmd2 — matches ReactOS C_AND.
/// </summary>
internal record AndNode(
    ICommandNode Left,
    IReadOnlyList<IToken> SeparatorTokens,
    ICommandNode Right,
    IReadOnlyList<Redirection> Redirections) : ICommandNode
{
    public IEnumerable<IToken> GetTokens()
    {
        foreach (var t in Left.GetTokens()) yield return t;
        foreach (var t in SeparatorTokens) yield return t;
        foreach (var t in Right.GetTokens()) yield return t;
        foreach (var r in Redirections) { yield return r.Token; foreach (var t in r.Target) yield return t; }
    }
}

/// <summary>
/// cmd1 || cmd2 — matches ReactOS C_OR.
/// </summary>
internal record OrNode(
    ICommandNode Left,
    IReadOnlyList<IToken> SeparatorTokens,
    ICommandNode Right,
    IReadOnlyList<Redirection> Redirections) : ICommandNode
{
    public IEnumerable<IToken> GetTokens()
    {
        foreach (var t in Left.GetTokens()) yield return t;
        foreach (var t in SeparatorTokens) yield return t;
        foreach (var t in Right.GetTokens()) yield return t;
        foreach (var r in Redirections) { yield return r.Token; foreach (var t in r.Target) yield return t; }
    }
}
