namespace Bat.Tokens;

internal class QuotedTextToken(string raw) : TokenBase(raw)
{
    private string? _cachedOpenQuote;
    private string? _cachedValue;
    private string? _cachedCloseQuote;

    public string OpenQuote => _cachedOpenQuote ??= raw[0].ToString();
    public string Value => _cachedValue ??= ExtractValue(Raw);
    public string CloseQuote => _cachedCloseQuote ??= (raw[^1] == raw[0] ? raw[^1].ToString() : "");

    private static string ExtractValue(string raw)
    {
        return raw[^1] == raw[0] && raw.Length > 1 ? raw[1..^1] : raw[1..];
    }
}
