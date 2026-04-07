using Bat.Commands;
using Bat.Console;
using Bat.Context;
using Bat.Context.Dos;
using Bat.Parsing;
using Bat.Tokens;

namespace Bat.UnitTests;

[TestClass]
public class BasicTokenization
{
    [TestMethod]
    public void EmptyString_ReturnsOnlyEndOfInput()
    {
        var result = Parser.Parse("");

        var tokens = result.RawTokens.ToList();
        var lines = result.Lines.ToList();
        Assert.HasCount(1, tokens);
        Assert.HasCount(1, lines);
        Assert.IsTrue(lines[0] is EmptyLine);
        Assert.IsTrue(tokens[0] is EndOfLineToken);
    }

    [TestMethod]
    public void GenericCommand_ReturnsCommand()
    {
        var result = Parser.Parse("xcopy");

        var tokens = result.RawTokens.ToList();
        var lines = result.Lines.ToList();
        Assert.HasCount(2, tokens);
        Assert.IsTrue(tokens[0] is CommandToken);
        Assert.HasCount(1, lines);
        Assert.IsFalse(lines[0] is EmptyLine);
        Assert.AreEqual("xcopy", result.ToString());
    }

    [TestMethod]
    public void BuiltInCommand_ReturnsCommand()
    {
        var result = Parser.Parse("echo 1");

        var tokens = result.RawTokens.ToList();
        var lines = result.Lines.ToList();
        Assert.HasCount(4, tokens);
        Assert.IsTrue(tokens[0] is BuiltInCommandToken<EchoCommand>);
        Assert.IsTrue(tokens[1] is WhitespaceToken);
        Assert.IsTrue(tokens[2] is TextToken);
        Assert.HasCount(1, lines);
        Assert.IsFalse(lines[0] is EmptyLine);
        Assert.AreEqual("echo 1", result.ToString());
    }

    [TestMethod]
    public void BackslashCommand_TokenizesWithoutHang()
    {
        var result = Parser.Parse(@"\notepad");

        var tokens = result.FirstLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is CommandToken cmd && cmd.Raw == @"\notepad"));
    }

    [TestMethod]
    public void BackslashPath_TokenizesAsOneToken()
    {
        var result = Parser.Parse(@"\Windows\notepad.exe");

        var tokens = result.FirstLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is CommandToken cmd && cmd.Raw == @"\Windows\notepad.exe"));
    }

    [TestMethod]
    public void DirSlashW_StillSplitsOnSlash()
    {
        var result = Parser.Parse("dir/w");

        var tokens = result.FirstLine.ToList();
        Assert.IsTrue(tokens[0] is BuiltInCommandToken<DirCommand>);
        Assert.IsTrue(tokens.Any(t => t is TextToken txt && txt.Raw == "/w"));
    }
}

[TestClass]
public class QuotedStringTokenization
{
    [TestMethod]
    public void DoubleQuotedString_ParsesCorrectly()
    {
        var result = Parser.Parse("echo \"hello world\"");

        var tokens = result.LastLine.ToList();
        Assert.HasCount(3, tokens);
        Assert.IsTrue(tokens[0] is BuiltInCommandToken<EchoCommand>);
        Assert.IsTrue(tokens[1] is WhitespaceToken);
        Assert.IsTrue(tokens[2] is QuotedTextToken);

        var quoted = (QuotedTextToken)tokens[2];
        Assert.AreEqual("\"", quoted.OpenQuote);
        Assert.AreEqual("hello world", quoted.Value);
        Assert.AreEqual("\"", quoted.CloseQuote);
    }

    [TestMethod]
    public void SingleQuotedString_ParsesCorrectly()
    {
        var result = Parser.Parse("echo 'hello world'");

        var tokens = result.LastLine.ToList();
        var quoted = tokens.OfType<QuotedTextToken>().First();
        Assert.AreEqual("'", quoted.OpenQuote);
        Assert.AreEqual("hello world", quoted.Value);
        Assert.AreEqual("'", quoted.CloseQuote);
    }

    [TestMethod]
    public void UnclosedQuote_TreatsAsText()
    {
        var result = Parser.Parse("echo \"hello");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is QuotedTextToken));
        Assert.IsFalse(result.IsIncomplete);
    }

    [TestMethod]
    public void UnclosedQuoteInDirCommand_ParsesCorrectly()
    {
        var result = Parser.Parse("dir \"__azu*");

        var tokens = result.LastLine.ToList();
        var quoted = tokens.OfType<QuotedTextToken>().First();
        Assert.AreEqual("\"", quoted.OpenQuote);
        Assert.AreEqual("__azu*", quoted.Value);
        Assert.AreEqual("", quoted.CloseQuote);
        Assert.AreEqual("\"__azu*", quoted.Raw);
    }

    [TestMethod]
    public void EmptyQuotedString_ParsesCorrectly()
    {
        var result = Parser.Parse("echo \"\"");

        var tokens = result.LastLine.ToList();
        var quoted = tokens.OfType<QuotedTextToken>().First();
        Assert.AreEqual("", quoted.Value);
        Assert.IsFalse(result.IsIncomplete);
    }
}

