using Microsoft.VisualStudio.TestTools.UnitTesting;
using Bat.Console;

namespace Bat.UnitTests;

[TestClass]
public class TokenizerBasicTests
{
    private ITokenizer _tokenizer = null!;

    [TestInitialize]
    public void Setup()
    {
        _tokenizer = new Tokenizer();
    }

    [TestMethod]
    public void EmptyString_ReturnsOnlyEndOfInput()
    {
        var result = _tokenizer.Tokenize("");
        
        Assert.HasCount(1, result);
        Assert.AreEqual(TokenType.EndOfInput, result[0].Type);
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void SimpleText_ReturnsTextToken()
    {
        var result = _tokenizer.Tokenize("hello");
        
        Assert.HasCount(2, result);
        Assert.AreEqual(TokenType.Text, result[0].Type);
        Assert.AreEqual("hello", result[0].Value);
        Assert.AreEqual(TokenType.EndOfInput, result[1].Type);
    }

    [TestMethod]
    public void TextWithSpaces_ReturnsCorrectTokens()
    {
        var result = _tokenizer.Tokenize("hello world");
        
        Assert.HasCount(4, result);
        Assert.AreEqual(TokenType.Text, result[0].Type);
        Assert.AreEqual("hello", result[0].Value);
        Assert.AreEqual(TokenType.Whitespace, result[1].Type);
        Assert.AreEqual(" ", result[1].Value);
        Assert.AreEqual(TokenType.Text, result[2].Type);
        Assert.AreEqual("world", result[2].Value);
    }
}

[TestClass]
public class ParenthesesTokenizerTests
{
    private ITokenizer _tokenizer = null!;

    [TestInitialize]
    public void Setup()
    {
        _tokenizer = new Tokenizer();
    }

    [TestMethod]
    public void SingleOpenParen_ReturnsOpenParenToken()
    {
        var result = _tokenizer.Tokenize("(");
        
        Assert.HasCount(2, result);
        Assert.AreEqual(TokenType.OpenParen, result[0].Type);
        Assert.AreEqual("(", result[0].Value);
    }

    [TestMethod]
    public void SingleCloseParen_ReturnsCloseParenToken()
    {
        var result = _tokenizer.Tokenize(")");
        
        Assert.HasCount(2, result);
        Assert.AreEqual(TokenType.CloseParen, result[0].Type);
        Assert.AreEqual(")", result[0].Value);
    }

    [TestMethod]
    public void BalancedParens_ReturnsCorrectTokens()
    {
        var result = _tokenizer.Tokenize("(echo hello)");
        
        var tokens = result.ToArray();
        Assert.AreEqual(TokenType.OpenParen, tokens[0].Type);
        Assert.AreEqual(TokenType.Text, tokens[1].Type);
        Assert.AreEqual("echo", tokens[1].Value);
        Assert.AreEqual(TokenType.Whitespace, tokens[2].Type);
        Assert.AreEqual(TokenType.Text, tokens[3].Type);
        Assert.AreEqual("hello", tokens[3].Value);
        Assert.AreEqual(TokenType.CloseParen, tokens[4].Type);
    }

    [TestMethod]
    public void NestedParens_ReturnsCorrectTokens()
    {
        var result = _tokenizer.Tokenize("((test))");

        var tokens = result.GetNonWhitespaceTokens().ToArray();
        Assert.HasCount(5, tokens); // ( ( test ) ) (no EndOfInput)
        Assert.AreEqual(TokenType.OpenParen, tokens[0].Type);
        Assert.AreEqual(TokenType.OpenParen, tokens[1].Type);
        Assert.AreEqual(TokenType.Text, tokens[2].Type);
        Assert.AreEqual(TokenType.CloseParen, tokens[3].Type);
        Assert.AreEqual(TokenType.CloseParen, tokens[4].Type);
    }
}

[TestClass]
public class QuotedStringTokenizerTests
{
    private ITokenizer _tokenizer = null!;

    [TestInitialize]
    public void Setup()
    {
        _tokenizer = new Tokenizer();
    }

