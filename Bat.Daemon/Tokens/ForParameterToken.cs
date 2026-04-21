namespace Bat.Tokens;

internal class ForParameterToken(string raw) : TokenBase(raw)
{
    public string Parameter => field ??= Raw[2..];
}