[TestClass]
public class VariableTokenization
{
    private readonly DosContext context;

    public VariableTokenization()
    {
        var fileSystem = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        context = new DosContext(fileSystem);
        // Set environment variable for testing
        context.EnvironmentVariables.Add("TESTVAR", "TestValue");
    }

    [TestMethod]
    public void EnvironmentVariable_ExpandsToText()
    {
        var result = Parser.Parse("echo %TESTVAR%");

        var tokens = result.LastLine.ToList();
        var textToken = tokens.OfType<TextToken>().First();
        Assert.AreEqual("%TESTVAR%", textToken.Value);
        Assert.AreEqual("%TESTVAR%", textToken.Raw);
    }

    [TestMethod]
    public void DelayedExpansionVariable_ParsesCorrectly()
    {
        var result = Parser.Parse("echo !TEST^^VAR!");

        var tokens = result.LastLine.ToList();
        var delayedVar = tokens.OfType<DelayedExpansionVariableToken>().First();
        Assert.AreEqual("TEST^VAR", delayedVar.Name);
        Assert.AreEqual("!TEST^^VAR!", delayedVar.Raw);
    }

    [TestMethod]
    public void DelayedExpansionWithEscapedExclamation_ParsesCorrectly()
    {
        var result = Parser.Parse("echo !foo^!bar!");

        var tokens = result.LastLine.ToList();
        var delayedVar = tokens.OfType<DelayedExpansionVariableToken>().First();
        Assert.AreEqual("foo!bar", delayedVar.Name);
        Assert.AreEqual("!foo^!bar!", delayedVar.Raw);
    }

    [TestMethod]
    public void UnclosedVariable_TreatsAsText()
    {
        var result = Parser.Parse("echo %TESTVAR");

        var tokens = result.LastLine.ToList();
        var textToken = tokens.OfType<TextToken>().First(t => t.Raw.Contains('%'));
        Assert.AreEqual("%TESTVAR", textToken.Raw);
    }
}

[TestClass]
public class RedirectionTokenization
{
    [TestMethod]
    public void OutputRedirection_ParsesCorrectly()
    {
        var result = Parser.Parse("echo hello > output.txt");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is OutputRedirectionToken));
    }

    [TestMethod]
    public void AppendRedirection_ParsesCorrectly()
    {
        var result = Parser.Parse("echo hello >> output.txt");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is AppendRedirectionToken));
    }

    [TestMethod]
    public void InputRedirection_ParsesCorrectly()
    {
        var result = Parser.Parse("sort < input.txt");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is InputRedirectionToken));
    }

    [TestMethod]
    public void StdErrRedirection_ParsesCorrectly()
    {
        var result = Parser.Parse("command 2> error.log");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is StdErrRedirectionToken));
    }

    [TestMethod]
    public void StdErrToStdOut_ParsesCorrectly()
    {
        var result = Parser.Parse("command 2>&1");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is StdErrToStdOutRedirectionToken));
    }
}

[TestClass]
public class CommandSeparatorTokenization
{
    [TestMethod]
    public void CommandSeparator_ParsesCorrectly()
    {
        var result = Parser.Parse("echo hello & echo world");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is CommandSeparatorToken));
        Assert.AreEqual(2, tokens.OfType<BuiltInCommandToken<EchoCommand>>().Count());
    }

    [TestMethod]
    public void ConditionalAnd_ParsesCorrectly()
    {
        var result = Parser.Parse("dir && echo success");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is ConditionalAndToken));
    }

    [TestMethod]
    public void ConditionalOr_ParsesCorrectly()
    {
        var result = Parser.Parse("dir || echo failed");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is ConditionalOrToken));
    }

    [TestMethod]
    public void Pipe_ParsesCorrectly()
    {
        var result = Parser.Parse("dir | find \"txt\"");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is PipeToken));
    }
}

[TestClass]
public class BlockCommandTokenization
{
    [TestMethod]
    public void IfCommand_ParsesBlockStart()
    {
        var result = Parser.Parse("if exist file.txt (echo found)");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is BuiltInCommandToken<IfCommand>));
        Assert.IsTrue(tokens.Any(t => t is BlockStartToken));
        Assert.IsTrue(tokens.Any(t => t is BlockEndToken));
        Assert.AreEqual("if exist file.txt (echo found)", result.ToString());
    }

    [TestMethod]
    public void IfElseCommand_ParsesCorrectly()
    {
        var result = Parser.Parse("if 1==1 (echo yes) else (echo no)");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is BuiltInCommandToken<IfCommand>));
        Assert.IsTrue(tokens.Any(t => t is BuiltInCommandToken<ElseCommand>));
        Assert.AreEqual(2, tokens.Count(t => t is BlockStartToken));
        Assert.AreEqual(2, tokens.Count(t => t is BlockEndToken));
        Assert.AreEqual("if 1==1 (echo yes) else (echo no)", result.ToString());
    }

    [TestMethod]
    public void ForCommand_ParsesWithParameter()
    {
        var result = Parser.Parse("for %%i in (*.txt) do echo %%i");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is BuiltInCommandToken<ForCommand>));
        Assert.IsTrue(tokens.Any(t => t is ForParameterToken));

        var forParams = tokens.OfType<ForParameterToken>().ToList();
        Assert.IsGreaterThanOrEqualTo(1, forParams.Count);
        Assert.AreEqual("i", forParams[0].Parameter);
    }

    [TestMethod]
    public void ParenthesesInNonBlockCommand_TreatedAsText()
    {
        var result = Parser.Parse("xcopy (file.txt)");

        var tokens = result.LastLine.ToList();
        // ( should be treated as text when not following a block command
        Assert.IsTrue(tokens[0] is CommandToken);
        // The tokenizer should recognize this is not a block context
    }
}

