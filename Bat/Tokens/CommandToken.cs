namespace Bat.Tokens;

internal class CommandToken : TextToken
{
    public CommandToken(string raw) : base(raw)
    {
    }

    // Legacy constructor for backward compatibility during migration
    internal CommandToken(string value, string raw) : base(value, raw)
    {
    }
}
