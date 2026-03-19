namespace Bat.Tokens;

internal abstract class TokenBase(string raw) : IToken
{
    public string Raw => raw;
    public override string ToString() => raw;
}