[TestClass]
public class MultilineTokenization
{
    [TestMethod]
    public void EscapeAtEndOfLine_ContinuesNextLine()
    {
        var result = Parser.Parse("echo hello^");

        Assert.IsTrue(result.IsIncomplete);
        Assert.IsTrue(result.LastLine[^1] is ContinuationToken);
    }

    [TestMethod]
    public void MultipleLines_ParsesSeparately()
    {
        var result = Parser.Parse("echo line1\r\necho line2");

        var lines = result.Lines.ToList();
        Assert.HasCount(2, lines);
        Assert.IsTrue(lines[0][0] is BuiltInCommandToken<EchoCommand>);
        Assert.IsTrue(lines[1][0] is BuiltInCommandToken<EchoCommand>);
    }

    [TestMethod]
    public void UnfinishedIfBlock_NotComplete()
    {
        var result = Parser.Parse("if 1==1 (");

        Assert.IsTrue(result.IsIncomplete);
    }

    [TestMethod]
    public void ContinuedCommand_PreservesContext()
    {
        var firstLine = Parser.Parse("echo hello^");
        var secondLine = Parser.Parse("world", firstLine);

        Assert.IsFalse(secondLine.IsIncomplete);
        var allTokens = secondLine.RawTokens.ToList();
        // Should contain tokens from both lines
        Assert.IsTrue(allTokens.Any(t => t is BuiltInCommandToken<EchoCommand>));
    }
}

[TestClass]
public class SpecialTokenization
{
    [TestMethod]
    public void EchoSuppressor_ParsesCorrectly()
    {
        var result = Parser.Parse("@echo off");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens[0] is EchoSupressorToken);
        Assert.IsTrue(tokens.Any(t => t is BuiltInCommandToken<EchoCommand>));
    }

    [TestMethod]
    public void Label_ParsesCorrectly()
    {
        var result = Parser.Parse(":mylabel");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens[0] is LabelToken);

        var label = (LabelToken)tokens[0];
        Assert.AreEqual("mylabel", label.Value);
    }

    [TestMethod]
    public void CommentLabel_ParsesAsLabel()
    {
        var result = Parser.Parse(":: This is a comment");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens[0] is LabelToken);

        var label = (LabelToken)tokens[0];
        // :: is treated as a label with : as the name
        Assert.AreEqual(": This is a comment", label.Value);
    }

    [TestMethod]
    public void ComparisonOperator_ParsesCorrectly()
    {
        var result = Parser.Parse("if 1==1 echo yes");

        var tokens = result.LastLine.ToList();
        var compOp = tokens.OfType<ComparisonOperatorToken>().FirstOrDefault();
        Assert.IsNotNull(compOp);
        Assert.AreEqual("==", compOp.Raw);
    }

    [TestMethod]
    public void EscapedCharacter_ParsesAsText()
    {
        var result = Parser.Parse("echo ^>test");

        var tokens = result.LastLine.ToList();
        // The ^> should be parsed as escaped > character
        var textTokens = tokens.OfType<TextToken>().ToList();
        Assert.IsTrue(textTokens.Any(t => t.Value.Contains('>')));
        Assert.IsFalse(result.IsIncomplete);
    }
}

[TestClass]
public class ComplexScenarios
{
    private readonly DosContext context;

    public ComplexScenarios()
    {
        var fileSystem = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        context = new DosContext(fileSystem);
        context.EnvironmentVariables["PATH"] = "C:\\Windows\\System32";
    }

