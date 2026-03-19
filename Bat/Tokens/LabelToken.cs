namespace Bat.Tokens;

internal class LabelToken(string value, string raw) : TokenBase(":" + raw)
{
    public string Value => value;
}
