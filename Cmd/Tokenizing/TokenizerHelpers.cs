using Bat.Console;
using Bat.Tokens;

namespace Bat.Tokenizing;

/// <summary>
/// Shared helper methods used across all tokenizer classes.
/// Encapsulates common scanner state checks and token yielding logic.
/// </summary>
internal static class TokenizerHelpers
{
    /// <summary>
    /// Helper to advance the scanner and return a token in one operation.
    /// Used for single-character or fixed-length tokens throughout all tokenizers.
    /// </summary>
    public static IToken? Yield(ref Scanner scanner, int advance, IToken token)
    {
        scanner.Advance(advance);
        return token;
    }

    /// <summary>
    /// Determines if the scanner is at a position where a command name should be parsed.
    /// True when expecting a command and no command has been seen yet on this line.
    /// </summary>
    public static bool IsExpectingCommand(ref Scanner scanner)
        => scanner.Expected.HasFlag(ExpectedTokenTypes.Command) && !scanner.HasCommand;

    /// <summary>
    /// Checks if the current context is within an IF statement or IF block.
    /// This affects how certain characters (>, =, etc.) and words (EQU, LSS, etc.) are interpreted.
    /// </summary>
    public static bool IsInIfCondition(ref Scanner scanner)
    {
        if (scanner.ContextStack.Count == 0) return false;
        var ctx = scanner.ContextStack.Peek();
        return ctx == BlockContext.If || ctx == BlockContext.IfBlock;
    }

    public static bool IsExpectingIfCondition(ref Scanner scanner) =>
        scanner.Expected.HasFlag(ExpectedTokenTypes.IfCondition) ||
        scanner.Expected.HasFlag(ExpectedTokenTypes.IfUnaryArg);

    /// <summary>
    /// Checks if we're at the start of a new line (no tokens yet, or last token was end-of-line).
    /// Used to determine if @ (echo suppressor) and : (label) have special meaning.
    /// </summary>
    public static bool IsAtStartOfLine(TokenSet tokenSet)
        => tokenSet.Count == 0 || tokenSet[^1] is EndOfLineToken;
}
