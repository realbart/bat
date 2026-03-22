using Bat.Tokens;

namespace Bat.Nodes;

/// <summary>
/// A pipe: cmd1 | cmd2 — matches ReactOS C_PIPE.
/// SeparatorTokens holds the whitespace + pipe + whitespace tokens between left and right.
/// </summary>
internal record PipeNode(
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

