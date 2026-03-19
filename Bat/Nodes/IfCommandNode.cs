using Bat.Tokens;

namespace Bat.Nodes;

/// <summary>
/// An if statement: if condition (then-block) [else (else-block)]
/// </summary>
internal record IfCommandNode(
    IReadOnlyList<IToken> ConditionTokens,
    ICommandNode ThenBranch,
    ICommandNode ElseBranch) : ICommandNode
{
    public IEnumerable<IToken> GetTokens() =>
        [..ConditionTokens, ..ThenBranch.GetTokens(), ..ElseBranch.GetTokens()];
}
