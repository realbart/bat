using Bat.Console;
using Bat.Tokens;

namespace Bat.Tokenizing;

internal static class LiteralTokenizer
{
    /// <summary>
    /// Processes line endings (\r\n, \n, or \r).
    /// If the previous token was a continuation (^), merges them instead of ending the line.
    /// Resets scanner state to expect a new command on the next line.
    /// </summary>
    public static EndOfLineToken? TokenizeLineEnd(ref Scanner scanner, TokenSet tokenSet)
    {
        var lineEnd = (scanner.Ch0, scanner.Ch1) switch
        {
            ('\r', '\n') => "\r\n",
            ('\n', _) => "\n",
            _ => "\r",
        };

        scanner.Advance(lineEnd.Length);

        var hasContinuation = tokenSet.Count > 0 && tokenSet[^1] is ContinuationToken;

        if (hasContinuation)
        {
            tokenSet[^1] = Token.Continuation("^" + lineEnd);
            return null;
        }

        scanner.Expected = ExpectedTokenTypes.StartOfCommand;
        scanner.HasCommand = false;
        return Token.EndOfLine(lineEnd);
    }

    /// <summary>
    /// Caret escapes the following character, preventing it from being interpreted as special.
    /// At end of line (^ followed by \r or \n), becomes a line continuation token.
    /// Standalone ^ at end of input remains as an escape marker.
    /// </summary>
    public static IToken? TokenizeEscape(ref Scanner scanner)
    {
        if (scanner.Ch1 is '\r' or '\n') return TokenizerHelpers.Yield(ref scanner, 1, Token.Escape);
        var start = scanner.Position;
        scanner.Advance();
        if (scanner.IsAtEnd) return TokenizerHelpers.Yield(ref scanner, 0, Token.Escape);
        scanner.Advance();
        return Token.Text(scanner.Substring(start));
    }

    /// <summary>
    /// Reads consecutive spaces and tabs as a single whitespace token.
    /// Preserves exact whitespace for round-trip parsing.
    /// </summary>
    public static WhitespaceToken TokenizeWhitespace(ref Scanner scanner)
    {
        var start = scanner.Position;
        while (!scanner.IsAtEnd && scanner.Ch0 is ' ' or '\t')
        {
            scanner.Advance();
        }
        return Token.Whitespace(scanner.Substring(start));
    }

    /// <summary>
    /// Labels start with : and consume the rest of the line.
    /// :: (double colon) creates a comment-style label.
    /// Labels are only recognized at the start of a line or after command separators.
    /// </summary>
    public static LabelToken TokenizeLabel(ref Scanner scanner)
    {
        var start = scanner.Position;
        scanner.Advance();

        if (!scanner.IsAtEnd && scanner.Ch0 == ':')
        {
            scanner.Advance();
        }

        while (!scanner.IsAtEnd)
        {
            scanner.Advance();
        }

        return Token.Label(scanner.Substring(start));
    }

    /// <summary>
    /// Reads text within quotes (single or double) until the matching closing quote.
    /// Unclosed quotes consume the rest of the line.
    /// No escape processing happens inside quotes in batch files.
    /// </summary>
    public static QuotedTextToken TokenizeQuotedString(ref Scanner scanner, char quote)
    {
        var start = scanner.Position;
        scanner.Advance();

        while (!scanner.IsAtEnd && scanner.Ch0 != quote)
        {
            scanner.Advance();
        }

        if (!scanner.IsAtEnd)
        {
            scanner.Advance();
        }

        return Token.QuotedText(scanner.Substring(start));
    }

    /// <summary>
    /// Dispatches to specialized variable tokenizers based on the character after %.
    /// %% → FOR loop parameter (%%i)
    /// %digit, %*, %~ → Batch script parameters (%1, %*, %~dp0)
    /// Otherwise → Environment variable (%PATH%, %VAR%)
    /// </summary>
    public static IToken TokenizeVariable(ref Scanner scanner)
    {
        var start = scanner.Position;
        scanner.Advance();

        if (scanner.Ch0 == '%') return TokenizeDoublePercent(ref scanner, start);
        if (char.IsDigit(scanner.Ch0) || scanner.Ch0 is '*' or '~') return TokenizeBatchParameter(ref scanner, start);
        return TokenizeEnvironmentVariable(ref scanner, start);
    }

    /// <summary>
    /// FOR loop parameters use %%letter syntax (e.g., %%i, %%j).
    /// Single %% without a letter is treated as literal text.
    /// </summary>
    private static IToken TokenizeDoublePercent(ref Scanner scanner, int start)
    {
        scanner.Advance();

        if (!scanner.IsAtEnd && char.IsLetter(scanner.Ch0))
        {
            scanner.Advance();
            return Token.ForParameter(scanner.Substring(start));
        }

        return Token.Text(scanner.Substring(start));
    }

    /// <summary>
    /// Batch parameters are %0-%9, %*, or extended forms like %~dp0.
    /// The tilde modifier allows extracting path components.
    /// </summary>
    private static TextToken TokenizeBatchParameter(ref Scanner scanner, int start)
    {
        var firstChar = scanner.Ch0;
        scanner.Advance();

        if (firstChar == '~')
        {
            while (!scanner.IsAtEnd && (char.IsLetter(scanner.Ch0) || char.IsDigit(scanner.Ch0)))
            {
                scanner.Advance();
            }
        }

        return Token.Text(scanner.Substring(start));
    }

    /// <summary>
    /// Environment variables use %NAME% syntax.
    /// Reads until the closing % or end of line.
    /// Unclosed variables (%PATH without closing %) are treated as literal text.
    /// </summary>
    private static TextToken TokenizeEnvironmentVariable(ref Scanner scanner, int start)
    {
        while (!scanner.IsAtEnd && scanner.Ch0 != '%')
        {
            scanner.Advance();
        }

        if (!scanner.IsAtEnd)
        {
            scanner.Advance();
        }

        return Token.Text(scanner.Substring(start));
    }

    /// <summary>
    /// Delayed expansion variables use !NAME! syntax (requires setlocal enabledelayedexpansion).
    /// Inside !...!, the caret ^ can escape any character including the closing !.
    /// Unclosed variables (!PATH without closing !) are treated as literal text.
    /// </summary>
    public static IToken TokenizeDelayedExpansion(ref Scanner scanner)
    {
        var start = scanner.Position;
        scanner.Advance();

        while (!scanner.IsAtEnd && scanner.Ch0 != '!')
        {
            scanner.Advance(scanner.Ch0 == '^' && scanner.Ch1 != '\0' ? 2 : 1);
        }

        if (scanner.IsAtEnd || scanner.Ch0 != '!') return Token.Text(scanner.Substring(start));
        scanner.Advance();
        return Token.DelayedExpansionVariable(scanner.Substring(start));
    }
}
