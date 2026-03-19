using Bat.Tokens;

namespace Bat.Nodes;

/// <summary>
/// A block of commands: (command1 & command2 & ...)
/// </summary>
internal record BlockNode(IReadOnlyList<ICommandNode> Commands) : ICommandNode
{
    public IEnumerable<IToken> GetTokens() =>
        [Token.BlockStart, ..Commands.SelectMany(cmd => cmd.GetTokens()), Token.BlockEnd];
}
