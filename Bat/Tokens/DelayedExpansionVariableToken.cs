namespace Bat.Tokens;

internal class DelayedExpansionVariableToken(string raw) : TokenBase(raw)
{
    private string? _cachedName;
    public string Name => _cachedName ??= ExtractAndUnescapeName(Raw);

    private static string ExtractAndUnescapeName(string raw)
    {
        var endIndex = raw.LastIndexOf('!');
        var content = endIndex <= 0 ? raw[1..] : raw[1..endIndex];
        return UnescapeUtility.Unescape(content);
    }
}
