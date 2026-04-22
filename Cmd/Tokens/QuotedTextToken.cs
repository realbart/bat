namespace Bat.Tokens;

internal class QuotedTextToken(string raw) : TokenBase(raw)
{
    public string OpenQuote => field ??= Raw[0].ToString();
    public string Value => field ??= Raw[^1] == Raw[0] && Raw.Length > 1 ? Raw[1..^1] : Raw[1..];
    public string CloseQuote => field ??= Raw[^1] == Raw[0] ? Raw[^1].ToString() : "";
}
