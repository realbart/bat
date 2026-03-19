namespace Bat.Tokens;

internal class TextToken(string value, string raw) : TokenBase(raw)
{
    public string Value => value;
}
