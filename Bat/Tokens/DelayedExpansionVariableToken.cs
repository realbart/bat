namespace Bat.Tokens;

internal class DelayedExpansionVariableToken(string name, string raw) : TokenBase(raw)
{
    public string Name => name;
}
