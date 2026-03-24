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
        for (int i = 0; i < parts.Length; i++)
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
        Assert.IsTrue(args.HasFlag('B'));
        Assert.IsTrue(args.HasFlag("B"));
        Assert.IsFalse(args.HasFlag('W'));
    }

    [TestMethod]
    public void Parse_Flag_CaseInsensitive()
    {
        var args = ArgumentSet.Parse(Tok("/b"), Spec(flags: "B"));
        Assert.IsTrue(args.HasFlag('B'));
        Assert.IsTrue(args.HasFlag('b'));
    }

    [TestMethod]
    public void Parse_Flag_DashPrefixRecognized()
    {
        var args = ArgumentSet.Parse(Tok("-B"), Spec(flags: "B"));
        Assert.IsTrue(args.HasFlag('B'));
    }

    [TestMethod]
    public void Parse_MultipleFlags_AllRecognized()
    {
        var args = ArgumentSet.Parse(Tok("/B", "/W", "/S"), Spec(flags: "B W S"));
        Assert.IsTrue(args.HasFlag('B'));
        Assert.IsTrue(args.HasFlag('W'));
        Assert.IsTrue(args.HasFlag('S'));
        Assert.IsFalse(args.HasFlag('L'));
    }

    [TestMethod]
    public void Parse_UnknownSwitch_TreatedAsPositional()
    {
        var args = ArgumentSet.Parse(Tok("/Z"), Spec(flags: "B"));
        Assert.IsFalse(args.HasFlag('Z'));
        Assert.AreEqual("/Z", args.Positionals[0]);
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
        Assert.IsTrue(args.HasFlag('B'));
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
    public void Parse_Option_GetValue_ThrowsOnMultiple()
    {
        var tokens = new List<IToken> { Token.Text("/A:D"), Token.Whitespace(" "), Token.Text("/A:H") };
        var args = ArgumentSet.Parse(tokens, Spec(options: "A"));
        bool threw = false;
        try { args.GetValue("A"); }
        catch (InvalidOperationException) { threw = true; }
        Assert.IsTrue(threw, "Expected InvalidOperationException");
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
        Assert.IsTrue(args.HasFlag('B'));
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
        Assert.IsFalse(args.HasFlag('B'));
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
}
