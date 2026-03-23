using System.Text;

namespace Bat.Tokens;

internal class DelayedExpansionVariableToken : TokenBase
{
    private string? _cachedName;

    public DelayedExpansionVariableToken(string raw) : base(raw)
    {
    }

    // Legacy constructor for backward compatibility during migration
    internal DelayedExpansionVariableToken(string name, string raw) : base(raw)
    {
        _cachedName = name;
    }

    public string Name => _cachedName ??= ExtractAndUnescapeName(Raw);

    private static string ExtractAndUnescapeName(string raw)
    {
        // Raw format: "!name!" or "!escaped^^name!"
        if (raw.Length < 2 || !raw.StartsWith('!'))
            return raw;

        var endIndex = raw.LastIndexOf('!');
        if (endIndex <= 0)
        {
            // Unclosed - return everything after first !
            return UnescapeDelayedExpansion(raw[1..]);
        }

        // Extract content between ! and !
        var content = raw[1..endIndex];
        return UnescapeDelayedExpansion(content);
    }

    private static string UnescapeDelayedExpansion(string content)
    {
        // Within delayed expansion, ^^ becomes ^
        if (content.IndexOf('^') == -1)
            return content;

        var sb = new StringBuilder(content.Length);
        for (int i = 0; i < content.Length; i++)
        {
            if (content[i] == '^' && i + 1 < content.Length)
            {
                // Skip the caret, take the next character
                i++;
            }
            sb.Append(content[i]);
        }
        return sb.ToString();
    }
}
