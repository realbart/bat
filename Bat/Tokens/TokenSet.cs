using Bat.Console;

namespace Bat.Tokens;

internal class TokenSet() : List<IToken>
{
    public Stack<BlockContext> ContextStack { get; } = new();
    public string? ErrorMessage { get; set; } 
}
