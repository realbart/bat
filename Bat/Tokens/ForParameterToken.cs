namespace Bat.Tokens;

internal class ForParameterToken(string raw) : TokenBase(raw)
{
    private string? _cachedParameter;
    public string Parameter => _cachedParameter ??= raw[2..];
}
