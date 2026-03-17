using Microsoft.VisualStudio.TestTools.UnitTesting;
using Bat.Console;

namespace Bat.UnitTests;

[TestClass]
public class TokenSetExtensionsSyntaxTests
{
    private ITokenizer _tokenizer = null!;

    [TestInitialize]
    public void Setup()
    {
        _tokenizer = new Tokenizer();
    }

    [TestMethod]
    public void ExtensionMethod_CompilationTest()
    {
        // This test will fail to compile if TokenSetExtensions has syntax errors
        var result = _tokenizer.Tokenize("(incomplete");
        
        // If this compiles and runs, the extension method syntax is correct
        var isBalanced = result.IsComplete;
        
        Assert.IsFalse(isBalanced); // Should be false due to unmatched paren
    }

    [TestMethod]
    public void ExtensionMethod_WithValidSyntax_Works()
    {
        var result = _tokenizer.Tokenize("echo hello");
        
        // Test that the extension method is accessible and works
        Assert.IsTrue(result.IsComplete);
    }

    [TestMethod]
    public void ExtensionMethod_RevealsIfTokenSetIsWrongType()
    {
        // This will show if TokenSet doesn't implement the expected interface
        var result = _tokenizer.Tokenize("test");
        
        // These method calls will fail if TokenSet doesn't have the right interface
        var nonWhitespace = result.GetNonWhitespaceTokens();
        var textTokens = result.Where(t=>t.Type == TokenType.Text);
        var errors = result.Errors;
        
        Assert.IsNotNull(nonWhitespace);
        Assert.IsNotNull(textTokens);
        Assert.IsNotNull(errors);
    }
}