    [TestMethod]
    public void DoubleQuotedString_ReturnsQuotedStringToken()
    {
        var result = _tokenizer.Tokenize("\"hello world\"");
        
        Assert.HasCount(2, result);
        Assert.AreEqual(TokenType.QuotedString, result[0].Type);
        Assert.AreEqual("hello world", result[0].Value);
    }

    [TestMethod]
    public void SingleQuotedString_ReturnsQuotedStringToken()
    {
        var result = _tokenizer.Tokenize("'hello world'");
        
        Assert.HasCount(2, result);
        Assert.AreEqual(TokenType.QuotedString, result[0].Type);
        Assert.AreEqual("hello world", result[0].Value);
    }

    [TestMethod]
    public void UnclosedQuotedString_ReturnsTextToken()
    {
        var result = _tokenizer.Tokenize("\"hello world");

        Assert.IsFalse(result.HasErrors); // No longer an error
        Assert.HasCount(2, result);
        Assert.AreEqual(TokenType.Text, result[0].Type); // Now Text, not Error
        Assert.AreEqual("\"hello world", result[0].Value); // Includes the quote
    }

    [TestMethod]
    public void EmptyQuotedString_ReturnsEmptyQuotedStringToken()
    {
        var result = _tokenizer.Tokenize("\"\"");
        
        Assert.HasCount(2, result);
        Assert.AreEqual(TokenType.QuotedString, result[0].Type);
        Assert.AreEqual("", result[0].Value);
    }

    [TestMethod]
    public void QuotedStringWithEscape_HandlesEscapeSequences()
    {
        var result = _tokenizer.Tokenize("\"hello^nworld\"");
        
        Assert.HasCount(2, result);
        Assert.AreEqual(TokenType.QuotedString, result[0].Type);
        Assert.AreEqual("hello\nworld", result[0].Value);
    }

    [TestMethod]
    public void QuotedStringWithEscapedQuote_HandlesEscapedQuote()
    {
        var result = _tokenizer.Tokenize("\"hello^\"world\"");
        
        Assert.HasCount(2, result);
        Assert.AreEqual(TokenType.QuotedString, result[0].Type);
        Assert.AreEqual("hello\"world", result[0].Value);
    }
}

[TestClass]
public class VariableTokenizerTests
{
    private ITokenizer _tokenizer = null!;

    [TestInitialize]
    public void Setup()
    {
        _tokenizer = new Tokenizer();
    }

    [TestMethod]
    public void SimpleVariable_ReturnsVariableToken()
    {
        var result = _tokenizer.Tokenize("%PATH%");
        
        Assert.HasCount(2, result);
        Assert.AreEqual(TokenType.Variable, result[0].Type);
        Assert.AreEqual("PATH", result[0].Value);
    }

    [TestMethod]
    public void EmptyVariable_ReturnsVariableToken()
    {
        var result = _tokenizer.Tokenize("%%");
        
        Assert.HasCount(2, result);
        Assert.AreEqual(TokenType.Variable, result[0].Type);
        Assert.AreEqual("", result[0].Value);
    }

    [TestMethod]
    public void UnclosedVariable_ReturnsTextToken()
    {
        var result = _tokenizer.Tokenize("%PATH");

        Assert.IsFalse(result.HasErrors); // No longer an error
        Assert.HasCount(2, result);
        Assert.AreEqual(TokenType.Text, result[0].Type); // Now Text, not Error
        Assert.AreEqual("%PATH", result[0].Value); // Includes the %
    }

    [TestMethod]
    public void VariableInText_ReturnsCorrectTokens()
    {
        var result = _tokenizer.Tokenize("echo %PATH% done");

        var nonWhitespace = result.GetNonWhitespaceTokens().ToArray();
        Assert.HasCount(3, nonWhitespace); // echo, %PATH%, done (no EndOfInput)
        Assert.AreEqual("echo", nonWhitespace[0].Value);
        Assert.AreEqual(TokenType.Variable, nonWhitespace[1].Type);
        Assert.AreEqual("PATH", nonWhitespace[1].Value);
        Assert.AreEqual("done", nonWhitespace[2].Value);
    }
}

[TestClass]
public class OperatorTokenizerTests
{
    private ITokenizer _tokenizer = null!;

