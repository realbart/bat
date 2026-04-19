using Bat.Tokens;

namespace Bat.Tokenizing;

/// <summary>
/// Main tokenizer coordinator. Dispatches character-by-character to specialized tokenizers
/// and maintains the main tokenization loop.
/// </summary>
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

    /// <summary>
    /// Main tokenization loop. Uses a switch expression to dispatch each character
    /// to the appropriate specialized tokenizer based on batch syntax rules.
    /// </summary>
    private static void TokenizeLine(ref Scanner scanner, TokenSet tokenSet)
    {
        while (!scanner.IsAtEnd)
        {
            if (tokenSet.ErrorMessage != null) return;

            var token = scanner.Ch0 switch
            {
                '\r' or '\n' => LiteralTokenizer.TokenizeLineEnd(ref scanner, tokenSet),
                ' ' or '\t' => LiteralTokenizer.TokenizeWhitespace(ref scanner),
                '@' when (TokenizerHelpers.IsAtStartOfLine(tokenSet) || TokenizerHelpers.IsExpectingCommand(ref scanner)) => TokenizerHelpers.Yield(ref scanner, 1, Token.EchoSupressor),
                ':' when (TokenizerHelpers.IsAtStartOfLine(tokenSet) || TokenizerHelpers.IsExpectingCommand(ref scanner)) => LiteralTokenizer.TokenizeLabel(ref scanner),
                '^' => LiteralTokenizer.TokenizeEscape(ref scanner),
                '"' or '\'' => LiteralTokenizer.TokenizeQuotedString(ref scanner, scanner.Ch0),
                '%' => LiteralTokenizer.TokenizeVariable(ref scanner),
                '!' => LiteralTokenizer.TokenizeDelayedExpansion(ref scanner),
                '(' => OperatorTokenizer.TokenizeBlockStart(ref scanner),
                ')' => OperatorTokenizer.TokenizeBlockEnd(ref scanner),
                '>' => OperatorTokenizer.TokenizeGreaterThan(ref scanner),
                '<' => TokenizerHelpers.Yield(ref scanner, 1, Token.InputRedirection),
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
}

