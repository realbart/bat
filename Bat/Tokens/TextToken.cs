using System.Text;

namespace Bat.Tokens;

internal class TextToken : TokenBase
{
    private string? _cachedValue;

    public TextToken(string raw) : base(raw)
    {
    }

    // Legacy constructor for backward compatibility during migration
    internal TextToken(string value, string raw) : base(raw)
    {
        _cachedValue = value;
    }

    public string Value => _cachedValue ??= Unescape(Raw);

    private static string Unescape(string raw)
    {
        // Fast path: no escape sequences
        if (!raw.Contains('^')) return raw;

        var sb = new StringBuilder(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '^' && i + 1 < raw.Length)
            {
                // Skip the caret, take the next character
                i++;
            }
            sb.Append(raw[i]);
        }
        return sb.ToString();
    }
}
