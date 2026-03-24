using Bat.Console;
using Bat.Tokens;

namespace Bat.Tokenizing;

internal static class OperatorTokenizer
{
    public static IToken TokenizeBlockStart(ref Scanner scanner)
    {
        var currentContext = scanner.ContextStack.Count > 0 ? scanner.ContextStack.Peek() : BlockContext.None;

        if (Tokenizer.IsExpectingCommand(ref scanner) ||
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
            return Tokenizer.Yield(ref scanner, 1, Token.BlockStart)!;
        }

        if (currentContext == BlockContext.If)
        {
            return Tokenizer.Yield(ref scanner, 1, new ErrorToken("( was unexpected at this time."))!;
        }

        return Tokenizer.Yield(ref scanner, 1, Token.Text("("))!;
    }

    public static IToken TokenizeBlockEnd(ref Scanner scanner)
    {
        if (scanner.ContextStack.Count == 0)
        {
            return Tokenizer.Yield(ref scanner, 1, new ErrorToken(") was unexpected at this time."))!;
        }
        var poppedContext = scanner.ContextStack.Pop();

        scanner.Expected = poppedContext switch
        {
            BlockContext.IfBlock => ExpectedTokenTypes.AfterBlockEnd | ExpectedTokenTypes.Else,
            BlockContext.ForSet => ExpectedTokenTypes.ForDoClause | ExpectedTokenTypes.Whitespace,
            _ => ExpectedTokenTypes.AfterBlockEnd & ~ExpectedTokenTypes.Else
        };

        return Tokenizer.Yield(ref scanner, 1, Token.BlockEnd)!;
    }

    public static IToken? TokenizeGreaterThan(ref Scanner scanner)
        => scanner.Ch1 switch
        {
            '>' => Tokenizer.Yield(ref scanner, 2, Token.AppendRedirection),
            '&' when scanner.Ch2 == '1' => Tokenizer.Yield(ref scanner, 3, Token.StdOutToStdErrRedirection),
            '=' => TokenizeComparison(ref scanner),
            _ when Tokenizer.IsInIfCondition(ref scanner) => TokenizeComparison(ref scanner),
            _ => Tokenizer.Yield(ref scanner, 1, Token.OutputRedirection)
        };

    public static IToken? TokenizeStdErrRedirection(ref Scanner scanner)
        => scanner.Ch1 != '>'
            ? CommandTokenizer.TokenizeTextOrCommand(ref scanner)
            : (scanner.Ch2, scanner.Ch3) switch
            {
                ('>', _) => Tokenizer.Yield(ref scanner, 3, Token.AppendStdErrRedirection),
                ('&', '1') => Tokenizer.Yield(ref scanner, 4, Token.StdErrToStdOutRedirection),
                _ => Tokenizer.Yield(ref scanner, 2, Token.StdErrRedirection)
            };

    public static IToken? TokenizeStdOutRedirection(ref Scanner scanner)
    {
        if (scanner.Ch1 != '>') return null;
        if (scanner.Ch2 == '&' && scanner.Ch3 == '2') return Tokenizer.Yield(ref scanner, 4, Token.StdOutToStdErrRedirection);
        scanner.Advance();
        return Token.Text("1");
    }

    public static IToken TokenizeAmpersand(ref Scanner scanner)
    {
        var currentContext = scanner.ContextStack.Count > 0 ? scanner.ContextStack.Peek() : BlockContext.None;
        var advance = scanner.Ch1 == '&' ? 2 : 1;

        if (!scanner.HasCommand || currentContext == BlockContext.If)
        {
            return Tokenizer.Yield(ref scanner, advance, new ErrorToken("& was unexpected at this time."))!;
        }

        scanner.Expected = ExpectedTokenTypes.StartOfCommand;
        scanner.HasCommand = false;
        return Tokenizer.Yield(ref scanner, advance, scanner.Ch1 == '&' ? Token.ConditionalAnd : Token.CommandSeparator)!;
    }

    public static IToken TokenizePipe(ref Scanner scanner)
    {
        var currentContext = scanner.ContextStack.Count > 0 ? scanner.ContextStack.Peek() : BlockContext.None;
        var advance = scanner.Ch1 == '|' ? 2 : 1;

        if (!scanner.HasCommand || currentContext == BlockContext.If)
        {
            return Tokenizer.Yield(ref scanner, advance, new ErrorToken("| was unexpected at this time."))!;
        }

        scanner.Expected = ExpectedTokenTypes.StartOfCommand;
        scanner.HasCommand = false;
        return Tokenizer.Yield(ref scanner, advance, scanner.Ch1 == '|' ? Token.ConditionalOr : Token.Pipe)!;
    }

    public static ComparisonOperatorToken TokenizeEquals(ref Scanner scanner)
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

    public static ComparisonOperatorToken TokenizeComparison(ref Scanner scanner)
    {
        var start = scanner.Position;
        while (!scanner.IsAtEnd && scanner.Ch0 is '>' or '<' or '=' or '!')
        {
            scanner.Advance();
        }
        return Token.ComparisonOperator(scanner.Substring(start));
    }
}
