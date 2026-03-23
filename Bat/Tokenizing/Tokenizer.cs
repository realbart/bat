using Bat.Commands;
using Bat.Console;
using Bat.Tokens;
using System.Text;

namespace Bat.Tokenizing;

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

    private static EndOfLineToken? TokenizeLineEnd(ref Scanner scanner, TokenSet tokenSet)
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
        if (scanner.Ch1 is '\r' or '\n') return Yield(ref scanner, 1, Token.Escape);
        var start = scanner.Position;
        scanner.Advance();
        if (scanner.IsAtEnd) return Yield(ref scanner, 0, Token.Escape);
        scanner.Advance();
        return Token.Text(scanner.Substring(start));
    }

    private static WhitespaceToken TokenizeWhitespace(ref Scanner scanner)
    {
        var start = scanner.Position;
        while (!scanner.IsAtEnd && scanner.Ch0 is ' ' or '\t')
        {
            scanner.Advance();
        }
        return Token.Whitespace(scanner.Substring(start));
    }

    private static LabelToken TokenizeLabel(ref Scanner scanner)
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

        return Yield(ref scanner, 1, Token.Text("("))!;
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

    private static QuotedTextToken TokenizeQuotedString(ref Scanner scanner, char quote)
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

    private static IToken TokenizeVariable(ref Scanner scanner)
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

    private static IToken TokenizeDelayedExpansion(ref Scanner scanner)
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
        return Token.Text("1");
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

    private static ComparisonOperatorToken TokenizeEquals(ref Scanner scanner)
    {
        var start = scanner.Position;
        scanner.Advance();
        if (!scanner.IsAtEnd && scanner.Ch0 == '=')
        {
            scanner.Advance();
        }
        var currentContext = scanner.ContextStack.Count > 0 ? scanner.ContextStack.Peek() : BlockContext.None;
        if (currentContext == BlockContext.If)
        {
            scanner.Expected = ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace | ExpectedTokenTypes.Command;
        }
        return Token.ComparisonOperator(scanner.Substring(start));
    }

    private static ComparisonOperatorToken TokenizeComparison(ref Scanner scanner)
    {
        var start = scanner.Position;
        while (!scanner.IsAtEnd && scanner.Ch0 is '>' or '<' or '=' or '!')
        {
            scanner.Advance();
        }
        return Token.ComparisonOperator(scanner.Substring(start));
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

    private static IToken? TokenizeTextOrCommand(ref Scanner scanner)
    {
        var text = ReadWord(ref scanner);
        if (text.Length == 0) return null;

        var lower = text.ToLower();

        if (lower == "else") return HandleElse(ref scanner, text);
        if (scanner.Expected.HasFlag(ExpectedTokenTypes.ForInClause) && lower == "in") return HandleForIn(ref scanner, text);
        if (scanner.Expected.HasFlag(ExpectedTokenTypes.ForDoClause) && lower == "do") return HandleForDo(ref scanner, text);
        return IsExpectingCommand(ref scanner) ? HandleCommandToken(ref scanner, text, lower) : HandleTextToken(ref scanner, text);
    }

    private static string ReadWord(ref Scanner scanner)
    {
        var start = scanner.Position;
        while (!scanner.IsAtEnd && scanner.Ch0 is not ' ' and not '\t' and not '\r' and not '\n'
               and not '(' and not ')' and not '"' and not '\'' and not '%' and not '!' 
               and not '&' and not '|' and not '<' and not '>' and not '=' and not '^')
        {
            scanner.Advance();
        }
        return scanner.Substring(start);
    }

    private static IToken HandleElse(ref Scanner scanner, string text)
    {
        if (scanner.Expected.HasFlag(ExpectedTokenTypes.Else))
        {
            if (scanner.ContextStack.Count > 0) scanner.ContextStack.Pop();
            scanner.Expected = ExpectedTokenTypes.Command | ExpectedTokenTypes.Whitespace;
            scanner.HasCommand = false;
            return Token.BuiltInCommand<ElseCommand>(text);
        }

        var isCommandBoundary = scanner.Expected.HasFlag(ExpectedTokenTypes.Command);
        var isAfterNonIfBlock = scanner.Expected.HasFlag(ExpectedTokenTypes.CommandSeparator)
                              && !scanner.Expected.HasFlag(ExpectedTokenTypes.Text);

        if (isCommandBoundary || isAfterNonIfBlock)
        {
            return new ErrorToken("else was unexpected at this time.");
        }

        scanner.Expected = ExpectedTokenTypes.AfterCommand;
        return Token.Text(text);
    }

    private static TextToken HandleForIn(ref Scanner scanner, string text)
    {
        scanner.Expected = ExpectedTokenTypes.ForSet | ExpectedTokenTypes.Whitespace;
        return Token.Text(text);
    }

    private static TextToken HandleForDo(ref Scanner scanner, string text)
    {
        scanner.Expected = ExpectedTokenTypes.StartOfCommand;
        return Token.Text(text);
    }

    private static IToken HandleCommandToken(ref Scanner scanner, string text, string lower)
    {
        var commandType = BuiltInCommandRegistry.GetCommandType(lower);
        var token = commandType != null
            ? CreateBuiltInCommandToken(commandType, text)
            : Token.Command(text);

        UpdateStateForCommand(ref scanner, commandType);
        scanner.HasCommand = true;
        return token;
    }

    private static IToken CreateBuiltInCommandToken(Type commandType, string text)
    {
        var factoryMethod = typeof(Token).GetMethod(nameof(Token.BuiltInCommand))!;
        var genericFactory = factoryMethod.MakeGenericMethod(commandType);
        return (IToken)genericFactory.Invoke(null, [text])!;
    }

    private static void UpdateStateForCommand(ref Scanner scanner, Type? commandType)
    {
        if (commandType == typeof(IfCommand))
        {
            scanner.ContextStack.Push(BlockContext.If);
            scanner.Expected = ExpectedTokenTypes.IfCondition | ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace;
            return;
        }
        if (commandType == typeof(ForCommand))
        {
            scanner.ContextStack.Push(BlockContext.For);
            scanner.Expected = ExpectedTokenTypes.ForInClause | ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace;
            return;
        }
        scanner.Expected = ExpectedTokenTypes.AfterCommand;
    }

    private static IToken HandleTextToken(ref Scanner scanner, string text)
    {
        if (IsInIfCondition(ref scanner) && IsComparisonOperator(text))
        {
            scanner.Expected = ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace | ExpectedTokenTypes.Command;
            return Token.ComparisonOperator(text);
        }
        UpdateExpectedAfterText(ref scanner, text);
        return Token.Text(text);
    }

    private static void UpdateExpectedAfterText(ref Scanner scanner, string text)
    {
        var ctx = scanner.ContextStack.Count > 0 ? scanner.ContextStack.Peek() : BlockContext.None;
        if (ctx != BlockContext.If)
        {
            scanner.Expected = ExpectedTokenTypes.AfterCommand;
            return;
        }
        if (scanner.Expected.HasFlag(ExpectedTokenTypes.IfCondition))
        {
            scanner.Expected = ExpectedAfterIfWord(text);
            return;
        }
        if (!scanner.Expected.HasFlag(ExpectedTokenTypes.Text) && !scanner.Expected.HasFlag(ExpectedTokenTypes.Command))
        {
            scanner.Expected = ExpectedTokenTypes.AfterCommand;
        }
    }

    private static ExpectedTokenTypes ExpectedAfterIfWord(string text)
        => text.ToUpper() switch
        {
            "NOT" => ExpectedTokenTypes.IfCondition | ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace,
            "EXIST" or "DEFINED" or "ERRORLEVEL" => ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace | ExpectedTokenTypes.Command,
            _ when text.StartsWith('/') => ExpectedTokenTypes.IfCondition | ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace,
            _ => ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace
        };

    private static bool IsComparisonOperator(string text)
        => text.ToUpper() is "EQU" or "NEQ" or "LSS" or "LEQ" or "GTR" or "GEQ" or "EXIST" or "DEFINED" or "ERRORLEVEL" or "NOT";
}

