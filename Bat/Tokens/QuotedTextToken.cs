namespace Bat.Tokens;

internal class QuotedTextToken(string raw) : TokenBase(raw)
{
    public string OpenQuote => field ??= Raw[0].ToString();
    public string Value => field ??= ExtractValue(Raw);
    public string CloseQuote => field ??= (Raw[^1] == Raw[0] ? Raw[^1].ToString() : "");

    private static string ExtractValue(string raw) => raw[^1] == raw[0] && raw.Length > 1 ? raw[1..^1] : raw[1..];
}
