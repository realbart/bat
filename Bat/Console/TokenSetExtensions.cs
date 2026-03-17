namespace Bat.Console;

internal static class TokenSetExtensions
{
    extension(TokenSet tokens)
    {
        internal bool IsBalanced()
        {
            if (tokens.Errors.Any(e => e.Contains("Unclosed quoted string") || e.Contains("Unclosed variable reference"))) return false;

            var nonWhitespace = tokens.GetNonWhitespaceTokens().ToArray();

            if (nonWhitespace.Length > 0)
            {
                var lastToken = nonWhitespace[^1];
                if (lastToken.Type == TokenType.Text && lastToken.Value == "^") return false;
            }

            var openParens = nonWhitespace.Sum(
                t => t.Type switch
                {
                    TokenType.OpenParen => 1,
                    TokenType.CloseParen => -1,
                    _ => 0
                }
            );
            return openParens == 0;
        }
    }
}
