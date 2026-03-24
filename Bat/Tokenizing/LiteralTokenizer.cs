using Bat.Console;
using Bat.Tokens;

namespace Bat.Tokenizing;

internal static class LiteralTokenizer
{
    public static EndOfLineToken? TokenizeLineEnd(ref Scanner scanner, TokenSet tokenSet)
    {
        var lineEnd = (scanner.Ch0, scanner.Ch1) switch
        {
            ('\r', '\n') => "\r\n",
            ('\n', _) => "\n",
            _ => "\r",
        };

        scanner.Advance(lineEnd.Length);

        bool hasContinuation = tokenSet.Count > 0 && tokenSet[^1] is ContinuationToken;

        if (hasContinuation)
        {
            tokenSet[^1] = Token.Continuation("^" + lineEnd);
            return null;
        }

        scanner.Expected = ExpectedTokenTypes.StartOfCommand;
        scanner.HasCommand = false;
        return Token.EndOfLine(lineEnd);
    }

    public static IToken? TokenizeEscape(ref Scanner scanner)
    {
        if (scanner.Ch1 is '\r' or '\n') return Tokenizer.Yield(ref scanner, 1, Token.Escape);
        var start = scanner.Position;
        scanner.Advance();
        if (scanner.IsAtEnd) return Tokenizer.Yield(ref scanner, 0, Token.Escape);
        scanner.Advance();
        return Token.Text(scanner.Substring(start));
    }

    public static WhitespaceToken TokenizeWhitespace(ref Scanner scanner)
    {
        var start = scanner.Position;
        while (!scanner.IsAtEnd && scanner.Ch0 is ' ' or '\t')
        {
            scanner.Advance();
        }
        return Token.Whitespace(scanner.Substring(start));
    }

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

    public static IToken TokenizeVariable(ref Scanner scanner)
    {
        var start = scanner.Position;
        scanner.Advance();

        if (scanner.Ch0 == '%') return TokenizeDoublePercent(ref scanner, start);
        if (char.IsDigit(scanner.Ch0) || scanner.Ch0 is '*' or '~') return TokenizeBatchParameter(ref scanner, start);
        return TokenizeEnvironmentVariable(ref scanner, start);
    }

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
