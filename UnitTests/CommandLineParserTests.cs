using Bat.Context;

namespace Bat.UnitTests;

[TestClass]
public class CommandLineParserTests
{
    private static BatArgumentParser WinParser() => new(directorySeparator: '\\');
    private static BatArgumentParser UnixParser() => new(directorySeparator: '/');

    private static BatArguments ParseWindows(params string[] args) => WinParser().Parse(args);
    private static BatArguments ParseUnix(params string[] args) => UnixParser().Parse(args);

    // ── Test 1: Modusdetectie ────────────────────────────────────────────────

    [TestMethod]
    public void ParseMode_WindowsSeparator_IsWindowsMode()
    {
        var parser = new BatArgumentParser(directorySeparator: '\\');
        var args = parser.Parse(["/C", "echo hello"]);
        Assert.AreEqual(BatMode.Windows, args.Mode);
    }

    [TestMethod]
    public void ParseMode_UnixSeparator_IsUnixMode()
    {
        var parser = new BatArgumentParser(directorySeparator: '/');
        var args = parser.Parse(["-c", "echo hello"]);
        Assert.AreEqual(BatMode.Unix, args.Mode);
    }

    // ── Test 2: /C en /K ──────────────────────────────────────────────────────

    [TestMethod]
    public void Parse_SlashC_SetsCommandAndTerminate()
    {
        var args = ParseWindows("/C", "echo hello");
        Assert.AreEqual("echo hello", args.Command);
        Assert.AreEqual(BatExitBehavior.TerminateAfterCommand, args.ExitBehavior);
    }

    [TestMethod]
    public void Parse_SlashK_SetsCommandAndKeepAlive()
    {
        var args = ParseWindows("/K", "echo hello");
        Assert.AreEqual("echo hello", args.Command);
        Assert.AreEqual(BatExitBehavior.KeepAliveAfterCommand, args.ExitBehavior);
    }

    [TestMethod]
    public void Parse_NoCommand_NoFlagC_StartsRepl()
    {
        var args = ParseWindows();
        Assert.IsNull(args.Command);
        Assert.AreEqual(BatExitBehavior.Repl, args.ExitBehavior);
    }

    [TestMethod]
    public void Parse_PositionalWithoutCK_ImpliesTerminate()
    {
        var args = ParseWindows("script.bat");
        Assert.AreEqual("script.bat", args.BatchFile);
        Assert.AreEqual(BatExitBehavior.TerminateAfterCommand, args.ExitBehavior);
    }

    // ── Test 3: Gecombineerde Unix-vlaggen ───────────────────────────────────

    [TestMethod]
    public void Parse_Unix_CombinedFlags_ParsesAll()
    {
        var args = ParseUnix("-cq", "echo hello");
        Assert.AreEqual(BatExitBehavior.TerminateAfterCommand, args.ExitBehavior);
        Assert.IsFalse(args.EchoEnabled);
    }

    [TestMethod]
    public void Parse_Unix_CombinedFlags_ThreeChars()
    {
        var args = ParseUnix("-kqa");
        Assert.AreEqual(BatExitBehavior.KeepAliveAfterCommand, args.ExitBehavior);
        Assert.IsFalse(args.EchoEnabled);
        Assert.AreEqual(OutputEncoding.Ansi, args.OutputEncoding);
    }

    // ── Test 4: /M drive-mapping ─────────────────────────────────────────────

    [TestMethod]
    public void Parse_MultipleM_CreatesMappings()
    {
        var args = ParseUnix("-m:c=/home/user,p=/mnt/projects");
        Assert.AreEqual(2, args.DriveMappings!.Count);
        Assert.AreEqual("/home/user", args.DriveMappings['C']);
        Assert.AreEqual("/mnt/projects", args.DriveMappings['P']);
    }

    [TestMethod]
    public void Parse_M_ColonSyntax_CreatesMappings()
    {
        var args = ParseWindows("/M:c=C:\\Temp,d=C:\\Users");
        Assert.AreEqual(2, args.DriveMappings!.Count);
        Assert.AreEqual("C:\\Temp", args.DriveMappings['C']);
        Assert.AreEqual("C:\\Users", args.DriveMappings['D']);
    }

    [TestMethod]
    public void Parse_NoM_UsesDefaultMapping()
    {
        var argsWin = ParseWindows();
        Assert.IsNull(argsWin.DriveMappings);

        var argsUnix = ParseUnix();
        Assert.IsNull(argsUnix.DriveMappings);
    }

    // ── Test 5: /V:ON zet DelayedExpansion ───────────────────────────────────

    [TestMethod]
    public void Parse_VOn_SetsDelayedExpansion()
    {
        var args = ParseWindows("/V:ON");
        Assert.IsTrue(args.DelayedExpansion);
    }

    [TestMethod]
    public void Parse_VOff_ClearsDelayedExpansion()
    {
        var args = ParseWindows("/V:OFF");
        Assert.IsFalse(args.DelayedExpansion);
    }

