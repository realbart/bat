using Bat.Execution;
using Context;

namespace Bat.UnitTests;

[TestClass]
public class PromptExpanderTests
{
    // ── helpers ─────────────────────────────────────────────────────────────

    private static TestContext CreateContext(char drive = 'C', string[]? path = null)
    {
        var ctx = new TestContext(drive, path ?? []);
        return ctx;
    }

    // Minimal IContext implementation used only in these tests
    private class TestContext(char drive, string[] path) : IContext
    {
        public IConsole Console { get; set; } = new TestConsole();
        public char CurrentDrive { get; } = drive;
        public string[] CurrentPath { get; } = path;
        public string CurrentPathDisplayName =>
            CurrentPath.Length == 0
                ? $"{CurrentDrive}:\\"
                : $"{CurrentDrive}:\\{string.Join("\\", CurrentPath)}";
        public IDictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>();
        public IDictionary<string, string> Macros { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<string> CommandHistory { get; } = [];
        public int HistorySize { get; set; } = 50;
        public int ErrorCode { get; set; }
        public IFileSystem FileSystem => null!;
        public object? CurrentBatch { get; set; }
        public bool EchoEnabled { get; set; } = true;
        public bool DelayedExpansion { get; set; }
        public bool ExtensionsEnabled { get; set; } = true;
        public string PromptFormat { get; set; } = "$P$G";
        public System.Globalization.CultureInfo FileCulture { get; } = System.Globalization.CultureInfo.CurrentCulture;
        public Stack<(char Drive, string[] Path)> DirectoryStack { get; } = new();
        public void SetPath(char drive, string[] path) { }
        public void SetCurrentDrive(char drive) { }
        public string[] GetPathForDrive(char drive) => [];
        public IReadOnlyDictionary<char, string[]> GetAllDrivePaths() => new Dictionary<char, string[]>();
        public void RestoreAllDrivePaths(Dictionary<char, string[]> paths) { }
        public (bool Found, string NativePath) TryGetCurrentFolder() => (false, "");
        public void ApplySnapshot(IContext other) { }
        public IContext StartNew(IConsole? console = null) => this;
    }

    // ── tests ────────────────────────────────────────────────────────────────

    [TestMethod]
    [Timeout(4000)]
    public void Expand_DefaultPG_ShowsPathAndGT()
    {
        var ctx = CreateContext('C', ["Users", "Bart"]);
        ctx.EnvironmentVariables["PROMPT"] = "$P$G";

        Assert.AreEqual("C:\\Users\\Bart>", PromptExpander.Expand(ctx));
    }

    [TestMethod]
    [Timeout(4000)]
    public void Expand_Multiline_Works()
    {
        var ctx = CreateContext('C', ["Users"]);
        ctx.EnvironmentVariables["PROMPT"] = "$P$_$G";

        Assert.AreEqual($"C:\\Users\r\n>", PromptExpander.Expand(ctx));
    }

    [TestMethod]
    [Timeout(4000)]
    public void Expand_DriveOnly()
    {
        var ctx = CreateContext('D', ["Projects"]);
        ctx.EnvironmentVariables["PROMPT"] = "$N$G";

        Assert.AreEqual("D:>", PromptExpander.Expand(ctx));
    }

    [TestMethod]
    [Timeout(4000)]
    public void Expand_DateTime_Works()
    {
        var ctx = CreateContext();
        ctx.EnvironmentVariables["PROMPT"] = "$D $T";

        var prompt = PromptExpander.Expand(ctx);

        // Date separator is locale-dependent (/ on en-US, . on nl-NL, etc.) — accept any non-digit
        StringAssert.Matches(prompt, new(@"^\S+ \d{2}.\d{2}.\d{4} \d{2}:\d{2}:\d{2}\.\d{2}$"));
    }

    [TestMethod]
    [Timeout(4000)]
    public void Expand_PushDepth_ShowsPlusses()
    {
        var ctx = CreateContext();
        ctx.DirectoryStack.Push(('C', ["Temp"]));
        ctx.DirectoryStack.Push(('C', ["Windows"]));
        ctx.EnvironmentVariables["PROMPT"] = "$+$G";

        Assert.AreEqual("++>", PromptExpander.Expand(ctx));
    }

    [TestMethod]
    [Timeout(4000)]
    public void Expand_PushDepthZero_ShowsNoPlus()
    {
        var ctx = CreateContext();
        ctx.EnvironmentVariables["PROMPT"] = "$+$G";

        Assert.AreEqual(">", PromptExpander.Expand(ctx));
    }

    [TestMethod]
    [Timeout(4000)]
    public void Expand_EscapedDollar()
    {
        var ctx = CreateContext();
        ctx.EnvironmentVariables["PROMPT"] = "Cost: $$5$G";

        Assert.AreEqual("Cost: $5>", PromptExpander.Expand(ctx));
    }

    [TestMethod]
    [Timeout(4000)]
    public void Expand_UnknownCode_RemainsLiteral()
    {
        var ctx = CreateContext('C', []);
        ctx.EnvironmentVariables["PROMPT"] = "$P$X$G";

        Assert.AreEqual("C:\\$X>", PromptExpander.Expand(ctx));
    }

    [TestMethod]
    [Timeout(4000)]
    public void Expand_NoPromptVariable_UsesDefault()
    {
        var ctx = CreateContext('C', []);
        // No PROMPT variable set

        Assert.AreEqual("C:\\>", PromptExpander.Expand(ctx));
    }

    [TestMethod]
    [Timeout(4000)]
    public void Expand_LiteralText_PassesThrough()
    {
        var ctx = CreateContext();
        ctx.EnvironmentVariables["PROMPT"] = "[bat] ";

        Assert.AreEqual("[bat] ", PromptExpander.Expand(ctx));
    }

    [TestMethod]
    [Timeout(4000)]
    public void Expand_AllSingleCharSymbols()
    {
        var ctx = CreateContext();
        ctx.EnvironmentVariables["PROMPT"] = "$A$B$C$F$G$L$Q$S";

        Assert.AreEqual("&|()><= ", PromptExpander.Expand(ctx)); // A=& B=| C=( F=) G=> L=< Q== S=space
    }

    [TestMethod]
    [Timeout(4000)]
    public void Expand_TrailingDollarIsLiteral()
    {
        var ctx = CreateContext();
        ctx.EnvironmentVariables["PROMPT"] = "test$";

        Assert.AreEqual("test$", PromptExpander.Expand(ctx));
    }

    [TestMethod]
    [Timeout(4000)]
    public void Expand_EmptyPromptVariable_UsesDefault()
    {
        var ctx = CreateContext('C', []);
        ctx.EnvironmentVariables["PROMPT"] = "";

        Assert.AreEqual("C:\\>", PromptExpander.Expand(ctx));
    }
}