    [TestInitialize]
    public void Setup()
    {
        _tokenizer = new Tokenizer();
    }

    [TestMethod]
    public void EqualsOperator_ReturnsOperatorToken()
    {
        var result = _tokenizer.Tokenize("==");
        
        Assert.HasCount(2, result);
        Assert.AreEqual(TokenType.Operator, result[0].Type);
        Assert.AreEqual("==", result[0].Value);
    }

    [TestMethod]
    public void NotEqualsOperator_ReturnsOperatorToken()
    {
        var result = _tokenizer.Tokenize("!=");
        
        Assert.HasCount(2, result);
        Assert.AreEqual(TokenType.Operator, result[0].Type);
        Assert.AreEqual("!=", result[0].Value);
    }

    [TestMethod]
    public void GreaterEqualsOperator_ReturnsOperatorToken()
    {
        var result = _tokenizer.Tokenize(">=");
        
        Assert.HasCount(2, result);
        Assert.AreEqual(TokenType.Operator, result[0].Type);
        Assert.AreEqual(">=", result[0].Value);
    }

    [TestMethod]
    public void LessEqualsOperator_ReturnsOperatorToken()
    {
        var result = _tokenizer.Tokenize("<=");
        
        Assert.HasCount(2, result);
        Assert.AreEqual(TokenType.Operator, result[0].Type);
        Assert.AreEqual("<=", result[0].Value);
    }

    [TestMethod]
    public void SingleGreaterThan_ReturnsGreaterThanToken()
    {
        var result = _tokenizer.Tokenize(">");
        
        Assert.HasCount(2, result);
        Assert.AreEqual(TokenType.GreaterThan, result[0].Type);
        Assert.AreEqual(">", result[0].Value);
    }

    [TestMethod]
    public void SingleLessThan_ReturnsLessThanToken()
    {
        var result = _tokenizer.Tokenize("<");
        
        Assert.HasCount(2, result);
        Assert.AreEqual(TokenType.LessThan, result[0].Type);
        Assert.AreEqual("<", result[0].Value);
    }

    [TestMethod]
    public void Redirection_ReturnsRedirectionToken()
    {
        var result = _tokenizer.Tokenize(">>");
        
        Assert.HasCount(2, result);
        Assert.AreEqual(TokenType.Redirection, result[0].Type);
        Assert.AreEqual(">>", result[0].Value);
    }

    [TestMethod]
    public void Pipe_ReturnsPipeToken()
    {
        var result = _tokenizer.Tokenize("|");
        
        Assert.HasCount(2, result);
        Assert.AreEqual(TokenType.Pipe, result[0].Type);
        Assert.AreEqual("|", result[0].Value);
    }
}

[TestClass]
public class EscapeSequenceTokenizerTests
{
    private ITokenizer _tokenizer = null!;

    [TestInitialize]
    public void Setup()
    {
        _tokenizer = new Tokenizer();
    }

    [TestMethod]
    public void EscapeCharacter_ReturnsTextToken()
    {
        var result = _tokenizer.Tokenize("^>");
        
        Assert.HasCount(2, result);
        Assert.AreEqual(TokenType.Text, result[0].Type);
        Assert.AreEqual(">", result[0].Value);
    }

    [TestMethod]
    public void EscapeAtEndOfInput_ReturnsLineContinuation()
    {
        var result = _tokenizer.Tokenize("test^");

        var nonWhitespace = result.GetNonWhitespaceTokens().ToArray();
        Assert.HasCount(2, nonWhitespace); // test, ^ (no EndOfInput in non-whitespace)
        Assert.AreEqual("test", nonWhitespace[0].Value);
        Assert.AreEqual(TokenType.LineContinuation, nonWhitespace[1].Type); // Now LineContinuation
        Assert.AreEqual("^", nonWhitespace[1].Value);
    }

    [TestMethod]
    public void EscapeSequenceInMiddle_ReturnsCorrectTokens()
    {
        var result = _tokenizer.Tokenize("hello^>world");

        var nonWhitespace = result.GetNonWhitespaceTokens().ToArray();
        Assert.HasCount(3, nonWhitespace); // hello, >, world (no EndOfInput)
        Assert.AreEqual("hello", nonWhitespace[0].Value);
        Assert.AreEqual(">", nonWhitespace[1].Value);
        Assert.AreEqual("world", nonWhitespace[2].Value);
    }
}

[TestClass]
public class ComplexBatchCommandTests
{
    private ITokenizer _tokenizer = null!;

