namespace Bat.Tokens;

internal class LabelToken(string raw) : TokenBase(raw)
{
    public string Value => field ??= ExtractValue(Raw);

    private static string ExtractValue(string raw) => raw[1..].TrimEnd();
}
