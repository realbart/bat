namespace Bat.Tokens;

internal class QuotedTextToken : TokenBase
{
    private string? _cachedOpenQuote;
    private string? _cachedValue;
    private string? _cachedCloseQuote;

    public QuotedTextToken(string raw) : base(raw)
    {
    }

    // Legacy constructor for backward compatibility during migration
    internal QuotedTextToken(string openQuote, string value, string closeQuote) 
        : base(openQuote + value + closeQuote)
    {
        _cachedOpenQuote = openQuote;
        _cachedValue = value;
        _cachedCloseQuote = closeQuote;
    }

    public string OpenQuote => _cachedOpenQuote ??= ExtractOpenQuote(Raw);
    public string Value => _cachedValue ??= ExtractValue(Raw);
    public string CloseQuote => _cachedCloseQuote ??= ExtractCloseQuote(Raw);

    private static string ExtractOpenQuote(string raw)
    {
        if (raw.Length == 0) return "";
        var firstChar = raw[0];
        if (firstChar == '"' || firstChar == '\'')
            return firstChar.ToString();
        return "";
    }

    private static string ExtractValue(string raw)
    {
        if (raw.Length == 0) return "";

        var firstChar = raw[0];
        if (firstChar != '"' && firstChar != '\'')
            return raw;

        // Find matching closing quote
        var lastChar = raw[^1];
        if (lastChar == firstChar && raw.Length > 1)
        {
            // Has both opening and closing quote
            return raw[1..^1];
        }
        else
        {
            // Missing closing quote
            return raw[1..];
        }
    }

    private static string ExtractCloseQuote(string raw)
    {
        if (raw.Length < 2) return "";

        var firstChar = raw[0];
        var lastChar = raw[^1];

        if ((firstChar == '"' || firstChar == '\'') && lastChar == firstChar)
            return lastChar.ToString();

        return "";
    }
}
