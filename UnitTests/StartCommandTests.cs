using Bat.Commands;
using Bat.Execution;
using Bat.Tokens;

namespace Bat.UnitTests;

[TestClass]
public class StartCommandTests
{
    private static (StartCommand cmd, TestConsole console, BatchContext bc, TestCommandContext ctx) Setup()
    {
        var cmd = new StartCommand();
        var console = new TestConsole();
        var fs = new TestFileSystem();
        var ctx = new TestCommandContext(fs) { Console = console };
        fs.AddDir('C', []);
        ctx.SetCurrentDrive('C');
        var bc = new BatchContext { Context = ctx };
        return (cmd, console, bc, ctx);
    }

    [TestMethod]
    public async Task Start_HelpRequest_DisplaysHelp()
    {
        var (cmd, console, bc, _) = Setup();
        var result = await cmd.ExecuteAsync(TestArgs.For<StartCommand>(Token.Text("/?")), bc, []);
        Assert.AreEqual(0, result);
        Assert.IsTrue(console.OutText.Contains("START"));
        Assert.IsTrue(console.OutText.Contains("/WAIT"));
        Assert.IsTrue(console.OutText.Contains("/B"));
        Assert.IsTrue(console.OutText.Contains("/D"));
        Assert.IsTrue(console.OutText.Contains("/MIN"));
    }

    [TestMethod]
    public async Task Start_ParsesTitle_FromFirstQuotedArg()
    {
        // START "My Title" notepad.exe → title = "My Title", command = notepad.exe
        var (cmd, console, bc, _) = Setup();
        var args = StartCommand.ParseStartArguments([
            "\"My Title\"", "notepad.exe"
        ]);
        Assert.AreEqual("My Title", args.Title);
        Assert.AreEqual("notepad.exe", args.Command);
    }

    [TestMethod]
    public async Task Start_ParsesFlags_B()
    {
        var args = StartCommand.ParseStartArguments(["/B", "notepad.exe"]);
        Assert.IsTrue(args.Background);
        Assert.AreEqual("notepad.exe", args.Command);
    }

    [TestMethod]
    public async Task Start_ParsesFlags_Wait()
    {
        var args = StartCommand.ParseStartArguments(["/WAIT", "notepad.exe"]);
        Assert.IsTrue(args.Wait);
        Assert.AreEqual("notepad.exe", args.Command);
    }

    [TestMethod]
    public async Task Start_ParsesFlags_Min()
    {
        var args = StartCommand.ParseStartArguments(["/MIN", "notepad.exe"]);
        Assert.AreEqual(StartWindowStyle.Minimized, args.WindowStyle);
        Assert.AreEqual("notepad.exe", args.Command);
    }

    [TestMethod]
    public async Task Start_ParsesFlags_Max()
    {
        var args = StartCommand.ParseStartArguments(["/MAX", "notepad.exe"]);
        Assert.AreEqual(StartWindowStyle.Maximized, args.WindowStyle);
        Assert.AreEqual("notepad.exe", args.Command);
    }

    [TestMethod]
    public async Task Start_ParsesFlags_D_SetsWorkingDirectory()
    {
        var args = StartCommand.ParseStartArguments(["/D", "C:\\Users", "notepad.exe"]);
        Assert.AreEqual("C:\\Users", args.WorkingDirectory);
        Assert.AreEqual("notepad.exe", args.Command);
    }

    [TestMethod]
    public async Task Start_ParsesFlags_I()
    {
        var args = StartCommand.ParseStartArguments(["/I", "notepad.exe"]);
        Assert.IsTrue(args.NewEnvironment);
        Assert.AreEqual("notepad.exe", args.Command);
    }

    [TestMethod]
    public async Task Start_ParsesPriority_Low()
    {
        var args = StartCommand.ParseStartArguments(["/LOW", "notepad.exe"]);
        Assert.AreEqual(StartPriority.Low, args.Priority);
    }

