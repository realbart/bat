using Bat.Commands;
using Bat.Console;
using Context;
using System.Text;

namespace Bat.Tokens;

internal static partial class Tokenizer
{
    internal static void AppendTokens(TokenSet tokenSet, string input, string eol = "")
    {
        if (string.IsNullOrEmpty(input))
        {
            tokenSet.Add(Token.EndOfLine(eol));
            return;
        }

        var scanner = new Scanner(input, tokenSet.ContextStack);
        TokenizeLine(ref scanner, tokenSet);

        if (tokenSet.Count == 0 || tokenSet[^1] is not EndOfLineToken)
        {
            tokenSet.Add(Token.EndOfLine(eol));
        }
    }

    private static void TokenizeLine(ref Scanner scanner, TokenSet tokenSet)
    {
        while (!scanner.IsAtEnd)
        {
            if (tokenSet.ErrorMessage != null) return;

            var token = scanner.Ch0 switch
            {
                '\r' or '\n' => TokenizeLineEnd(ref scanner, tokenSet),
                ' ' or '\t' => TokenizeWhitespace(ref scanner),
                '@' when (IsAtStartOfLine(tokenSet) || IsExpectingCommand(ref scanner)) => Yield(ref scanner, 1, Token.EchoSupressor),
                ':' when (IsAtStartOfLine(tokenSet) || IsExpectingCommand(ref scanner)) => TokenizeLabel(ref scanner),
                '^' => TokenizeEscape(ref scanner),
                '"' or '\'' => TokenizeQuotedString(ref scanner, scanner.Ch0),
                '%' => TokenizeVariable(ref scanner),
                '!' => TokenizeDelayedExpansion(ref scanner),
                '(' => TokenizeBlockStart(ref scanner),
                ')' => TokenizeBlockEnd(ref scanner),
                '>' => TokenizeGreaterThan(ref scanner),
                '<' => Yield(ref scanner, 1, Token.InputRedirection),
                '2' => TokenizeStdErrRedirection(ref scanner),
                '1' => TokenizeStdOutRedirection(ref scanner) ?? TokenizeTextOrCommand(ref scanner),
                '&' => TokenizeAmpersand(ref scanner),
                '|' => TokenizePipe(ref scanner),
                '=' => TokenizeEquals(ref scanner),
                _ => TokenizeTextOrCommand(ref scanner)
            };

            switch (token)
            {
                case null:
                    break;
                case ErrorToken error:
                    tokenSet.ErrorMessage = error.Message;
                    break;
                default:
                    tokenSet.Add(token);
                    break;
            }
        }
    }

    private static IToken? TokenizeLineEnd(ref Scanner scanner, TokenSet tokenSet)
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

    private static IToken? TokenizeEscape(ref Scanner scanner)
    {
        if (scanner.Ch1 == '\r' || scanner.Ch1 == '\n') return Yield(ref scanner, 1, Token.Escape);
        if (scanner.Position == scanner.Input.Length - 1) return Yield(ref scanner, 1, Token.Escape);
        scanner.Advance();
        if (scanner.IsAtEnd) return null;
        var escaped = scanner.Ch0;
        scanner.Advance();
        return Token.Text(escaped.ToString(), $"^{escaped}");
    }

    private static IToken TokenizeWhitespace(ref Scanner scanner)
    {
        var sb = new StringBuilder();

        while (!scanner.IsAtEnd && (scanner.Ch0 == ' ' || scanner.Ch0 == '\t'))
        {
            sb.Append(scanner.Ch0);
            scanner.Advance();
        }

        return Token.Whitespace(sb.ToString());
    }

    private static IToken TokenizeLabel(ref Scanner scanner)
    {
        scanner.Advance();

        var sb = new StringBuilder();

        if (!scanner.IsAtEnd && scanner.Ch0 == ':')
        {
            sb.Append(':');
            scanner.Advance();
        }

        while (!scanner.IsAtEnd)
        {
            sb.Append(scanner.Ch0);
            scanner.Advance();
        }

        return Token.Label(sb.ToString().TrimEnd(), sb.ToString());
    }

    private static bool IsAtStartOfLine(TokenSet tokenSet)
        => tokenSet.Count == 0 || tokenSet[^1] is EndOfLineToken;

    private static bool IsExpectingCommand(ref Scanner scanner)
        => scanner.Expected.HasFlag(ExpectedTokenTypes.Command) && !scanner.HasCommand;

