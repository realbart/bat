using Bat.Tokens;

namespace Bat.Nodes;

/// <summary>
/// Empty command node - represents absence of a command
/// </summary>
internal record EmptyCommandNode : ICommandNode
{
    public static readonly EmptyCommandNode Instance = new();
    private EmptyCommandNode() { }
    public IReadOnlyList<Redirection> Redirections { get; init; } = [];
    public IEnumerable<IToken> GetTokens() => [];
}
