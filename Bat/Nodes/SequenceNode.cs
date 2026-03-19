using Bat.Tokens;

namespace Bat.Nodes;

/// <summary>
/// Commands separated by &, &&, ||
/// </summary>
internal record SequenceNode(IReadOnlyList<(ICommandNode Command, IToken? Separator)> Commands) : ICommandNode
{
    public IEnumerable<IToken> GetTokens() =>
        Commands.SelectMany(item => 
            item.Separator != null 
                ? [..item.Command.GetTokens(), item.Separator]
                : item.Command.GetTokens());
}
