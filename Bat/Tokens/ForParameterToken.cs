namespace Bat.Tokens;

internal class ForParameterToken : TokenBase
{
    private string? _cachedParameter;

    public ForParameterToken(string raw) : base(raw)
    {
    }

    // Legacy constructor for backward compatibility during migration
    internal ForParameterToken(string parameter, string raw) : base(raw)
    {
        _cachedParameter = parameter;
    }

    public string Parameter => _cachedParameter ??= ExtractParameter(Raw);

    private static string ExtractParameter(string raw)
    {
        // Raw format: "%%i" -> extract "i"
        if (raw.StartsWith("%%") && raw.Length > 2)
            return raw[2..];

        // Fallback
        return raw;
    }
}