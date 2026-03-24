using Bat.Console;
using Bat.Tokens;

namespace Bat.Tokenizing;

internal static class Tokenizer
{
    internal static void Tokenize(TokenSet tokenSet, string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            tokenSet.Add(Token.EndOfLine());
            return;
        }

        var scanner = new Scanner(input, tokenSet.ContextStack);
        TokenizeLine(ref scanner, tokenSet);

        if (tokenSet.Count == 0 || tokenSet[^1] is not EndOfLineToken)
        {
            tokenSet.Add(Token.EndOfLine());
        }
    }

    private static void TokenizeLine(ref Scanner scanner, TokenSet tokenSet)
    {
        while (!scanner.IsAtEnd)
        {
            if (tokenSet.ErrorMessage != null) return;

            var token = scanner.Ch0 switch
            {
                '\r' or '\n' => LiteralTokenizer.TokenizeLineEnd(ref scanner, tokenSet),
                ' ' or '\t' => LiteralTokenizer.TokenizeWhitespace(ref scanner),
                '@' when (IsAtStartOfLine(tokenSet) || IsExpectingCommand(ref scanner)) => Yield(ref scanner, 1, Token.EchoSupressor),
                ':' when (IsAtStartOfLine(tokenSet) || IsExpectingCommand(ref scanner)) => LiteralTokenizer.TokenizeLabel(ref scanner),
                '^' => LiteralTokenizer.TokenizeEscape(ref scanner),
                '"' or '\'' => LiteralTokenizer.TokenizeQuotedString(ref scanner, scanner.Ch0),
                '%' => LiteralTokenizer.TokenizeVariable(ref scanner),
                '!' => LiteralTokenizer.TokenizeDelayedExpansion(ref scanner),
                '(' => OperatorTokenizer.TokenizeBlockStart(ref scanner),
                ')' => OperatorTokenizer.TokenizeBlockEnd(ref scanner),
                '>' => OperatorTokenizer.TokenizeGreaterThan(ref scanner),
                '<' => Yield(ref scanner, 1, Token.InputRedirection),
                '2' => OperatorTokenizer.TokenizeStdErrRedirection(ref scanner),
                '1' => OperatorTokenizer.TokenizeStdOutRedirection(ref scanner) ?? CommandTokenizer.TokenizeTextOrCommand(ref scanner),
                '&' => OperatorTokenizer.TokenizeAmpersand(ref scanner),
                '|' => OperatorTokenizer.TokenizePipe(ref scanner),
                '=' => OperatorTokenizer.TokenizeEquals(ref scanner),
                _ => CommandTokenizer.TokenizeTextOrCommand(ref scanner)
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

    private static bool IsAtStartOfLine(TokenSet tokenSet)
        => tokenSet.Count == 0 || tokenSet[^1] is EndOfLineToken;

    public static bool IsExpectingCommand(ref Scanner scanner)
        => scanner.Expected.HasFlag(ExpectedTokenTypes.Command) && !scanner.HasCommand;

    public static bool IsInIfCondition(ref Scanner scanner)
    {
        if (scanner.ContextStack.Count == 0) return false;
        var ctx = scanner.ContextStack.Peek();
        return ctx == BlockContext.If || ctx == BlockContext.IfBlock;
    }

    public static IToken? Yield(ref Scanner scanner, int advance, IToken token)
    {
        scanner.Advance(advance);
        return token;
    }
}

