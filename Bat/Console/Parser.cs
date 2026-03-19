using Bat.Nodes;
using Bat.Tokens;
using Context;

namespace Bat.Console;

/// <summary>
/// Parses batch script input into a command tree.
/// </summary>
internal class Parser(IContext context)
{
    private readonly TokenSet tokens = [];

    /// <summary>
    /// Parse input text into a ParsedCommand tree
    /// </summary>
    public void Append(string input)
    {
        Tokenizer.AppendTokens(context, tokens, input);
    }

    public string? ErrorMessage => tokens.ErrorMessage;

    public bool IsIncomplete =>
        tokens.ContextStack.Count > 0 || tokens.LastOrDefault(t => t is not EndOfLineToken and not WhitespaceToken) is ContinuationToken;
 

    internal ParsedCommand ParseCommand()
    {
        // Check for errors first
        if (ErrorMessage != null)
        {
            return new ParsedCommand(new SimpleCommandNode(tokens), ErrorMessage);
        }

        // Determine if incomplete
        if (IsIncomplete)
        {
            return new ParsedCommand(new IncompleteNode(tokens));
        }

        return new ParsedCommand(new SimpleCommandNode(tokens));
    }
}
