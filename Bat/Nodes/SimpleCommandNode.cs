using Bat.Tokens;

namespace Bat.Nodes;

/// <summary>
/// A simple command: name + arguments (matches ReactOS C_COMMAND).
/// Head = the command name token; Tail = remaining argument tokens.
/// </summary>
internal record CommandNode(
    IToken Head,
    IReadOnlyList<IToken> Tail,
    IReadOnlyList<Redirection> Redirections) : ICommandNode
{
    public IEnumerable<IToken> GetTokens()
    {
        yield return Head;
        foreach (var t in Tail) yield return t;
        foreach (var r in Redirections) { yield return r.Token; foreach (var t in r.Target) yield return t; }
    }
}

/// <summary>
/// Legacy alias kept so existing tests that reference SimpleCommandNode still compile
/// while we migrate.
/// </summary>
internal record SimpleCommandNode(IReadOnlyList<IToken> Tokens) : ICommandNode
{
    public IReadOnlyList<Redirection> Redirections { get; init; } = [];
    public IEnumerable<IToken> GetTokens() => Tokens;
}