    [TestMethod]
    public async Task Start_ParsesPriority_High()
    {
        var args = StartCommand.ParseStartArguments(["/HIGH", "notepad.exe"]);
        Assert.AreEqual(StartPriority.High, args.Priority);
    }

    [TestMethod]
    public async Task Start_ParsesPriority_RealTime()
    {
        var args = StartCommand.ParseStartArguments(["/REALTIME", "notepad.exe"]);
        Assert.AreEqual(StartPriority.RealTime, args.Priority);
    }

    [TestMethod]
    public async Task Start_ParsesPriority_AboveNormal()
    {
        var args = StartCommand.ParseStartArguments(["/ABOVENORMAL", "notepad.exe"]);
        Assert.AreEqual(StartPriority.AboveNormal, args.Priority);
    }

    [TestMethod]
    public async Task Start_ParsesPriority_BelowNormal()
    {
        var args = StartCommand.ParseStartArguments(["/BELOWNORMAL", "notepad.exe"]);
        Assert.AreEqual(StartPriority.BelowNormal, args.Priority);
    }

    [TestMethod]
    public async Task Start_ParsesCommandArguments()
    {
        var args = StartCommand.ParseStartArguments(["notepad.exe", "file.txt", "/S"]);
        Assert.AreEqual("notepad.exe", args.Command);
        Assert.AreEqual("file.txt /S", args.Arguments);
    }

    [TestMethod]
    public async Task Start_TitleAndFlags_Combined()
    {
        var args = StartCommand.ParseStartArguments([
            "\"Window\"", "/MIN", "/WAIT", "/D", "C:\\Temp", "cmd.exe", "/C", "echo", "hi"
        ]);
        Assert.AreEqual("Window", args.Title);
        Assert.AreEqual(StartWindowStyle.Minimized, args.WindowStyle);
        Assert.IsTrue(args.Wait);
        Assert.AreEqual("C:\\Temp", args.WorkingDirectory);
        Assert.AreEqual("cmd.exe", args.Command);
        Assert.AreEqual("/C echo hi", args.Arguments);
    }

    [TestMethod]
    public async Task Start_NoCommand_IsEmpty()
    {
        var args = StartCommand.ParseStartArguments([]);
        Assert.IsNull(args.Command);
    }

    [TestMethod]
    public async Task Start_NormalPriority_Flag()
    {
        // /NORMAL sets both priority and window style in CMD
        var args = StartCommand.ParseStartArguments(["/NORMAL", "notepad.exe"]);
        // /NORMAL as priority
        Assert.AreEqual(StartPriority.Normal, args.Priority);
    }

    [TestMethod]
    public void IsCmdLaunch_Cmd_ReturnsTrue()
    {
        Assert.IsTrue(StartCommand.IsCmdLaunch("cmd"));
        Assert.IsTrue(StartCommand.IsCmdLaunch("CMD"));
        Assert.IsTrue(StartCommand.IsCmdLaunch("cmd.exe"));
        Assert.IsTrue(StartCommand.IsCmdLaunch("CMD.EXE"));
    }

    [TestMethod]
    public void IsCmdLaunch_Bat_ReturnsTrue()
    {
        Assert.IsTrue(StartCommand.IsCmdLaunch("bat"));
        Assert.IsTrue(StartCommand.IsCmdLaunch("BAT"));
        Assert.IsTrue(StartCommand.IsCmdLaunch("bat.exe"));
    }

    [TestMethod]
    public void IsCmdLaunch_OtherCommand_ReturnsFalse()
    {
        Assert.IsFalse(StartCommand.IsCmdLaunch("notepad"));
        Assert.IsFalse(StartCommand.IsCmdLaunch("notepad.exe"));
        Assert.IsFalse(StartCommand.IsCmdLaunch("explorer.exe"));
    }

    [TestMethod]
    public void IsCmdLaunch_CmdWithPath_ReturnsTrue()
    {
        Assert.IsTrue(StartCommand.IsCmdLaunch(@"C:\Windows\System32\cmd.exe"));
        Assert.IsTrue(StartCommand.IsCmdLaunch(@"Z:\Windows\cmd.exe"));
    }
}
