using Bat.Tokens;

namespace Bat.Nodes;

/// <summary>
/// A parenthesised block of commands: ( cmd1 \n cmd2 … ) — matches ReactOS C_BLOCK.
/// Subcommands are linked via their Next property (here modelled as a list).
/// </summary>
internal record BlockNode(
    IReadOnlyList<ICommandNode> Subcommands,
    IReadOnlyList<Redirection> Redirections) : ICommandNode
{
    public IEnumerable<IToken> GetTokens()
    {
        yield return Token.BlockStart;
        foreach (var cmd in Subcommands)
            foreach (var t in cmd.GetTokens()) yield return t;
        yield return Token.BlockEnd;
        foreach (var r in Redirections) { yield return r.Token; foreach (var t in r.Target) yield return t; }
    }
}
