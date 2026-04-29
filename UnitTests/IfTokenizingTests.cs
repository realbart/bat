#if WINDOWS
using Bat.Tokenizing;
using Bat.Tokens;

namespace Bat.UnitTests;

[TestClass]
public class IfTokenizingTests
{
    [TestMethod]
    public void If_StringEqual_ProducesCorrectTokens()
    {
        var tokens = new TokenSet();
        Tokenizer.Tokenize(tokens, "if \"hello\"==\"hello\" echo THEN");

        var tokenList = tokens.ToList();
        var tokenTypes = string.Join(", ", tokenList.Select(t => $"{t.GetType().Name}[{t.Raw}]"));

        Assert.Fail($"Tokens: {tokenTypes}");
    }
}
#endif
