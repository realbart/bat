using Bat.Tokens;

namespace Bat.Nodes;

/// <summary>
/// Base interface for all nodes in the command tree (matches ReactOS PARSED_COMMAND)
/// </summary>
internal interface ICommandNode
{
    /// <summary>Redirections attached to this command (populated by parser)</summary>
    IReadOnlyList<Redirection> Redirections { get; }
    IEnumerable<IToken> GetTokens();
}

/// <summary>
/// A redirection attached to a command (e.g. >file, 2>&1)
/// Matches ReactOS REDIRECTION struct.
/// </summary>
internal record Redirection(
    IToken Token,       // the redirection token itself
    IReadOnlyList<IToken> Target  // file name tokens (or handle for &1)
);
