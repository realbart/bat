using Bat.Console;
using Bat.Tokens;

namespace Bat.Tokenizing;

internal static class OperatorTokenizer
{
    /// <summary>
    /// Opening parenthesis can start a block (for grouping commands) or be literal text.
    /// Only interpreted as block start when expecting a command, at FOR set position,
    /// or within an IF context. Otherwise treated as literal text.
    /// </summary>
    public static IToken TokenizeBlockStart(ref Scanner scanner)
    {
        var currentContext = scanner.ContextStack.Count > 0 ? scanner.ContextStack.Peek() : BlockContext.None;

        if (TokenizerHelpers.IsExpectingCommand(ref scanner) ||
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
            return TokenizerHelpers.Yield(ref scanner, 1, Token.BlockStart)!;
        }

        if (currentContext == BlockContext.If)
        {
            return TokenizerHelpers.Yield(ref scanner, 1, new ErrorToken("( was unexpected at this time."))!;
        }

        return TokenizerHelpers.Yield(ref scanner, 1, Token.Text("("))!;
    }

    /// <summary>
    /// Closing parenthesis ends a block started by (.
    /// Pops the context stack and updates expectations based on what kind of block ended.
    /// IF blocks can be followed by ELSE; FOR blocks expect DO clause; others expect command separators.
    /// </summary>
    public static IToken TokenizeBlockEnd(ref Scanner scanner)
    {
        if (scanner.ContextStack.Count == 0)
        {
            return TokenizerHelpers.Yield(ref scanner, 1, new ErrorToken(") was unexpected at this time."))!;
        }
        var poppedContext = scanner.ContextStack.Pop();

        scanner.Expected = poppedContext switch
        {
            BlockContext.IfBlock => ExpectedTokenTypes.AfterBlockEnd | ExpectedTokenTypes.Else,
            BlockContext.ForSet => ExpectedTokenTypes.ForDoClause | ExpectedTokenTypes.Whitespace,
            _ => ExpectedTokenTypes.AfterBlockEnd & ~ExpectedTokenTypes.Else
        };

        return TokenizerHelpers.Yield(ref scanner, 1, Token.BlockEnd)!;
    }

    /// <summary>
    /// Greater-than has multiple meanings based on context and following characters:
    /// >> = append redirection, >&1 = redirect to stdout, >= = comparison (in IF)
    /// > alone = output redirection or comparison operator (in IF conditions).
    /// </summary>
    public static IToken? TokenizeGreaterThan(ref Scanner scanner)
        => scanner.Ch1 switch
        {
            '>' => TokenizerHelpers.Yield(ref scanner, 2, Token.AppendRedirection),
            '&' when scanner.Ch2 == '1' => TokenizerHelpers.Yield(ref scanner, 3, Token.StdOutToStdErrRedirection),
            '=' => TokenizeComparison(ref scanner),
            _ when TokenizerHelpers.IsInIfCondition(ref scanner) => TokenizeComparison(ref scanner),
            _ => TokenizerHelpers.Yield(ref scanner, 1, Token.OutputRedirection)
        };

    /// <summary>
    /// Digit 2 followed by > redirects stderr.
    /// 2>> = append stderr, 2>&1 = redirect stderr to stdout, 2> = stderr redirection.
    /// Standalone 2 is treated as text or start of a command name.
    /// </summary>
    public static IToken? TokenizeStdErrRedirection(ref Scanner scanner)
        => scanner.Ch1 != '>'
            ? CommandTokenizer.TokenizeTextOrCommand(ref scanner)
            : (scanner.Ch2, scanner.Ch3) switch
            {
                ('>', _) => TokenizerHelpers.Yield(ref scanner, 3, Token.AppendStdErrRedirection),
                ('&', '1') => TokenizerHelpers.Yield(ref scanner, 4, Token.StdErrToStdOutRedirection),
                _ => TokenizerHelpers.Yield(ref scanner, 2, Token.StdErrRedirection)
            };

    /// <summary>
    /// Digit 1 followed by > redirects stdout explicitly (usually redundant as > already does this).
    /// 1>&2 = redirect stdout to stderr. Standalone 1 is treated as text or command.
    /// </summary>
    public static IToken? TokenizeStdOutRedirection(ref Scanner scanner)
    {
        if (scanner.Ch1 != '>') return null;
        if (scanner.Ch2 == '&' && scanner.Ch3 == '2') return TokenizerHelpers.Yield(ref scanner, 4, Token.StdOutToStdErrRedirection);
        scanner.Advance();
        return Token.Text("1");
    }

    /// <summary>
    /// Single & separates commands unconditionally; && only runs second command if first succeeds.
    /// Both require a command to have been seen on the current line; otherwise it's an error.
    /// Cannot be used within IF condition expressions.
    /// </summary>
    public static IToken TokenizeAmpersand(ref Scanner scanner)
    {
        var currentContext = scanner.ContextStack.Count > 0 ? scanner.ContextStack.Peek() : BlockContext.None;
        var advance = scanner.Ch1 == '&' ? 2 : 1;

        if (!scanner.HasCommand || currentContext == BlockContext.If)
        {
            return TokenizerHelpers.Yield(ref scanner, advance, new ErrorToken("& was unexpected at this time."))!;
        }

        scanner.Expected = ExpectedTokenTypes.StartOfCommand;
        scanner.HasCommand = false;
        return TokenizerHelpers.Yield(ref scanner, advance, scanner.Ch1 == '&' ? Token.ConditionalAnd : Token.CommandSeparator)!;
    }

    /// <summary>
    /// Single | pipes output to next command; || only runs second command if first fails.
    /// Both require a command to have been seen; otherwise it's an error.
    /// Cannot be used within IF condition expressions.
    /// </summary>
    public static IToken TokenizePipe(ref Scanner scanner)
    {
        var currentContext = scanner.ContextStack.Count > 0 ? scanner.ContextStack.Peek() : BlockContext.None;
        var advance = scanner.Ch1 == '|' ? 2 : 1;

        if (!scanner.HasCommand || currentContext == BlockContext.If)
        {
            return TokenizerHelpers.Yield(ref scanner, advance, new ErrorToken("| was unexpected at this time."))!;
        }

        scanner.Expected = ExpectedTokenTypes.StartOfCommand;
        scanner.HasCommand = false;
        return TokenizerHelpers.Yield(ref scanner, advance, scanner.Ch1 == '|' ? Token.ConditionalOr : Token.Pipe)!;
    }

    /// <summary>
    /// Equals sign can be = or == (both are equivalent in batch IF comparisons).
    /// In IF contexts, becomes a comparison operator; elsewhere it's part of SET syntax.
    /// </summary>
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

    /// <summary>
    /// Reads symbolic comparison operators like >=, &lt;=, ==, !=.
    /// Only valid within IF condition contexts.
    /// </summary>
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