    // ── Test 6: /Q schakelt echo uit ─────────────────────────────────────────

    [TestMethod]
    public void Parse_Q_DisablesEcho()
    {
        var args = ParseWindows("/Q");
        Assert.IsFalse(args.EchoEnabled);
    }

    // ── Test 7: Standaardwaarden zonder vlaggen ──────────────────────────────

    [TestMethod]
    public void Parse_NoFlags_DefaultValues()
    {
        var args = ParseWindows();
        Assert.IsTrue(args.EchoEnabled);
        Assert.IsFalse(args.DelayedExpansion);
        Assert.IsTrue(args.ExtensionsEnabled);
        Assert.IsNull(args.Command);
        Assert.AreEqual(BatExitBehavior.Repl, args.ExitBehavior);
    }

    // ── Test 7a: Help-vlaggen ────────────────────────────────────────────────

    [TestMethod]
    public void Parse_Windows_SlashQuestion_SetsShowHelp()
    {
        var args = ParseWindows("/?");
        Assert.IsTrue(args.ShowHelp);
    }

    [TestMethod]
    public void Parse_Unix_MinusH_SetsShowHelp()
    {
        var args = ParseUnix("-h");
        Assert.IsTrue(args.ShowHelp);
    }

    [TestMethod]
    public void Parse_Unix_DoubleDashHelp_SetsShowHelp()
    {
        var args = ParseUnix("--help");
        Assert.IsTrue(args.ShowHelp);
    }

    // ── Test 7b: Positional argument (batch bestand) ─────────────────────────

    [TestMethod]
    public void Parse_PositionalArgument_SetsBatchFile()
    {
        var args = ParseWindows("script.bat");
        Assert.AreEqual("script.bat", args.BatchFile);
        Assert.AreEqual(BatExitBehavior.TerminateAfterCommand, args.ExitBehavior);
    }

    [TestMethod]
    public void Parse_PositionalArgument_WithFlags_ParsesBoth()
    {
        var args = ParseWindows("/Q", "script.bat");
        Assert.AreEqual("script.bat", args.BatchFile);
        Assert.IsFalse(args.EchoEnabled);
    }

    // ── Test 8: E, F, A, U flags ─────────────────────────────────────────────

    [TestMethod]
    public void Parse_EOn_EnablesExtensions()
    {
        var args = ParseWindows("/E:ON");
        Assert.IsTrue(args.ExtensionsEnabled);
    }

    [TestMethod]
    public void Parse_EOff_DisablesExtensions()
    {
        var args = ParseWindows("/E:OFF");
        Assert.IsFalse(args.ExtensionsEnabled);
    }

    [TestMethod]
    public void Parse_FOn_EnablesFileCompletion()
    {
        var args = ParseWindows("/F:ON");
        Assert.IsTrue(args.FilenameCompletion);
    }

    [TestMethod]
    public void Parse_FOff_DisablesFileCompletion()
    {
        var args = ParseWindows("/F:OFF");
        Assert.IsFalse(args.FilenameCompletion);
    }

    [TestMethod]
    public void Parse_A_SetsAnsiEncoding()
    {
        var args = ParseWindows("/A");
        Assert.AreEqual(OutputEncoding.Ansi, args.OutputEncoding);
    }

    [TestMethod]
    public void Parse_U_SetsUnicodeEncoding()
    {
        var args = ParseWindows("/U");
        Assert.AreEqual(OutputEncoding.Unicode, args.OutputEncoding);
    }

    // ── Startup banner flag ──────────────────────────────────────────────────

    [TestMethod]
    public void Parse_N_SuppressesBanner()
    {
        var args = ParseWindows("/N");
        Assert.IsTrue(args.SuppressBanner);
    }

    [TestMethod]
    public void Parse_Unix_N_SuppressesBanner()
    {
        var args = ParseUnix("-n");
        Assert.IsTrue(args.SuppressBanner);
    }

    [TestMethod]
    public void Parse_Unix_NoLogo_SuppressesBanner()
    {
        var args = ParseUnix("--nologo");
        Assert.IsTrue(args.SuppressBanner);
    }

    [TestMethod]
    public void Parse_Default_ShowsBanner()
    {
        var args = ParseWindows();
        Assert.IsFalse(args.SuppressBanner);
    }

    [TestMethod]
    public void Parser_UnixMode_DetectsCorrectly()
    {
        var parser = new BatArgumentParser(directorySeparator: '/');
        var args = parser.Parse(["-h"]);
        Assert.AreEqual(BatMode.Unix, args.Mode);
        Assert.IsTrue(args.ShowHelp);
    }

    [TestMethod]
    public void Parser_WindowsMode_DetectsCorrectly()
    {
        var parser = new BatArgumentParser(directorySeparator: '\\');
        var args = parser.Parse(["/?"]); 
        Assert.AreEqual(BatMode.Windows, args.Mode);
        Assert.IsTrue(args.ShowHelp);
    }
}