    [TestInitialize]
    public void Setup()
    {
        _tokenizer = new Tokenizer();
    }

    [TestMethod]
    public void IfCommand_ReturnsCorrectTokens()
    {
        var result = _tokenizer.Tokenize("if %foo%==1 (echo test)");

        var nonWhitespace = result.GetNonWhitespaceTokens().ToArray();
        Assert.HasCount(8, nonWhitespace); // if, %foo%, ==, 1, (, echo, test, ) (no EndOfInput)
        Assert.AreEqual("if", nonWhitespace[0].Value);
        Assert.AreEqual(TokenType.Variable, nonWhitespace[1].Type);
        Assert.AreEqual("foo", nonWhitespace[1].Value);
        Assert.AreEqual(TokenType.Operator, nonWhitespace[2].Type);
        Assert.AreEqual("==", nonWhitespace[2].Value);
        Assert.AreEqual("1", nonWhitespace[3].Value);
        Assert.AreEqual(TokenType.OpenParen, nonWhitespace[4].Type);
        Assert.AreEqual("echo", nonWhitespace[5].Value);
        Assert.AreEqual("test", nonWhitespace[6].Value);
        Assert.AreEqual(TokenType.CloseParen, nonWhitespace[7].Type);
    }

    [TestMethod]
    public void ComplexCommandWithQuotesAndParens_ReturnsCorrectTokens()
    {
        var result = _tokenizer.Tokenize("if \"%foo%\"==\"1\" (echo \"test)\")");

        var nonWhitespace = result.GetNonWhitespaceTokens().ToArray();
        Assert.HasCount(8, nonWhitespace); // Updated count
        Assert.AreEqual("if", nonWhitespace[0].Value);
        Assert.AreEqual(TokenType.QuotedString, nonWhitespace[1].Type);
        Assert.AreEqual("%foo%", nonWhitespace[1].Value); // Variables inside quotes stay literal
        Assert.AreEqual(TokenType.Operator, nonWhitespace[2].Type);
        Assert.AreEqual("==", nonWhitespace[2].Value);
        Assert.AreEqual(TokenType.QuotedString, nonWhitespace[3].Type);
        Assert.AreEqual("1", nonWhitespace[3].Value);
        Assert.AreEqual(TokenType.OpenParen, nonWhitespace[4].Type);
        Assert.AreEqual("echo", nonWhitespace[5].Value);
        Assert.AreEqual(TokenType.QuotedString, nonWhitespace[6].Type);
        Assert.AreEqual("test)", nonWhitespace[6].Value); // Paren inside quote is literal
        Assert.AreEqual(TokenType.CloseParen, nonWhitespace[7].Type);
    }

    [TestMethod]
    public void CommandSeparator_ReturnsCorrectTokens()
    {
        var result = _tokenizer.Tokenize("echo hello & echo world");

        var nonWhitespace = result.GetNonWhitespaceTokens().ToArray();
        Assert.HasCount(5, nonWhitespace); // echo, hello, &, echo, world (no EndOfInput)
        Assert.AreEqual("echo", nonWhitespace[0].Value);
        Assert.AreEqual("hello", nonWhitespace[1].Value);
        Assert.AreEqual(TokenType.CommandSeparator, nonWhitespace[2].Type);
        Assert.AreEqual("&", nonWhitespace[2].Value);
        Assert.AreEqual("echo", nonWhitespace[3].Value);
        Assert.AreEqual("world", nonWhitespace[4].Value);
    }
}

[TestClass]
public class MultiLineTokenizerTests
{
    private ITokenizer _tokenizer = null!;

    [TestInitialize]
    public void Setup()
    {
        _tokenizer = new Tokenizer();
    }

