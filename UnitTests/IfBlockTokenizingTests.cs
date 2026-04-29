#if WINDOWS
using Bat.Tokenizing;
using Bat.Tokens;

namespace Bat.UnitTests;

[TestClass]
public class IfBlockTokenizingTests
{
    [TestMethod]
    public void If_Defined_WithBlock_ProducesBlockTokens()
    {
        var tokens = new TokenSet();
        Tokenizer.Tokenize(tokens, "if defined VAR (echo THEN)");

        Assert.IsNull(tokens.ErrorMessage, $"Tokenizer error: {tokens.ErrorMessage}");

        var tokenList = tokens.ToList();
        var tokenTypes = string.Join(", ", tokenList.Select(t => $"{t.GetType().Name}[{t.Raw}]"));

        Assert.IsTrue(tokenList.Any(t => t is BlockStartToken), $"Should find BlockStartToken. Tokens: {tokenTypes}");
        Assert.IsTrue(tokenList.Any(t => t is BlockEndToken), $"Should find BlockEndToken. Tokens: {tokenTypes}");
    }
}
#endif