    private static IToken TokenizeBlockStart(ref Scanner scanner)
    {
        var currentContext = scanner.ContextStack.Count > 0 ? scanner.ContextStack.Peek() : BlockContext.None;

        if (IsExpectingCommand(ref scanner) ||
            scanner.Expected.HasFlag(ExpectedTokenTypes.ForSet) ||
            (currentContext == BlockContext.If && scanner.Expected.HasFlag(ExpectedTokenTypes.Command)))
        {
            var newContext = currentContext switch
            {
                BlockContext.If => BlockContext.IfBlock,
                BlockContext.For when scanner.Expected.HasFlag(ExpectedTokenTypes.ForSet) => BlockContext.ForSet,
                _ => BlockContext.Generic
            };

            if (currentContext == BlockContext.If || (currentContext == BlockContext.For && scanner.Expected.HasFlag(ExpectedTokenTypes.ForSet)))
            {
                scanner.ContextStack.Pop();
            }

            scanner.ContextStack.Push(newContext);

            if (newContext == BlockContext.ForSet)
            {
                scanner.Expected = ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace | ExpectedTokenTypes.BlockEnd;
            }
            else
            {
                scanner.Expected = ExpectedTokenTypes.StartOfCommand;
            }

            scanner.HasCommand = false;
            return Yield(ref scanner, 1, Token.BlockStart)!;
        }

        if (currentContext == BlockContext.If)
        {
            return Yield(ref scanner, 1, new ErrorToken("( was unexpected at this time."))!;
        }

        return Yield(ref scanner, 1, Token.Text("(", "("))!;
    }

    private static IToken TokenizeBlockEnd(ref Scanner scanner)
    {
        if (scanner.ContextStack.Count == 0)
        {
            return Yield(ref scanner, 1, new ErrorToken(") was unexpected at this time."))!;
        }
        var poppedContext = scanner.ContextStack.Pop();

        scanner.Expected = poppedContext switch
        {
            BlockContext.IfBlock => ExpectedTokenTypes.AfterBlockEnd | ExpectedTokenTypes.Else,
            BlockContext.ForSet => ExpectedTokenTypes.ForDoClause | ExpectedTokenTypes.Whitespace,
            _ => ExpectedTokenTypes.AfterBlockEnd & ~ExpectedTokenTypes.Else
        };

        return Yield(ref scanner, 1, Token.BlockEnd)!;
    }

    private static IToken TokenizeQuotedString(ref Scanner scanner, char quote)
    {
        scanner.Advance();

        var sb = new StringBuilder();

        while (!scanner.IsAtEnd)
        {
            var ch = scanner.Ch0;

            if (ch == quote)
            {
                scanner.Advance();
                return Token.QuotedText(quote.ToString(), sb.ToString(), quote.ToString());
            }

            sb.Append(ch);
            scanner.Advance();
        }

        return Token.QuotedText(quote.ToString(), sb.ToString(), "");
    }

    private static IToken TokenizeVariable(ref Scanner scanner)
    {
        var raw = new StringBuilder("%");
        scanner.Advance();

        if (!scanner.IsAtEnd && scanner.Ch0 == '%')
        {
            raw.Append('%');
            scanner.Advance();

            if (!scanner.IsAtEnd && char.IsLetter(scanner.Ch0))
            {
                var param = scanner.Ch0.ToString();
                raw.Append(param);
                scanner.Advance();
                return Token.ForParameter(param, raw.ToString());
            }

            return Token.Text("%", raw.ToString());
        }

        if (!scanner.IsAtEnd)
        {
            var ch = scanner.Ch0;

            if (char.IsDigit(ch) || ch == '*' || ch == '~')
            {
                raw.Append(ch);
                scanner.Advance();

                if (ch == '~' && !scanner.IsAtEnd)
                {
                    while (!scanner.IsAtEnd && (char.IsLetter(scanner.Ch0) || char.IsDigit(scanner.Ch0)))
                    {
                        raw.Append(scanner.Ch0);
                        scanner.Advance();
                    }
                }

                return Token.Text(raw.ToString(), raw.ToString());
            }
        }

        while (!scanner.IsAtEnd && scanner.Ch0 != '%')
        {
            raw.Append(scanner.Ch0);
            scanner.Advance();
        }

        if (!scanner.IsAtEnd && scanner.Ch0 == '%')
        {
            raw.Append('%');
            scanner.Advance();
            return Token.Text(raw.ToString(), raw.ToString());
        }

        return Token.Text(raw.ToString(), raw.ToString());
    }

    private static IToken TokenizeDelayedExpansion(ref Scanner scanner)
    {
        scanner.Advance();

        var sb = new StringBuilder();
        var rawSb = new StringBuilder("!");

        while (!scanner.IsAtEnd && scanner.Ch0 != '!')
        {
            var ch = scanner.Ch0;

            if (ch == '^' && scanner.Ch1 != '\0')
            {
                rawSb.Append(ch);
                scanner.Advance();
                if (!scanner.IsAtEnd)
                {
                    var escaped = scanner.Ch0;
                    rawSb.Append(escaped);
                    sb.Append(escaped);
                    scanner.Advance();
                }
            }
            else
            {
                rawSb.Append(ch);
                sb.Append(ch);
                scanner.Advance();
            }
        }

        if (scanner.IsAtEnd || scanner.Ch0 != '!') return Token.Text(rawSb.ToString(), rawSb.ToString());
        rawSb.Append('!');
        scanner.Advance();
        return Token.DelayedExpansionVariable(sb.ToString(), rawSb.ToString());
    }

