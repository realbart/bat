using Bat.Tokens;

namespace Bat.Nodes;

/// <summary>
/// @ prefix: suppresses command echo — matches ReactOS C_QUIET.
/// The EchoSuppressor token is attached here; the real subcommand is in Subcommand.
/// </summary>
internal record QuietNode(
    EchoSupressorToken At,
    ICommandNode Subcommand,
    IReadOnlyList<Redirection> Redirections) : ICommandNode
{
    public IEnumerable<IToken> GetTokens()
    {
        yield return At;
        foreach (var t in Subcommand.GetTokens()) yield return t;
        foreach (var r in Redirections) { yield return r.Token; foreach (var t in r.Target) yield return t; }
    }
}