    [TestMethod]
    public void MultiLineTokenize_CombinesTokensCorrectly()
    {
        var firstLine = _tokenizer.Tokenize("if %foo%==1 (");
        var result = _tokenizer.Tokenize(firstLine, "echo hello)");
        
        var nonWhitespace = result.GetNonWhitespaceTokens().ToArray();
        Assert.IsTrue(nonWhitespace.Any(t => t.Value == "if"));
        Assert.IsTrue(nonWhitespace.Any(t => t.Value == "echo"));
        Assert.IsTrue(nonWhitespace.Any(t => t.Value == "hello"));
    }

    [TestMethod]
    public void MultiLineTokenize_PreservesErrors()
    {
        var firstLine = _tokenizer.Tokenize("if \"unclosed");
        var result = _tokenizer.Tokenize(firstLine, "echo test");

        // Since unclosed quotes are no longer errors, this test should pass
        Assert.IsFalse(result.HasErrors); // No errors anymore

        var nonWhitespace = result.GetNonWhitespaceTokens().ToArray();
        Assert.IsTrue(nonWhitespace.Any(t => t.Value == "if"));
        Assert.IsTrue(nonWhitespace.Any(t => t.Value == "\"unclosed")); // Literal text token
        Assert.IsTrue(nonWhitespace.Any(t => t.Value == "echo"));
        Assert.IsTrue(nonWhitespace.Any(t => t.Value == "test"));
    }
}

[TestClass]
public class TokenSetExtensionTests
{
    private ITokenizer _tokenizer = null!;

    [TestInitialize]
    public void Setup()
    {
        _tokenizer = new Tokenizer();
    }

    [TestMethod]
    public void IsBalanced_WithUnclosedQuote_ReturnsFalse()
    {
        var result = _tokenizer.Tokenize("echo \"hello");

        // Since unclosed quotes are now literal text, this should be balanced
        try
        {
            var isBalanced = result.IsComplete;
            Assert.IsTrue(isBalanced); // Changed expectation
        }
        catch (Exception ex)
        {
            Assert.Fail($"IsComplete extension method has syntax error: {ex.Message}");
        }
    }

    [TestMethod]
    public void IsBalanced_WithUnclosedVariable_ReturnsFalse()
    {
        var result = _tokenizer.Tokenize("echo %PATH");

        // Since unclosed variables are now literal text, this should be balanced
        try
        {
            var isBalanced = result.IsComplete;
            Assert.IsTrue(isBalanced); // Changed expectation
        }
        catch (Exception ex)
        {
            Assert.Fail($"IsComplete extension method has syntax error: {ex.Message}");
        }
    }

    [TestMethod]
    public void IsBalanced_WithUnbalancedParens_ReturnsFalse()
    {
        var result = _tokenizer.Tokenize("if %foo%==1 (echo test");
        
        try
        {
            var isBalanced = result.IsComplete;
            Assert.IsFalse(isBalanced);
        }
        catch (Exception ex)
        {
            Assert.Fail($"IsComplete extension method has syntax error: {ex.Message}");
        }
    }

    [TestMethod]
    public void IsBalanced_WithBalancedParens_ReturnsTrue()
    {
        var result = _tokenizer.Tokenize("if %foo%==1 (echo test)");
        
        try
        {
            var isBalanced = result.IsComplete;
            Assert.IsTrue(isBalanced);
        }
        catch (Exception ex)
        {
            Assert.Fail($"IsComplete extension method has syntax error: {ex.Message}");
        }
    }

    [TestMethod]
    public void IsBalanced_WithLineContinuation_ReturnsFalse()
    {
        var result = _tokenizer.Tokenize("echo hello ^");

        try
        {
            var isBalanced = result.IsComplete;
            Assert.IsFalse(isBalanced); // Should still be false for LineContinuation
        }
        catch (Exception ex)
        {
            Assert.Fail($"IsComplete extension method has syntax error: {ex.Message}");
        }
    }

    [TestMethod]
    public void IsBalanced_WithCompleteCommand_ReturnsTrue()
    {
        var result = _tokenizer.Tokenize("echo hello world");
        
        try
        {
            var isBalanced = result.IsComplete;
            Assert.IsTrue(isBalanced);
        }
        catch (Exception ex)
        {
            Assert.Fail($"IsComplete extension method has syntax error: {ex.Message}");
        }
    }
}