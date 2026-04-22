using Bat.Context;

namespace Bat.UnitTests;

[TestClass]
public class BatArgumentParserTests
{
    private static BatArguments Parse(params string[] args) =>
        new BatArgumentParser('\\').Parse(args);

    private static BatArguments ParseUnix(params string[] args) =>
        new BatArgumentParser('/').Parse(args);

    // ── /M comma-separated ────────────────────────────────────────────────────

    [TestMethod]
    public void Parse_M_ColonSyntax_CreatesMappings()
    {
        var args = Parse("/M:c=C:\\Projects,d=D:\\Data");
        Assert.IsNotNull(args.DriveMappings);
        Assert.AreEqual(@"C:\Projects", args.DriveMappings['C']);
        Assert.AreEqual(@"D:\Data", args.DriveMappings['D']);
    }

    [TestMethod]
    public void Parse_M_SingleMapping_Works()
    {
        var args = Parse("/M:z=C:\\");
        Assert.AreEqual(@"C:\", args.DriveMappings!['Z']);
    }

    [TestMethod]
    public void Parse_M_LowercaseDriveLetter_StoredAsUppercase()
    {
        var args = Parse("/M:c=C:\\Foo");
        Assert.IsTrue(args.DriveMappings!.ContainsKey('C'));
    }

    [TestMethod]
    public void Parse_MultipleM_CreatesMappings()
    {
        var args = Parse("/M:c=C:\\Foo", "/M:d=D:\\Bar");
        Assert.AreEqual(@"C:\Foo", args.DriveMappings!['C']);
        Assert.AreEqual(@"D:\Bar", args.DriveMappings!['D']);
    }

    [TestMethod]
    public void Parse_MultipleM_MixedWithOtherFlags()
    {
        var args = Parse("/N", "/M:c=C:\\Foo", "/M:d=D:\\Bar", "/Q");
        Assert.IsTrue(args.SuppressBanner);
        Assert.IsFalse(args.EchoEnabled);
        Assert.AreEqual(@"C:\Foo", args.DriveMappings!['C']);
        Assert.AreEqual(@"D:\Bar", args.DriveMappings!['D']);
    }

    [TestMethod]
    public void Parse_M_QuotedPath_Windows()
    {
        var args = Parse(@"/M:c=""C:\Foo Bar""");
        Assert.AreEqual(@"C:\Foo Bar", args.DriveMappings!['C']);
    }

    [TestMethod]
    public void Parse_M_QuotedPathWithComma_Windows()
    {
        var args = Parse(@"/M:c=""C:\Foo,Bar"",d=D:\Baz");
        Assert.AreEqual(@"C:\Foo,Bar", args.DriveMappings!['C']);
        Assert.AreEqual(@"D:\Baz", args.DriveMappings!['D']);
    }

    [TestMethod]
    public void Parse_M_LaterOverrideEarlier()
    {
        var args = Parse("/M:c=C:\\Old", "/M:c=C:\\New");
        Assert.AreEqual(@"C:\New", args.DriveMappings!['C']);
    }

    // ── Unix -M ───────────────────────────────────────────────────────────────

    [TestMethod]
    public void ParseUnix_M_ColonSyntax_CreatesMappings()
    {
        var args = ParseUnix("-M:c=/home/peter,d=/home");
        Assert.AreEqual("/home/peter", args.DriveMappings!['C']);
        Assert.AreEqual("/home", args.DriveMappings!['D']);
    }

    [TestMethod]
    public void ParseUnix_M_MultipleSeparateFlags()
    {
        var args = ParseUnix("-M:c=/home/peter", "-M:d=/home", "-M:e=/home/bart");
        Assert.AreEqual("/home/peter", args.DriveMappings!['C']);
        Assert.AreEqual("/home", args.DriveMappings!['D']);
        Assert.AreEqual("/home/bart", args.DriveMappings!['E']);
    }

    [TestMethod]
    public void ParseUnix_M_SingleQuotedPath()
    {
        var args = ParseUnix("-M:c='/home/my path'");
        Assert.AreEqual("/home/my path", args.DriveMappings!['C']);
    }

    [TestMethod]
    public void ParseUnix_M_DoubleQuotedPath()
    {
        var args = ParseUnix("-M:c=\"/home/my path\"");
        Assert.AreEqual("/home/my path", args.DriveMappings!['C']);
    }

    [TestMethod]
    public void ParseUnix_M_QuotedPathWithComma()
    {
        var args = ParseUnix("-M:c='/home/foo,bar',d=/home/baz");
        Assert.AreEqual("/home/foo,bar", args.DriveMappings!['C']);
        Assert.AreEqual("/home/baz", args.DriveMappings!['D']);
    }

    // ── No /M ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Parse_NoM_UsesDefaultMapping()
    {
        var args = Parse("/N");
        Assert.IsNull(args.DriveMappings);
    }
}
