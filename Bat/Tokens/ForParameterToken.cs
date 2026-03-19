namespace Bat.Tokens;

internal class ForParameterToken(string parameter, string raw) : TokenBase(raw)
{
    public string Parameter => parameter; // e.g., "i" from "%%i"
}