    [TestMethod]
    public void ComplexIfCommand_ParsesCorrectly()
    {
        var result = Parser.Parse("if \"%PATH%\"==\"C:\\Windows\\System32\" (echo correct path)");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is BuiltInCommandToken<IfCommand>));
        Assert.IsTrue(tokens.Any(t => t is QuotedTextToken));
        Assert.IsTrue(tokens.Any(t => t is ComparisonOperatorToken));
        Assert.IsTrue(tokens.Any(t => t is BlockStartToken));
    }

    [TestMethod]
    public void MultipleRedirections_ParseCorrectly()
    {
        var result = Parser.Parse("command > output.txt 2> error.log");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is OutputRedirectionToken));
        Assert.IsTrue(tokens.Any(t => t is StdErrRedirectionToken));
    }

    [TestMethod]
    public void ChainedCommands_ParseCorrectly()
    {
        var result = Parser.Parse("dir && echo success || echo failed");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is ConditionalAndToken));
        Assert.IsTrue(tokens.Any(t => t is ConditionalOrToken));
        Assert.AreEqual(3, tokens.OfType<CommandToken>().Count() + tokens.OfType<IBuiltInCommandToken>().Count());
    }

    [TestMethod]
    public void QuotedStringWithParentheses_DoesNotAffectBlocks()
    {
        var result = Parser.Parse("echo \"test (with parens)\"");

        var tokens = result.LastLine.ToList();
        var quoted = tokens.OfType<QuotedTextToken>().First();
        Assert.AreEqual("test (with parens)", quoted.Value);
        Assert.IsFalse(result.IsIncomplete);
    }

    [TestMethod]
    public void NestedBlocks_TrackCorrectly()
    {
        var result = Parser.Parse("if 1==1 (if 2==2 (echo nested))");

        var tokens = result.RawTokens.ToList();

        // Debug: Check what tokens we actually got
        var ifCommands = tokens.OfType<BuiltInCommandToken<IfCommand>>().Count();
        var echoCommands = tokens.OfType<BuiltInCommandToken<EchoCommand>>().Count();

        Assert.AreEqual(2, ifCommands, "Should have 2 IF command tokens");
        Assert.AreEqual(1, echoCommands, "Should have 1 ECHO command token");
        Assert.AreEqual(2, tokens.Count(t => t is BlockStartToken));
        Assert.AreEqual(2, tokens.Count(t => t is BlockEndToken));
        Assert.IsFalse(result.IsIncomplete);
    }

    [TestMethod]
    public void Block_DoesNotAllowElse()
    {
        var parser = new Parser();
        parser.Append("(\r\necho 1\r\n) else (\r\necho2 )");

        Assert.IsNotNull(parser.ErrorMessage);

        var result = parser.ParseCommand();

        var elses = result.RawTokens.OfType<BuiltInCommandToken<ElseCommand>>();
        Assert.IsEmpty(elses);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("echo hello world")]
    [DataRow("xcopy")]
    [DataRow("@echo off")]
    [DataRow("echo \"hello world\"")]
    [DataRow("echo 'hello world'")]
    [DataRow("echo %PATH%")]
    [DataRow("echo !VAR!")]
    [DataRow("echo ^>test")]
    [DataRow("echo hello > output.txt")]
    [DataRow("echo hello >> output.txt")]
    [DataRow("sort < input.txt")]
    [DataRow("command 2> error.log")]
    [DataRow("command 2>&1")]
    [DataRow("echo hello & echo world")]
    [DataRow("dir && echo success")]
    [DataRow("dir || echo failed")]
    [DataRow("dir | find \"txt\"")]
    [DataRow("if 1==1 (echo yes)")]
    [DataRow("if 1==1 (echo yes) else (echo no)")]
    [DataRow("for %%i in (*.txt) do echo %%i")]
    [DataRow(":mylabel")]
    [DataRow(":: This is a comment")]
    [DataRow("echo   multiple   spaces")]
    [DataRow("echo\ttabs")]
    [DataRow("echo \"quoted with (parens)\"")]
    [DataRow("if exist file.txt (echo found)")]
    [DataRow("echo line1\r\necho line2")]
    [DataRow("echo line1\r\necho line2\r\necho line3")]
    [DataRow("echo hello^\r\nworld")]
    [DataRow("goto foo\r\necho 1\r\n:foo\r\necho 2")]
    [DataRow("echo ^^test")]
    [DataRow("if 1==1 (if 2==2 (echo nested))")]
    [DataRow("dir && echo success || echo failed")]
    [DataRow("command > output.txt 2> error.log")]
    [DataRow("if \"%PATH%\"==\"value\" (echo match)")]
    [DataRow("echo test & dir & pause")]
    [DataRow("@echo off\r\n@echo Processing...")]
    [DataRow("for %%i in (*.txt) do (echo %%i)")]
    [DataRow("if else == foo echo match")]
    [DataRow("setlocal")]
    [DataRow(@"\notepad")]
    [DataRow(@"\Windows\notepad.exe")]
    [DataRow("endlocal")]
    [DataRow("exit")]
    [DataRow("quit")]
    public void ToString_EqualsParsedData(string data)
    {
        var tokens = Parser.Parse(data);
        Assert.AreEqual(data, tokens.ToString());
    }
}

[TestClass]
public class BatchParameterTokenization
{
    [TestMethod]
    public void SimpleParameter_ParsesCorrectly()
    {
        var result = Parser.Parse("echo %1 %2 %3");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is BuiltInCommandToken<EchoCommand>));
        // %1, %2, %3 should be parsed as text tokens with parameter values
        Assert.AreEqual("echo %1 %2 %3", result.ToString());
    }

    [TestMethod]
    public void ModifiedParameter_ParsesCorrectly()
    {
        var result = Parser.Parse("echo %~1");

        Assert.AreEqual("echo %~1", result.ToString());
    }

    [TestMethod]
    public void ParameterWithModifiers_ParsesCorrectly()
    {
        var result = Parser.Parse("echo %~dp1 %~nx2");

        Assert.AreEqual("echo %~dp1 %~nx2", result.ToString());
    }

    [TestMethod]
    public void AllParameters_ParsesCorrectly()
    {
        var result = Parser.Parse("echo %*");

        Assert.AreEqual("echo %*", result.ToString());
    }
}

