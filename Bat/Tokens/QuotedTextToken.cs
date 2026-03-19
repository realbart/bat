namespace Bat.Tokens;

internal class QuotedTextToken(string openQuote, string value, string closeQuote) : TokenBase(openQuote + value + closeQuote)
{
    public string OpenQuote => openQuote;
    public string Value => value;
    public string CloseQuote => closeQuote;

}
