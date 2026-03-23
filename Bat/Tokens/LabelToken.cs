namespace Bat.Tokens;

internal class LabelToken : TokenBase
{
    private string? _cachedValue;

    public LabelToken(string raw) : base(":" + raw)
    {
    }

    // Legacy constructor for backward compatibility during migration
    internal LabelToken(string value, string raw) : base(":" + raw)
    {
        _cachedValue = value;
    }

    public string Value => _cachedValue ??= UnescapeLabel(Raw);

    private static string UnescapeLabel(string raw)
    {
        // Remove leading ":" and trim trailing whitespace
        if (raw.StartsWith(':'))
        {
            return raw[1..].TrimEnd();
        }
        return raw.TrimEnd();
    }
}
