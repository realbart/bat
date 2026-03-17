namespace Bat.Console;

internal record Token(TokenType Type, string Value, int Position, int Length)
{
    public static Token Text(string value, int position) => new(TokenType.Text, value, position, value.Length);
    public static Token QuotedString(string value, int position, int length) => new(TokenType.QuotedString, value, position, length);
    public static Token OpenParen(int position) => new(TokenType.OpenParen, "(", position, 1);
    public static Token CloseParen(int position) => new(TokenType.CloseParen, ")", position, 1);
    public static Token Variable(string value, int position, int length) => new(TokenType.Variable, value, position, length);
    public static Token Whitespace(string value, int position) => new(TokenType.Whitespace, value, position, value.Length);
    public static Token NewLine(int position) => new(TokenType.NewLine, Environment.NewLine, position, Environment.NewLine.Length);
    public static Token CommandSeparator(int position) => new(TokenType.CommandSeparator, "&", position, 1);
    public static Token Operator(string value, int position) => new(TokenType.Operator, value, position, value.Length);
    public static Token GreaterThan(int position) => new(TokenType.GreaterThan, ">", position, 1);
    public static Token LessThan(int position) => new(TokenType.LessThan, "<", position, 1);
    public static Token Pipe(int position) => new(TokenType.Pipe, "|", position, 1);
    public static Token Redirection(string value, int position) => new(TokenType.Redirection, value, position, value.Length);
    public static Token EndOfInput(int position) => new(TokenType.EndOfInput, string.Empty, position, 0);
    public static Token Error(string value, int position, string message) => new(TokenType.Error, value, position, value.Length) { ErrorMessage = message };
    
    public string? ErrorMessage { get; init; }
}