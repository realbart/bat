namespace Bat.Tokens;

internal class LabelToken(string raw) : TokenBase(raw)
{
    private string? _cachedValue;
    public string Value => _cachedValue ??= ExtractValue(Raw);

    private static string ExtractValue(string raw) => raw[1..].TrimEnd();
}
