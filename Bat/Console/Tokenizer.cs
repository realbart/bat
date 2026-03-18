using Bat.Commands;
using Context;
using System.Text;

namespace Bat.Console;

internal class Tokenizer(IContext context, string input, string eol = "", TokenSet? command = null)
{
    public static TokenSet Tokenize(IContext context, string input, TokenSet? command = null) => new Tokenizer(context, input, command: command).Tokenize();
    public static TokenSet Tokenize(IContext context, string input, string eol, TokenSet? command = null) => new Tokenizer(context, input, eol, command).Tokenize();

    private int _position = 0;
    private readonly List<IToken> line = [];

    public TokenSet Tokenize()
    {
        return new TokenSet(new Line(line, Token.EndOfLine(eol)), command);
    }
}

