using Bat.Commands;
using Bat.Tokens;

namespace Bat.UnitTests;

[TestClass]
public class ArgumentSetTests
{
    private static ArgumentSpec Spec(string flags = "", string options = "") =>
        ArgumentSpec.From([new BuiltInCommandAttribute("test") { Flags = flags, Options = options }]);

    private static IToken T(string s) =>
        s.StartsWith('"') && s.EndsWith('"') ? Token.QuotedText(s) : Token.Text(s);

    private static List<IToken> Tok(params string[] parts)
    {
        var result = new List<IToken>();
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0) result.Add(Token.Whitespace(" "));
            result.Add(T(parts[i]));
        }
        return result;
    }

    // ── Flags ───────────────────────────────────────────────────────────────

    [TestMethod]
    public void Parse_Flag_RecognizesSwitch()
    {
        var args = ArgumentSet.Parse(Tok("/B"), Spec(flags: "B"));
        Assert.IsTrue(args.GetFlagValue('B'));
        Assert.IsTrue(args.GetFlagValue("B"));
        Assert.IsFalse(args.GetFlagValue('W'));
    }

    [TestMethod]
    public void Parse_Flag_CaseInsensitive()
    {
        var args = ArgumentSet.Parse(Tok("/b"), Spec(flags: "B"));
        Assert.IsTrue(args.GetFlagValue('B'));
        Assert.IsTrue(args.GetFlagValue('b'));
    }

    [TestMethod]
    public void Parse_Flag_DashPrefixRecognized()
    {
        var args = ArgumentSet.Parse(Tok("-B"), Spec(flags: "B"));
        Assert.IsTrue(args.GetFlagValue('B'));
    }

    [TestMethod]
    public void Parse_MultipleFlags_AllRecognized()
    {
        var args = ArgumentSet.Parse(Tok("/B", "/W", "/S"), Spec(flags: "B W S"));
        Assert.IsTrue(args.GetFlagValue('B'));
        Assert.IsTrue(args.GetFlagValue('W'));
        Assert.IsTrue(args.GetFlagValue('S'));
        Assert.IsFalse(args.GetFlagValue('L'));
    }

    [TestMethod]
    public void Parse_UnknownMultiCharSwitch_TreatedAsPositional()
    {
        var args = ArgumentSet.Parse(Tok("/ZZ"), Spec(flags: "B"));
        Assert.IsFalse(args.GetFlagValue('Z'));
        Assert.AreEqual("/ZZ", args.Positionals[0]);
    }

    // ── Options ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void Parse_Option_ColonSyntax()
    {
        var args = ArgumentSet.Parse(Tok("/A:D"), Spec(options: "A"));
        Assert.AreEqual("D", args.GetValue("A"));
    }

    [TestMethod]
    public void Parse_Option_NextWordSyntax()
    {
        var args = ArgumentSet.Parse(Tok("/A", "D"), Spec(options: "A"));
        Assert.AreEqual("D", args.GetValue("A"));
    }

    [TestMethod]
    public void Parse_Option_NextWordNotConsumedIfSwitch()
    {
        var args = ArgumentSet.Parse(Tok("/O", "/B"), Spec(flags: "B", options: "O"));
        Assert.AreEqual("", args.GetValue("O"));
        Assert.IsTrue(args.GetFlagValue('B'));
    }

    [TestMethod]
    public void Parse_Option_MultipleValues_GetValues()
    {
        // /A:D /A:H — two values for the same option
        var tokens = new List<IToken> { Token.Text("/A:D"), Token.Whitespace(" "), Token.Text("/A:H") };
        var args = ArgumentSet.Parse(tokens, Spec(options: "A"));
        var vals = args.GetValues("A");
        Assert.AreEqual(2, vals.Length);
        Assert.IsTrue(vals.Contains("D"));
        Assert.IsTrue(vals.Contains("H"));
    }

    [TestMethod]
    public void Parse_Option_GetValue_ReturnsFirstWhenMultiple()
    {
        var tokens = new List<IToken> { Token.Text("/A:D"), Token.Whitespace(" "), Token.Text("/A:H") };
        var args = ArgumentSet.Parse(tokens, Spec(options: "A"));
        var value = args.GetValue("A");
        Assert.AreEqual("D", value);
    }

    [TestMethod]
    public void Parse_Option_MissingValue_EmptyString()
    {
        var args = ArgumentSet.Parse(Tok("/O"), Spec(options: "O"));
        Assert.AreEqual("", args.GetValue("O"));
    }

    // ── Positionals ─────────────────────────────────────────────────────────

    [TestMethod]
    public void Parse_Positional_PlainWord()
    {
        var args = ArgumentSet.Parse(Tok(@"C:\Windows"), Spec());
        Assert.AreEqual(@"C:\Windows", args.Positionals[0]);
    }

    [TestMethod]
    public void Parse_Positional_QuotedTextStripsQuotes()
    {
        var tokens = new List<IToken> { Token.QuotedText("\"my folder\"") };
        var args = ArgumentSet.Parse(tokens, Spec());
        Assert.AreEqual("my folder", args.Positionals[0]);
    }

    [TestMethod]
    public void Parse_Positional_MixedFlagsAndPositionals()
    {
        var args = ArgumentSet.Parse(Tok("/B", @"C:\Temp"), Spec(flags: "B"));
        Assert.IsTrue(args.GetFlagValue('B'));
        Assert.AreEqual(1, args.Positionals.Count);
        Assert.AreEqual(@"C:\Temp", args.Positionals[0]);
    }

    // ── FullArgument ────────────────────────────────────────────────────────

    [TestMethod]
    public void Parse_FullArgument_StripsLeadingWhitespace()
    {
        var tokens = new List<IToken>
        {
            Token.Whitespace(" "),
            Token.Text("hello world")
        };
        var args = ArgumentSet.Parse(tokens, Spec());
        Assert.AreEqual("hello world", args.FullArgument);
    }

    [TestMethod]
    public void Parse_FullArgument_PreservesRawTokenContent()
    {
        var tokens = new List<IToken>
        {
            Token.Text("FOO=bar baz")
        };
        var args = ArgumentSet.Parse(tokens, Spec());
        Assert.AreEqual("FOO=bar baz", args.FullArgument);
    }

    // ── Help Request ────────────────────────────────────────────────────────

    [TestMethod]
    public void Parse_HelpRequest_SingleSlashQuestion()
    {
        var args = ArgumentSet.Parse(Tok("/?"), Spec());
        Assert.IsTrue(args.IsHelpRequest);
    }

    [TestMethod]
    public void Parse_HelpRequest_Anywhere()
    {
        var args = ArgumentSet.Parse(Tok("/B", "/?", "/S"), Spec(flags: "B S"));
        Assert.IsTrue(args.IsHelpRequest);
    }

    [TestMethod]
    public void Parse_HelpRequest_NoFlags_NorPositionals()
    {
        var args = ArgumentSet.Parse(Tok("/?"), Spec(flags: "B"));
        Assert.IsTrue(args.IsHelpRequest);
        Assert.AreEqual(0, args.Positionals.Count);
        Assert.IsFalse(args.GetFlagValue('B'));
    }

    // ── Empty input ─────────────────────────────────────────────────────────

    [TestMethod]
    public void Parse_Empty_AllDefaults()
    {
        var args = ArgumentSet.Parse([], Spec());
        Assert.AreEqual("", args.FullArgument);
        Assert.AreEqual(0, args.Positionals.Count);
        Assert.IsFalse(args.IsHelpRequest);
    }

    // ── Prefix-option matching (/AH → option A = H) ─────────────────────────

    [TestMethod]
    public void Parse_PrefixOption_NoColon_ValueIsRemainder()
    {
        var args = ArgumentSet.Parse(Tok("/AH"), Spec(options: "A"));
        Assert.AreEqual("H", args.GetValue("A"));
    }

    [TestMethod]
    public void Parse_PrefixOption_WithNegation()
    {
        var args = ArgumentSet.Parse(Tok("/A-H"), Spec(options: "A"));
        Assert.AreEqual("-H", args.GetValue("A"));
    }

    [TestMethod]
    public void Parse_PrefixOption_MultipleLetters()
    {
        var args = ArgumentSet.Parse(Tok("/ADH"), Spec(options: "A"));
        Assert.AreEqual("DH", args.GetValue("A"));
    }

    [TestMethod]
    public void Parse_PrefixOption_CaseInsensitive()
    {
        var args = ArgumentSet.Parse(Tok("/ah"), Spec(options: "A"));
        Assert.AreEqual("h", args.GetValue("A"));
    }

    [TestMethod]
    public void Parse_PrefixOption_DoesNotConflictWithExactFlag()
    {
        // /L = lowercase flag, /AL = attribute option A with value "L"
        var args = ArgumentSet.Parse(Tok("/L", "/AL"), Spec(flags: "L", options: "A"));
        Assert.IsTrue(args.GetFlagValue('L'));
        Assert.AreEqual("L", args.GetValue("A"));
    }

    [TestMethod]
    public void Parse_PrefixOption_LongestPrefixWins()
    {
        // If both "A" and "AB" are options and input is "/ABC", "AB" wins
        var args = ArgumentSet.Parse(Tok("/ABC"), Spec(options: "A AB"));
        Assert.AreEqual("C", args.GetValue("AB"));
        Assert.IsNull(args.GetValue("A"));
    }

    // ── Negated flags (/-X) ─────────────────────────────────────────────────

    [TestMethod]
    public void Parse_NegatedFlag_SlashDashX_RecognizedAsNegated()
    {
        var args = ArgumentSet.Parse(Tok("/-C"), Spec(flags: "C"));
        Assert.IsFalse(args.GetFlagValue('C'));
        Assert.IsFalse(args.GetFlagValue("C"));
        // Default is false, but explicit negation also gives false — distinguish via non-default:
        Assert.IsFalse(args.GetFlagValue('C', defaultValue: true),
            "/-C should override the defaultValue=true and return false");
    }

    [TestMethod]
    public void Parse_NegatedFlag_CaseInsensitive()
    {
        var args = ArgumentSet.Parse(Tok("/-c"), Spec(flags: "C"));
        Assert.IsFalse(args.GetFlagValue('C', defaultValue: true));
        Assert.IsFalse(args.GetFlagValue('c', defaultValue: true));
    }

    [TestMethod]
    public void Parse_NegatedFlag_UnknownNotRecognized()
    {
        // /-Z when Z is not a known flag → treated as positional, default returned
        var args = ArgumentSet.Parse(Tok("/-Z"), Spec(flags: "C"));
        Assert.IsFalse(args.GetFlagValue('Z'));
        Assert.AreEqual("/-Z", args.Positionals[0]);
    }

    [TestMethod]
    public void Parse_NegatedFlag_CoexistsWithPositiveFlag()
    {
        var args = ArgumentSet.Parse(Tok("/B", "/-C"), Spec(flags: "B C"));
        Assert.IsTrue(args.GetFlagValue('B'));
        Assert.IsFalse(args.GetFlagValue('C'));
        Assert.IsFalse(args.GetFlagValue('C', defaultValue: true));
    }

    [TestMethod]
    public void Parse_GetFlagValue_DefaultReturnedWhenAbsent()
    {
        var args = ArgumentSet.Parse(Tok("/B"), Spec(flags: "B C"));
        Assert.IsFalse(args.GetFlagValue('C'));
        Assert.IsTrue(args.GetFlagValue('C', defaultValue: true));
    }

    [TestMethod]
    public void Parse_InvalidSingleCharSwitch_ReturnsError()
    {
        var args = ArgumentSet.Parse(Tok("/g"), Spec(flags: "B C"));
        Assert.IsNotNull(args.ErrorMessage);
        Assert.IsTrue(args.ErrorMessage.Contains("Invalid switch"));
        Assert.IsTrue(args.ErrorMessage.Contains("\"g\""));
    }

    [TestMethod]
    public void Parse_CompoundFlags_SplitsCorrectly()
    {
        var args = ArgumentSet.Parse(Tok("/Q/X"), Spec(flags: "Q X B"));
        Assert.IsNull(args.ErrorMessage);
        Assert.IsTrue(args.GetFlagValue('Q'));
        Assert.IsTrue(args.GetFlagValue('X'));
        Assert.IsFalse(args.GetFlagValue('B'));
    }

    [TestMethod]
    public void Parse_SingleCharFlag_Q_RecognizedAsFlag()
    {
        var args = ArgumentSet.Parse(Tok("/Q"), Spec(flags: "Q B"));
        Assert.IsNull(args.ErrorMessage, $"Should not have error, got: {args.ErrorMessage}");
        Assert.IsTrue(args.GetFlagValue('Q'));
        Assert.AreEqual(0, args.Positionals.Count, "Q should be flag, not positional");
    }

    [TestMethod]
    public void Parse_FlagAndPositional_BothRecognized()
    {
        var args = ArgumentSet.Parse(Tok("/Q", "\\windows"), Spec(flags: "Q B"));
        Assert.IsNull(args.ErrorMessage, $"Should not have error, got: {args.ErrorMessage}");
        Assert.IsTrue(args.GetFlagValue('Q'), "Q should be recognized as flag");
        Assert.AreEqual(1, args.Positionals.Count, "Should have one positional");
        Assert.AreEqual("\\windows", args.Positionals[0]);
    }

    [TestMethod]
    public void Parse_SlashQAsTextToken_WithPositional_BothRecognized()
    {
        var tokens = new List<IToken> { Token.Text("/Q"), Token.Whitespace(" "), Token.Text("\\windows") };
        var spec = Spec(flags: "Q B");
        var args = ArgumentSet.Parse(tokens, spec);
        Assert.IsNull(args.ErrorMessage, $"Should not have error, got: {args.ErrorMessage}");
        Assert.IsTrue(args.GetFlagValue('Q'), $"Q should be flag. Positionals: {string.Join(", ", args.Positionals)}");
        Assert.AreEqual(1, args.Positionals.Count, $"Should have one positional. Got: {string.Join(", ", args.Positionals)}");
    }
}
