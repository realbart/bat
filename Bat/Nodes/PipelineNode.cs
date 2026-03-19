using Bat.Tokens;

namespace Bat.Nodes;

/// <summary>
/// A piped command: cmd1 | cmd2 | cmd3
/// </summary>
internal record PipelineNode(IReadOnlyList<ICommandNode> Commands) : ICommandNode
{
    public IEnumerable<IToken> GetTokens() =>
        Commands.SelectMany((cmd, index) => 
            index > 0 
                ? [Token.Pipe, ..cmd.GetTokens()]
                : cmd.GetTokens());
}