[TestClass]
public class SetCommandTokenization
{
    [TestMethod]
    public void SimpleSet_ParsesCorrectly()
    {
        var result = Parser.Parse("set VAR=value");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is BuiltInCommandToken<SetCommand>));
        Assert.AreEqual("set VAR=value", result.ToString());
    }

    [TestMethod]
    public void SetWithSpaces_ParsesCorrectly()
    {
        var result = Parser.Parse("set \"VAR=value with spaces\"");

        Assert.AreEqual("set \"VAR=value with spaces\"", result.ToString());
    }

    [TestMethod]
    public void SetArithmetic_ParsesCorrectly()
    {
        var result = Parser.Parse("set /A RESULT=5+3");

        Assert.AreEqual("set /A RESULT=5+3", result.ToString());
    }

    [TestMethod]
    public void SetPrompt_ParsesCorrectly()
    {
        var result = Parser.Parse("set /P VAR=Enter value: ");

        Assert.AreEqual("set /P VAR=Enter value: ", result.ToString());
    }
}

[TestClass]
public class IfVariantsTokenization
{
    [TestMethod]
    public void IfExist_ParsesCorrectly()
    {
        var result = Parser.Parse("if exist file.txt echo found");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is BuiltInCommandToken<IfCommand>));
        Assert.AreEqual("if exist file.txt echo found", result.ToString());
    }

    [TestMethod]
    public void IfNotExist_ParsesCorrectly()
    {
        var result = Parser.Parse("if not exist file.txt echo not found");

        Assert.AreEqual("if not exist file.txt echo not found", result.ToString());
    }

    [TestMethod]
    public void IfDefined_ParsesCorrectly()
    {
        var result = Parser.Parse("if defined VAR echo variable is set");

        Assert.AreEqual("if defined VAR echo variable is set", result.ToString());
    }

    [TestMethod]
    public void IfErrorLevel_ParsesCorrectly()
    {
        var result = Parser.Parse("if errorlevel 1 echo error occurred");

        Assert.AreEqual("if errorlevel 1 echo error occurred", result.ToString());
    }

    [TestMethod]
    public void IfCaseInsensitive_ParsesCorrectly()
    {
        var result = Parser.Parse("if /I \"%VAR%\"==\"test\" echo match");

        Assert.AreEqual("if /I \"%VAR%\"==\"test\" echo match", result.ToString());
    }

    [TestMethod]
    public void IfComparisonOperators_ParseCorrectly()
    {
        var tests = new[]
        {
            "if 1 EQU 1 echo equal",
            "if 2 NEQ 1 echo not equal",
            "if 2 GTR 1 echo greater",
            "if 1 LSS 2 echo less",
            "if 2 GEQ 2 echo greater or equal",
            "if 1 LEQ 1 echo less or equal"
        };

        foreach (var test in tests)
        {
            var result = Parser.Parse(test);
            Assert.AreEqual(test, result.ToString(), $"Failed for: {test}");
        }
    }
}

[TestClass]
public class ForLoopVariantsTokenization
{
    [TestMethod]
    public void ForDirectories_ParsesCorrectly()
    {
        var result = Parser.Parse("for /D %%i in (*.*) do echo %%i");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is BuiltInCommandToken<ForCommand>));
        Assert.AreEqual("for /D %%i in (*.*) do echo %%i", result.ToString());
    }

    [TestMethod]
    public void ForRecursive_ParsesCorrectly()
    {
        var result = Parser.Parse("for /R %%i in (*.txt) do echo %%i");

        Assert.AreEqual("for /R %%i in (*.txt) do echo %%i", result.ToString());
    }

    [TestMethod]
    public void ForLoop_ParsesCorrectly()
    {
        var result = Parser.Parse("for /L %%i in (1,1,10) do echo %%i");

        Assert.AreEqual("for /L %%i in (1,1,10) do echo %%i", result.ToString());
    }

    [TestMethod]
    public void ForFileProcessing_ParsesCorrectly()
    {
        var result = Parser.Parse("for /F \"tokens=1,2 delims=,\" %%i in (file.txt) do echo %%i %%j");

        Assert.AreEqual("for /F \"tokens=1,2 delims=,\" %%i in (file.txt) do echo %%i %%j", result.ToString());
    }

    [TestMethod]
    public void ForWithCommand_ParsesCorrectly()
    {
        var result = Parser.Parse("for /F %%i in ('dir /b') do echo %%i");

        Assert.AreEqual("for /F %%i in ('dir /b') do echo %%i", result.ToString());
    }
}

[TestClass]
public class SpecialCharactersTokenization
{
    [TestMethod]
    public void EmailAddress_ParsesCorrectly()
    {
        var result = Parser.Parse("echo test@example.com");

        Assert.AreEqual("echo test@example.com", result.ToString());
    }

