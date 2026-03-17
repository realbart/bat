using System.Collections.ObjectModel;

namespace Bat.Console;

internal class TokenSet : ReadOnlyCollection<Token>
{
    public readonly bool HasErrors;
    public readonly IReadOnlyCollection<string> Errors;
    public bool HasLineContinuation;
    public int BlockNestingLevel;
    public bool IsComplete => BlockNestingLevel == 0 && !HasLineContinuation;

    public TokenSet(List<Token> tokens, int blockNestingLevel) : base(tokens)
    {
        Errors = this.Where(t => t.Type == TokenType.Error).Select(t => t.ErrorMessage).OfType<string>().ToArray();
        HasErrors = Errors.Count > 0;
        HasLineContinuation = tokens.Count > 1 && tokens[^2].Type == TokenType.LineContinuation;
        BlockNestingLevel = blockNestingLevel;
    }

    public IEnumerable<Token> GetTokensOfType(TokenType type) => this.Where(t => t.Type == type);

    public IEnumerable<Token> GetNonWhitespaceTokens() => this.Where(t => t.Type != TokenType.Whitespace && t.Type != TokenType.NewLine && t.Type != TokenType.EndOfInput);

    public IReadOnlyCollection<Token> this[Range range] => ((List<Token>)Items)[range];
}