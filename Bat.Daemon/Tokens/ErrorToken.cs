namespace Bat.Tokens;

internal class ErrorToken(string message) : TokenBase(message)
{
    public string Message { get; } = message;
}
