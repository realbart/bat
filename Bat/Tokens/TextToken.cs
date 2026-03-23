namespace Bat.Tokens;

internal class TextToken(string raw) : TokenBase(raw)
{
    private string? _cachedValue;
    public string Value => _cachedValue ??= UnescapeUtility.Unescape(Raw);
}
