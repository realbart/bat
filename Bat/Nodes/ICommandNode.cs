using Bat.Tokens;

namespace Bat.Nodes;

/// <summary>
/// Base class for all nodes in the command tree
/// </summary>
internal interface ICommandNode
{
    IEnumerable<IToken> GetTokens();
}