    [TestMethod]
    public void PercentSign_ParsesCorrectly()
    {
        var result = Parser.Parse("echo 100%% complete");

        Assert.AreEqual("echo 100%% complete", result.ToString());
    }

    [TestMethod]
    public void CommandFlags_ParseCorrectly()
    {
        var tests = new[]
        {
            "dir /s /b",
            "copy /Y source.txt dest.txt",
            "xcopy /E /H /C /I",
            "del /F /Q /S *.tmp"
        };

        foreach (var test in tests)
        {
            var result = Parser.Parse(test);
            Assert.AreEqual(test, result.ToString(), $"Failed for: {test}");
        }
    }

    [TestMethod]
    public void EqualsSignInArgument_ParsesCorrectly()
    {
        var result = Parser.Parse("set VAR=value");

        Assert.AreEqual("set VAR=value", result.ToString());
    }

    [TestMethod]
    public void Ampersand_ParsesCorrectly()
    {
        var result = Parser.Parse("echo This ^& That");

        Assert.AreEqual("echo This ^& That", result.ToString());
    }
}

[TestClass]
public class WhitespaceAndEmptyLinesTokenization
{
    [TestMethod]
    public void EmptyLine_ParsesCorrectly()
    {
        var result = Parser.Parse("echo line1\r\n\r\necho line2");

        var lines = result.Lines.ToList();
        Assert.HasCount(3, lines);
        Assert.IsTrue(lines[1] is EmptyLine);
        Assert.AreEqual("echo line1\r\n\r\necho line2", result.ToString());
    }

    [TestMethod]
    public void WhitespaceOnlyLine_ParsesCorrectly()
    {
        var result = Parser.Parse("echo line1\r\n   \r\necho line2");

        Assert.AreEqual("echo line1\r\n   \r\necho line2", result.ToString());
    }

    [TestMethod]
    public void TabsAndSpacesMixed_ParsesCorrectly()
    {
        var result = Parser.Parse("echo \t  mixed \t whitespace");

        Assert.AreEqual("echo \t  mixed \t whitespace", result.ToString());
    }

    [TestMethod]
    public void TrailingWhitespace_PreservedCorrectly()
    {
        var result = Parser.Parse("echo test   ");

        Assert.AreEqual("echo test   ", result.ToString());
    }
}

[TestClass]
public class NestedQuotesTokenization
{
    [TestMethod]
    public void SingleInsideDouble_ParsesCorrectly()
    {
        var result = Parser.Parse("echo \"outer 'inner' outer\"");

        var tokens = result.LastLine.ToList();
        var quoted = tokens.OfType<QuotedTextToken>().First();
        Assert.AreEqual("outer 'inner' outer", quoted.Value);
        Assert.AreEqual("echo \"outer 'inner' outer\"", result.ToString());
    }

    [TestMethod]
    public void DoubleInsideSingle_ParsesCorrectly()
    {
        var result = Parser.Parse("echo 'outer \"inner\" outer'");

        Assert.AreEqual("echo 'outer \"inner\" outer'", result.ToString());
    }

    [TestMethod]
    public void EscapedQuote_ParsesCorrectly()
    {
        var result = Parser.Parse("echo ^\"escaped quote^\"");

        Assert.AreEqual("echo ^\"escaped quote^\"", result.ToString());
    }
}

[TestClass]
public class SpecialFilenamesTokenization
{
    [TestMethod]
    public void RedirectToNul_ParsesCorrectly()
    {
        var result = Parser.Parse("echo test > NUL");

        Assert.AreEqual("echo test > NUL", result.ToString());
    }

    [TestMethod]
    public void TypeCon_ParsesCorrectly()
    {
        var result = Parser.Parse("type CON");

        Assert.AreEqual("type CON", result.ToString());
    }

    [TestMethod]
    public void SpecialDevices_ParseCorrectly()
    {
        var tests = new[]
        {
            "copy file.txt PRN",
            "echo test > LPT1",
            "type COM1",
            "copy CON output.txt"
        };

        foreach (var test in tests)
        {
            var result = Parser.Parse(test);
            Assert.AreEqual(test, result.ToString(), $"Failed for: {test}");
        }
    }
}

[TestClass]
public class CallCommandTokenization
{
    [TestMethod]
    public void CallSubroutine_ParsesCorrectly()
    {
        var result = Parser.Parse("call :subroutine arg1 arg2");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is BuiltInCommandToken<CallCommand>));
        Assert.AreEqual("call :subroutine arg1 arg2", result.ToString());
    }

    [TestMethod]
    public void CallExternalBatch_ParsesCorrectly()
    {
        var result = Parser.Parse("call otherbatch.bat %1 %2");

        Assert.AreEqual("call otherbatch.bat %1 %2", result.ToString());
    }

    [TestMethod]
    public void CallWithQuotes_ParsesCorrectly()
    {
        var result = Parser.Parse("call \"path with spaces.bat\" arg");

        Assert.AreEqual("call \"path with spaces.bat\" arg", result.ToString());
    }
}

