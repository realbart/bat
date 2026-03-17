using System.Collections.ObjectModel;

namespace Bat.Console;

internal class TokenSet : ReadOnlyCollection<Token>
{
    public bool HasErrors { get; }
    public string[] Errors { get; }

    public TokenSet(List<Token> tokens, IEnumerable<string> errors) : base(tokens)
    {
        var tokenErrors = this.Where(t => t.Type == TokenType.Error).Select(t => t.ErrorMessage).OfType<string>();
        Errors = [.. errors, .. tokenErrors];
        HasErrors = Errors.Any();
    }

    public IEnumerable<Token> GetTokensOfType(TokenType type) => this.Where(t => t.Type == type);

    public IEnumerable<Token> GetNonWhitespaceTokens() => this.Where(t => t.Type != TokenType.Whitespace && t.Type != TokenType.NewLine && t.Type != TokenType.EndOfInput);

    public IReadOnlyCollection<Token> this[Range range] => ((List<Token>)Items)[range];
}