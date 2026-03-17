using Microsoft.VisualStudio.TestTools.UnitTesting;
using Bat.Console;

namespace Bat.UnitTests;

[TestClass]
public class TokenizerTests
{
    private ITokenizer _tokenizer = null!;

    [TestInitialize]
    public void Setup()
    {
        _tokenizer = new Tokenizer();
    }

    [TestClass]
    public class BasicTokenization : TokenizerTests
    {
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
            Assert.AreEqual(TokenType.EndOfInput, result[3].Type);
        }
    }

    [TestClass]
    public class ParenthesesTokenization : TokenizerTests
    {
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
            Assert.HasCount(5, tokens); // ( ( test ) ) + EndOfInput
            Assert.AreEqual(TokenType.OpenParen, tokens[0].Type);
            Assert.AreEqual(TokenType.OpenParen, tokens[1].Type);
            Assert.AreEqual(TokenType.Text, tokens[2].Type);
            Assert.AreEqual(TokenType.CloseParen, tokens[3].Type);
            Assert.AreEqual(TokenType.CloseParen, tokens[4].Type);
        }

        [TestMethod]
        public void UnbalancedParens_DetectedByIsBalanced()
        {
            var result = _tokenizer.Tokenize("(echo hello");
            
            // This should NOT be balanced - missing closing paren
            Assert.IsFalse(result.IsBalanced());
        }

        [TestMethod]
        public void TooManyCloseParens_DetectedByIsBalanced()
        {
            var result = _tokenizer.Tokenize("echo hello)");
            
            // This should be balanced (excess close parens are allowed)
            Assert.IsTrue(result.IsBalanced());
        }
    }

    [TestClass]
    public class QuotedStringTokenization : TokenizerTests
    {
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
        public void UnclosedQuotedString_ReturnsErrorTokenAndNotBalanced()
        {
            var result = _tokenizer.Tokenize("\"hello world");
            
            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Unclosed quoted string")));
            Assert.IsFalse(result.IsBalanced()); // Should not be balanced
            
            var errorToken = result.First(t => t.Type == TokenType.Error);
            Assert.AreEqual("hello world", errorToken.Value);
        }

        [TestMethod]
        public void EmptyQuotedString_ReturnsEmptyQuotedStringToken()
        {
            var result = _tokenizer.Tokenize("\"\"");
            
            Assert.HasCount(2, result);
            Assert.AreEqual(TokenType.QuotedString, result[0].Type);
            Assert.AreEqual("", result[0].Value);
            Assert.IsTrue(result.IsBalanced());
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

        [TestMethod]
        public void QuotedStringWithParentheses_DoesNotAffectBalance()
        {
            var result = _tokenizer.Tokenize("\"test (echo)\"");
            
            Assert.AreEqual(TokenType.QuotedString, result[0].Type);
            Assert.AreEqual("test (echo)", result[0].Value);
            Assert.IsTrue(result.IsBalanced()); // Parens in quotes don't affect balance
        }
    }

    [TestClass]
    public class VariableTokenization : TokenizerTests
    {
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
        public void UnclosedVariable_ReturnsErrorTokenAndNotBalanced()
        {
            var result = _tokenizer.Tokenize("%PATH");
            
            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Unclosed variable reference")));
            Assert.IsFalse(result.IsBalanced()); // Should not be balanced
        }

        [TestMethod]
        public void VariableInText_ReturnsCorrectTokens()
        {
            var result = _tokenizer.Tokenize("echo %PATH% done");
            
            var nonWhitespace = result.GetNonWhitespaceTokens().ToArray();
            Assert.HasCount(4, nonWhitespace); // echo, %PATH%, done, EndOfInput
            Assert.AreEqual("echo", nonWhitespace[0].Value);
            Assert.AreEqual(TokenType.Variable, nonWhitespace[1].Type);
            Assert.AreEqual("PATH", nonWhitespace[1].Value);
            Assert.AreEqual("done", nonWhitespace[2].Value);
        }
    }

    [TestClass]
    public class OperatorTokenization : TokenizerTests
    {
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

        [TestMethod]
        public void SingleEquals_ReturnsOperatorToken()
        {
            var result = _tokenizer.Tokenize("=");
            
            Assert.HasCount(2, result);
            Assert.AreEqual(TokenType.Operator, result[0].Type);
            Assert.AreEqual("=", result[0].Value);
        }
    }

    [TestClass]
    public class EscapeSequenceTokenization : TokenizerTests
    {
        [TestMethod]
        public void EscapeCharacter_ReturnsTextToken()
        {
            var result = _tokenizer.Tokenize("^>");
            
            Assert.HasCount(2, result);
            Assert.AreEqual(TokenType.Text, result[0].Type);
            Assert.AreEqual(">", result[0].Value);
        }

        [TestMethod]
        public void EscapeAtEndOfInput_ReturnsCaretAsTextAndNotBalanced()
        {
            var result = _tokenizer.Tokenize("test^");
            
            var nonWhitespace = result.GetNonWhitespaceTokens().ToArray();
            Assert.HasCount(3, nonWhitespace); // test, ^, EndOfInput
            Assert.AreEqual("test", nonWhitespace[0].Value);
            Assert.AreEqual("^", nonWhitespace[1].Value);
            
            // Line ending with ^ should not be balanced (continuation line)
            Assert.IsFalse(result.IsBalanced());
        }

        [TestMethod]
        public void EscapeSequenceInMiddle_ReturnsCorrectTokens()
        {
            var result = _tokenizer.Tokenize("hello^>world");
            
            var nonWhitespace = result.GetNonWhitespaceTokens().ToArray();
            Assert.HasCount(4, nonWhitespace); // hello, >, world, EndOfInput
            Assert.AreEqual("hello", nonWhitespace[0].Value);
            Assert.AreEqual(">", nonWhitespace[1].Value);
            Assert.AreEqual("world", nonWhitespace[2].Value);
        }

        [TestMethod]
        public void EscapedCaret_ReturnsCaretText()
        {
            var result = _tokenizer.Tokenize("^^");
            
            Assert.HasCount(2, result);
            Assert.AreEqual(TokenType.Text, result[0].Type);
            Assert.AreEqual("^", result[0].Value);
        }
    }

    [TestClass]
    public class ComplexBatchCommands : TokenizerTests
    {
        [TestMethod]
        public void IfCommand_ReturnsCorrectTokens()
        {
            var result = _tokenizer.Tokenize("if %foo%==1 (echo test)");
            
            var nonWhitespace = result.GetNonWhitespaceTokens().ToArray();
            Assert.HasCount(9, nonWhitespace); // if, %foo%, ==, 1, (, echo, test, ), EndOfInput
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
            Assert.IsTrue(result.IsBalanced());
        }

        [TestMethod]
        public void ComplexCommandWithQuotesAndParens_ReturnsCorrectTokens()
        {
            var result = _tokenizer.Tokenize("if \"%foo%\"==\"1\" (echo \"test)\")");
            
            var nonWhitespace = result.GetNonWhitespaceTokens().ToArray();
            Assert.HasCount(9, nonWhitespace);
            Assert.AreEqual("if", nonWhitespace[0].Value);
            Assert.AreEqual(TokenType.QuotedString, nonWhitespace[1].Type);
            Assert.AreEqual("%foo%", nonWhitespace[1].Value); // Variable inside quotes is literal
            Assert.AreEqual(TokenType.Operator, nonWhitespace[2].Type);
            Assert.AreEqual("==", nonWhitespace[2].Value);
            Assert.AreEqual(TokenType.QuotedString, nonWhitespace[3].Type);
            Assert.AreEqual("1", nonWhitespace[3].Value);
            Assert.AreEqual(TokenType.OpenParen, nonWhitespace[4].Type);
            Assert.AreEqual("echo", nonWhitespace[5].Value);
            Assert.AreEqual(TokenType.QuotedString, nonWhitespace[6].Type);
            Assert.AreEqual("test)", nonWhitespace[6].Value); // Paren inside quote is literal
            Assert.AreEqual(TokenType.CloseParen, nonWhitespace[7].Type);
            Assert.IsTrue(result.IsBalanced());
        }

        [TestMethod]
        public void NestedIfWithElse_ReturnsCorrectTokens()
        {
            var result = _tokenizer.Tokenize("if %foo%==1 ( echo ) ) else ( dir ) )");
            
            var nonWhitespace = result.GetNonWhitespaceTokens().ToArray();
            // This tests the exact scenario from the original request
            Assert.IsTrue(nonWhitespace.Any(t => t.Value == "if"));
            Assert.IsTrue(nonWhitespace.Any(t => t.Value == "else"));
            Assert.IsTrue(nonWhitespace.Any(t => t.Value == "echo"));
            Assert.IsTrue(nonWhitespace.Any(t => t.Value == "dir"));
            
            // Count parens
            var openParens = nonWhitespace.Count(t => t.Type == TokenType.OpenParen);
            var closeParens = nonWhitespace.Count(t => t.Type == TokenType.CloseParen);
            Assert.AreEqual(2, openParens);
            Assert.AreEqual(4, closeParens); // More close than open - should be balanced
            Assert.IsTrue(result.IsBalanced());
        }

        [TestMethod]
        public void UnfinishedIfCommand_NotBalanced()
        {
            var result = _tokenizer.Tokenize("if %foo%==1 (");
            
            Assert.IsFalse(result.IsBalanced()); // Missing close paren
        }

        [TestMethod]
        public void CommandSeparator_ReturnsCorrectTokens()
        {
            var result = _tokenizer.Tokenize("echo hello & echo world");
            
            var nonWhitespace = result.GetNonWhitespaceTokens().ToArray();
            Assert.IsTrue(nonWhitespace.Any(t => t.Type == TokenType.CommandSeparator));
            Assert.AreEqual("&", nonWhitespace.First(t => t.Type == TokenType.CommandSeparator).Value);
        }

        [TestMethod]
        public void PipeCommand_ReturnsCorrectTokens()
        {
            var result = _tokenizer.Tokenize("dir | find \"txt\"");
            
            var nonWhitespace = result.GetNonWhitespaceTokens().ToArray();
            Assert.IsTrue(nonWhitespace.Any(t => t.Type == TokenType.Pipe));
            Assert.AreEqual("|", nonWhitespace.First(t => t.Type == TokenType.Pipe).Value);
        }

        [TestMethod]
        public void RedirectionCommand_ReturnsCorrectTokens()
        {
            var result = _tokenizer.Tokenize("echo hello > output.txt");
            
            var nonWhitespace = result.GetNonWhitespaceTokens().ToArray();
            Assert.IsTrue(nonWhitespace.Any(t => t.Type == TokenType.GreaterThan));
        }
    }

    [TestClass]
    public class MultilineTokenization : TokenizerTests
    {
        [TestMethod]
        public void MultilineWithNewlines_ReturnsNewLineTokens()
        {
            var result = _tokenizer.Tokenize("echo hello\r\necho world");
            
            var tokens = result.ToArray();
            Assert.IsTrue(tokens.Any(t => t.Type == TokenType.NewLine));
        }

        [TestMethod]
        public void ContinueFromPreviousTokens_PreservesTokens()
        {
            var firstResult = _tokenizer.Tokenize("if %foo%==1 (");
            var secondResult = _tokenizer.Tokenize(firstResult, "echo test)");
            
            // Should contain tokens from both lines
            var allTokens = secondResult.GetNonWhitespaceTokens().ToArray();
            Assert.IsTrue(allTokens.Any(t => t.Value == "if"));
            Assert.IsTrue(allTokens.Any(t => t.Value == "echo"));
            Assert.IsTrue(allTokens.Any(t => t.Value == "test"));
            Assert.IsTrue(secondResult.IsBalanced()); // Should now be balanced
        }
    }

    [TestClass]
    public class TokenSetExtensionsTests : TokenizerTests
    {
        [TestMethod]
        public void GetTokensOfType_ReturnsCorrectTokens()
        {
            var result = _tokenizer.Tokenize("(echo hello)");
            
            var openParens = result.GetTokensOfType(TokenType.OpenParen).ToArray();
            Assert.HasCount(1, openParens);
            Assert.AreEqual("(", openParens[0].Value);
        }

        [TestMethod]
        public void GetNonWhitespaceTokens_FiltersWhitespace()
        {
            var result = _tokenizer.Tokenize("  hello   world  ");
            
            var nonWhitespace = result.GetNonWhitespaceTokens().ToArray();
            Assert.HasCount(3, nonWhitespace); // hello, world, EndOfInput
            Assert.IsTrue(nonWhitespace.All(t => t.Type != TokenType.Whitespace));
        }

        [TestMethod] 
        public void IsBalanced_WithNoTokens_ReturnsTrue()
        {
            var result = _tokenizer.Tokenize("");
            
            Assert.IsTrue(result.IsBalanced());
        }

        [TestMethod]
        public void IsBalanced_WithErrorTokens_ChecksForUnclosedErrors()
        {
            // Test will reveal if there are issues with the extension method syntax
            var result = _tokenizer.Tokenize("\"unclosed quote");
            
            // This should compile and run if TokenSetExtensions.IsBalanced() works
            Assert.IsFalse(result.IsBalanced());
        }

        [TestMethod]
        public void IsBalanced_WithLineContinuation_ReturnsFalse()
        {
            var result = _tokenizer.Tokenize("echo test^");
            
            // Line ending with ^ should not be balanced
            Assert.IsFalse(result.IsBalanced());
        }

        [TestMethod]
        public void IsBalanced_WithEscapedCaret_ReturnsTrue()
        {
            var result = _tokenizer.Tokenize("echo test^^");
            
            // ^^ becomes a single ^ character, not line continuation
            Assert.IsTrue(result.IsBalanced());
        }
    }
}