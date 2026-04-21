using Bat.Console;
using Bat.Tokens;

namespace Bat.Tokenizing;

internal class TokenSet() : List<IToken>
{
    public Stack<BlockContext> ContextStack { get; } = new();
    public string? ErrorMessage { get; set; } 
}