[TestClass]
public class GotoCommandTokenization
{
    [TestMethod]
    public void GotoLabel_ParsesCorrectly()
    {
        var result = Parser.Parse("goto :label");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is BuiltInCommandToken<GotoCommand>));
        Assert.AreEqual("goto :label", result.ToString());
    }

    [TestMethod]
    public void GotoEof_ParsesCorrectly()
    {
        var result = Parser.Parse("goto :EOF");

        Assert.AreEqual("goto :EOF", result.ToString());
    }
}

[TestClass]
public class MixedEscapeScenarios
{
    [TestMethod]
    public void TripleEscape_ParsesCorrectly()
    {
        var result = Parser.Parse("echo ^^^>test");

        Assert.AreEqual("echo ^^^>test", result.ToString());
    }

    [TestMethod]
    public void EscapeInPath_ParsesCorrectly()
    {
        var result = Parser.Parse("set \"path=C:\\Path\\^(with parens^)\"");

        Assert.AreEqual("set \"path=C:\\Path\\^(with parens^)\"", result.ToString());
    }
}

[TestClass]
public class DirCommandSyntaxVariants
{
    // CMD:  dir\w  →  dir \w  (lists root-relative path \w; "File Not Found" if absent)
    // BAT:  "dir\w" is read as one CommandToken by the tokenizer; the Dispatcher splits at \
    //       and runs DirCommand with argument \w — same end result.
    [TestMethod]
    public void DirBackslashW_ParsesAsCommandToken()
    {
        var result = Parser.Parse(@"dir\w");

        var token = result.LastLine.First();
        Assert.IsTrue(token is CommandToken, $"Expected CommandToken, got {token.GetType().Name}");
        Assert.AreEqual(@"dir\w", token.Raw);
    }

    // CMD:  dir/w  →  dir /w  (wide format; exit 0)
    // BAT:  "dir/w" is now split by ReadWord at / → BuiltInCommandToken<DirCommand> + /w flag
    [TestMethod]
    public void DirSlashW_ParsesAsBuiltInCommand()
    {
        var result = Parser.Parse("dir/w");

        var token = result.LastLine.First();
        Assert.IsTrue(token is BuiltInCommandToken<DirCommand>, $"Expected BuiltInCommandToken<DirCommand>, got {token.GetType().Name}");
    }

    // CMD:  dir-w  →  not recognised as internal/external command; exit 1.
    // BAT:  "dir-w" is read as one word by ReadWord → resolves to CommandToken, not DirCommand.
    [TestMethod]
    public void DirDashW_ParsesAsGenericCommand()
    {
        var result = Parser.Parse("dir-w");

        var token = result.LastLine.First();
        Assert.IsTrue(token is CommandToken);
        Assert.IsFalse(token is IBuiltInCommandToken);
    }

    // CMD:  dir -w  →  "-w" is a filename pattern (not a flag in CMD); shows only files named "-w".
    // BAT:  "dir" is recognised as DirCommand; "-w" becomes a TextToken argument that
    //       ArgumentSet interprets as the /W flag (wide format).
    [TestMethod]
    public void DirSpaceDashW_ParsesAsDirCommandWithDashWArgument()
    {
        var result = Parser.Parse("dir -w");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens[0] is BuiltInCommandToken<DirCommand>);
        Assert.IsTrue(tokens[1] is WhitespaceToken);
        Assert.AreEqual("-w", tokens[2].Raw);
    }

    [TestMethod]
    public void EscapeBeforeNewline_ParsesCorrectly()
    {
        var result = Parser.Parse("echo test^^");

        Assert.IsFalse(result.IsIncomplete);
        Assert.AreEqual("echo test^^", result.ToString());
    }

    [TestMethod]
    public void MultipleEscapedCharacters_ParseCorrectly()
    {
        var result = Parser.Parse("echo ^<^>^|^&");

        Assert.AreEqual("echo ^<^>^|^&", result.ToString());
    }
}

[TestClass]
public class FileHandleRedirectionTokenization
{
    [TestMethod]
    public void ExplicitStdOut_ParsesCorrectly()
    {
        var result = Parser.Parse("command 1>out.txt");

        Assert.AreEqual("command 1>out.txt", result.ToString());
    }

    [TestMethod]
    public void StdOutToStdErr_ParsesCorrectly()
    {
        var result = Parser.Parse("command 1>&2");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is StdOutToStdErrRedirectionToken));
        Assert.AreEqual("command 1>&2", result.ToString());
    }

    [TestMethod]
    public void MultipleFileHandles_ParseCorrectly()
    {
        var result = Parser.Parse("command 1>out.txt 2>err.txt");

        Assert.AreEqual("command 1>out.txt 2>err.txt", result.ToString());
    }

    [TestMethod]
    public void CombinedRedirection_ParsesCorrectly()
    {
        var result = Parser.Parse("command >out.txt 2>&1");

        Assert.AreEqual("command >out.txt 2>&1", result.ToString());
    }
}

