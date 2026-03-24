namespace Bat.Tokens;

internal class LabelToken(string raw) : TokenBase(raw)
{
    public string Value => field ??= Raw[1..].TrimEnd();
}
