namespace Bat.Console
{
    internal interface ITokenizer
    {
        TokenSet Tokenize(TokenSet tokens, string input);
        TokenSet Tokenize(string input);
    }
}