[TestClass]
public class RemCommandTokenization
{
    [TestMethod]
    public void RemComment_ParsesCorrectly()
    {
        var result = Parser.Parse("rem This is a comment");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is BuiltInCommandToken<RemCommand>));
        Assert.AreEqual("rem This is a comment", result.ToString());
    }

    [TestMethod]
    public void RemAfterCommand_ParsesCorrectly()
    {
        var result = Parser.Parse("echo test & rem comment");

        Assert.AreEqual("echo test & rem comment", result.ToString());
    }

    [TestMethod]
    public void ColonColonComment_ParsesCorrectly()
    {
        var result = Parser.Parse(":: This is also a comment");

        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens[0] is LabelToken);
        Assert.AreEqual(":: This is also a comment", result.ToString());
    }
}

[TestClass]
public class ErrorDetection
{
    [TestMethod]
    public void IncompleteIfCondition_WithBlockStart_ShowsError()
    {
        var result = Parser.Parse("if 1 (");

        Assert.IsTrue(result.HasError);
        Assert.AreEqual("( was unexpected at this time.", result.ErrorMessage);
    }

    [TestMethod]
    public void IncompleteIfCondition_WithAmpersand_ShowsError()
    {
        var result = Parser.Parse("if 1 & echo 1");

        Assert.IsTrue(result.HasError);
        Assert.AreEqual("& was unexpected at this time.", result.ErrorMessage);
    }

    [TestMethod]
    public void DoubleElse_ShowsError()
    {
        var result = Parser.Parse("if 1==1 (\r\necho 1\r\n) else (\r\necho 2\r\n) else (");

        Assert.IsTrue(result.HasError);
        Assert.AreEqual("else was unexpected at this time.", result.ErrorMessage);
    }

    [TestMethod]
    public void UnmatchedClosingParen_ShowsError()
    {
        var result = Parser.Parse(")");

        Assert.IsTrue(result.HasError);
        Assert.AreEqual(") was unexpected at this time.", result.ErrorMessage);
    }

    [TestMethod]
    public void StandaloneAmpersand_ShowsError()
    {
        var result = Parser.Parse("&");

        Assert.IsTrue(result.HasError);
        Assert.AreEqual("& was unexpected at this time.", result.ErrorMessage);
    }

    [TestMethod]
    public void AmpersandWithSpace_ShowsError()
    {
        var result = Parser.Parse(" &");

        Assert.IsTrue(result.HasError);
        Assert.AreEqual("& was unexpected at this time.", result.ErrorMessage);
    }

    [TestMethod]
    public void StandalonePipe_ShowsError()
    {
        var result = Parser.Parse("|");

        Assert.IsTrue(result.HasError);
        Assert.AreEqual("| was unexpected at this time.", result.ErrorMessage);
    }
}

[TestClass]
public class TextOrCommandTokenization
{
    [TestMethod]
    public void ForWithBlockBody_ParsesCorrectly()
    {
        var result = Parser.Parse("for %%i in (*.txt) do (echo %%i)");

        var tokens = result.LastLine.ToList();
        Assert.IsFalse(result.HasError);
        Assert.IsTrue(tokens.Any(t => t is BuiltInCommandToken<ForCommand>));
        Assert.IsTrue(tokens.Any(t => t is BuiltInCommandToken<EchoCommand>));
        Assert.AreEqual(2, tokens.Count(t => t is BlockStartToken));
        Assert.AreEqual(2, tokens.Count(t => t is BlockEndToken));
        Assert.AreEqual("for %%i in (*.txt) do (echo %%i)", result.ToString());
    }

    [TestMethod]
    public void ElseAtCommandBoundary_ShowsError()
    {
        // After '&', a command is expected; 'else' without a preceding if-block is an error
        var result = Parser.Parse("echo a & else echo b");

        Assert.IsTrue(result.HasError);
        Assert.AreEqual("else was unexpected at this time.", result.ErrorMessage);
    }

    [TestMethod]
    public void ElseInIfCondition_IsText()
    {
        // 'else' in an IF condition (not at a command boundary) is treated as a plain text token
        var result = Parser.Parse("if else == foo echo match");

        Assert.IsFalse(result.HasError);
        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.OfType<TextToken>().Any(t => t.Raw == "else"));
        Assert.AreEqual("if else == foo echo match", result.ToString());
    }

    [TestMethod]
    public void ElseAsArgument_IsText()
    {
        var result = Parser.Parse("echo hello else world");

        Assert.IsFalse(result.HasError);
        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.OfType<TextToken>().Any(t => t.Raw == "else"));
        Assert.AreEqual("echo hello else world", result.ToString());
    }

    [TestMethod]
    public void GreaterThanInIfCondition_IsComparison()
    {
        var result = Parser.Parse("if 5 > 3 echo greater");

        Assert.IsFalse(result.HasError);
        var tokens = result.LastLine.ToList();
        Assert.IsTrue(tokens.Any(t => t is ComparisonOperatorToken { Raw: ">" }));
        Assert.IsFalse(tokens.Any(t => t is OutputRedirectionToken));
        Assert.AreEqual("if 5 > 3 echo greater", result.ToString());
    }
}


