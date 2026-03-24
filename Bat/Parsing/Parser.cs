using Bat.Console;
using Bat.Nodes;
using Bat.Tokenizing;
using Bat.Tokens;

namespace Bat.Parsing;

/// <summary>
/// Accumulates tokenised input and drives AST construction.
/// Each parsing phase is delegated to a dedicated static class that receives a
/// <see cref="ParseReader"/> by <c>ref</c>.
/// All whitespace and EOL tokens are threaded through the AST so that
/// <c>GetTokens()</c> round-trips to the exact original source text.
/// </summary>
internal class Parser()
{
    private readonly TokenSet _tokenSet = [];

    /// <summary>
    /// Appends a line of batch source to the token stream.
    /// May be called multiple times when a command spans continuation lines.
    /// </summary>
    public void Append(string input) => Tokenizer.Tokenize(_tokenSet, input);

    /// <summary>
    /// A tokenizer-level syntax error, or <c>null</c> if the input is well-formed so far.
    /// </summary>
    public string? ErrorMessage => _tokenSet.ErrorMessage;

    /// <summary>
    /// True when the token stream ends inside an open block or on a continuation line,
    /// meaning more input is required before the command can be parsed.
    /// IF and FOR contexts without open parentheses are complete at end of line.
    /// </summary>
    public bool IsIncomplete =>
        _tokenSet.ContextStack.Any(c => c is BlockContext.IfBlock or BlockContext.ForSet or BlockContext.Generic) ||
        _tokenSet.LastOrDefault(t => t is not EndOfLineToken and not WhitespaceToken) is ContinuationToken;

    /// <summary>
    /// Builds and returns the AST for the accumulated token stream.
    /// Returns an error node when tokenization or parsing fails,
    /// and an incomplete node when the command spans more lines than have been appended.
    /// </summary>
    internal ParsedCommand ParseCommand()
    {
        if (ErrorMessage != null) return new ParsedCommand(new SimpleCommandNode(_tokenSet), ErrorMessage, _tokenSet);

        if (IsIncomplete) return new ParsedCommand(new IncompleteNode(_tokenSet), null, _tokenSet);

        var reader = new ParseReader(_tokenSet);
        reader.SkipInert();

        var node = SequenceParser.ParseCommandOp(ref reader);

        reader.SkipInert();

        if (!reader.AtEnd && reader.Current is not EndOfLineToken)
            reader.ParseError ??= $"Unexpected token: {reader.Current!.Raw}";

        if (reader.ParseError != null)
            return new ParsedCommand(new SimpleCommandNode(_tokenSet), reader.ParseError, _tokenSet);

        return new ParsedCommand(node ?? EmptyCommandNode.Instance, null, _tokenSet);
    }
}