    private static IToken? TokenizeGreaterThan(ref Scanner scanner)
        => scanner.Ch1 switch
        {
            '>' => Yield(ref scanner, 2, Token.AppendRedirection),
            '&' when scanner.Ch2 == '1' => Yield(ref scanner, 3, Token.StdOutToStdErrRedirection),
            '=' => TokenizeComparison(ref scanner),
            _ when IsInIfCondition(ref scanner) => TokenizeComparison(ref scanner),
            _ => Yield(ref scanner, 1, Token.OutputRedirection)
        };

    private static IToken? TokenizeStdErrRedirection(ref Scanner scanner)
        => scanner.Ch1 != '>'
            ? TokenizeTextOrCommand(ref scanner)
            : (scanner.Ch2, scanner.Ch3) switch
            {
                ('>', _) => Yield(ref scanner, 3, Token.AppendStdErrRedirection),
                ('&', '1') => Yield(ref scanner, 4, Token.StdErrToStdOutRedirection),
                _ => Yield(ref scanner, 2, Token.StdErrRedirection)
            };

    private static IToken? TokenizeStdOutRedirection(ref Scanner scanner)
    {
        if (scanner.Ch1 != '>') return null;
        if (scanner.Ch2 == '&' && scanner.Ch3 == '2') return Yield(ref scanner, 4, Token.StdOutToStdErrRedirection);
        scanner.Advance();
        return Token.Text("1", "1");
    }

    private static IToken TokenizeAmpersand(ref Scanner scanner)
    {
        var currentContext = scanner.ContextStack.Count > 0 ? scanner.ContextStack.Peek() : BlockContext.None;
        var advance = scanner.Ch1 == '&' ? 2 : 1;

        if (!scanner.HasCommand || currentContext == BlockContext.If)
        {
            return Yield(ref scanner, advance, new ErrorToken("& was unexpected at this time."))!;
        }

        scanner.Expected = ExpectedTokenTypes.StartOfCommand;
        scanner.HasCommand = false;
        return Yield(ref scanner, advance, scanner.Ch1 == '&' ? Token.ConditionalAnd : Token.CommandSeparator)!;
    }

    private static IToken TokenizePipe(ref Scanner scanner)
    {
        var currentContext = scanner.ContextStack.Count > 0 ? scanner.ContextStack.Peek() : BlockContext.None;
        var advance = scanner.Ch1 == '|' ? 2 : 1;

        if (!scanner.HasCommand || currentContext == BlockContext.If)
        {
            return Yield(ref scanner, advance, new ErrorToken("| was unexpected at this time."))!;
        }

        scanner.Expected = ExpectedTokenTypes.StartOfCommand;
        scanner.HasCommand = false;
        return Yield(ref scanner, advance, scanner.Ch1 == '|' ? Token.ConditionalOr : Token.Pipe)!;
    }

    private static IToken TokenizeEquals(ref Scanner scanner)
    {
        var sb = new StringBuilder("=");
        scanner.Advance();
        if (!scanner.IsAtEnd && scanner.Ch0 == '=')
        {
            sb.Append('=');
            scanner.Advance();
        }
        var currentContext = scanner.ContextStack.Count > 0 ? scanner.ContextStack.Peek() : BlockContext.None;
        if (currentContext == BlockContext.If)
        {
            scanner.Expected = ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace | ExpectedTokenTypes.Command;
        }
        return Token.ComparisonOperator(sb.ToString());
    }

    private static IToken TokenizeComparison(ref Scanner scanner)
    {
        var sb = new StringBuilder();
        while (!scanner.IsAtEnd && !char.IsWhiteSpace(scanner.Ch0) && ">=<!=".Contains(scanner.Ch0))
        {
            sb.Append(scanner.Ch0);
            scanner.Advance();
        }
        return Token.ComparisonOperator(sb.ToString());
    }

    private static bool IsInIfCondition(ref Scanner scanner)
    {
        if (scanner.ContextStack.Count == 0) return false;
        var ctx = scanner.ContextStack.Peek();
        return ctx == BlockContext.If || ctx == BlockContext.IfBlock;
    }

    private static IToken? Yield(ref Scanner scanner, int advance, IToken token)
    {
        scanner.Advance(advance);
        return token;
    }
}

