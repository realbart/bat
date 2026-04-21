namespace Bat.Tokens;

internal class TextToken(string raw) : TokenBase(raw)
{
    public string Value => field ??= UnescapeUtility.Unescape(Raw);
}
