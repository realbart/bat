using Bat.Commands;
using Bat.Execution;
using Bat.Nodes;
using Bat.Tokens;

namespace Bat.UnitTests;

[TestClass]
public class PauseCommandTests
{
    [TestMethod]
    public async Task Pause_DisplaysMessage_AndWaitsForInput()
    {
        var cmd = new PauseCommand();
        var console = new TestConsole("x\n");
        var ctx = new TestCommandContext();
        var bc = new BatchContext { Console = console, Context = ctx };

        var exitCode = await cmd.ExecuteAsync(TestArgs.For<PauseCommand>(), bc, []);

        Assert.AreEqual(0, exitCode);
        Assert.IsTrue(console.OutText.Contains("Press any key to continue . . . "));
    }

    [TestMethod]
    public async Task Pause_HelpRequest_DisplaysHelp()
    {
        var cmd = new PauseCommand();
        var console = new TestConsole();
        var ctx = new TestCommandContext();
        var bc = new BatchContext { Console = console, Context = ctx };

        var exitCode = await cmd.ExecuteAsync(TestArgs.For<PauseCommand>(Token.Text("/?")), bc, []);

        Assert.AreEqual(0, exitCode);
        Assert.IsTrue(console.OutLines[0].Contains("Suspends processing"));
    }
}

[TestClass]
public class TitleCommandTests
{
    [TestMethod]
    public async Task Title_SetsWindowTitle()
    {
        var cmd = new TitleCommand();
        var console = new TestConsole();
        var ctx = new TestCommandContext();
        var bc = new BatchContext { Console = console, Context = ctx };

        var args = ArgumentSet.ParseString("My Window Title", ArgumentSpec.Empty);
        var exitCode = await cmd.ExecuteAsync(args, bc, []);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("My Window Title", System.Console.Title);
    }

    [TestMethod]
    public async Task Title_EmptyString_DoesNothing()
    {
        var cmd = new TitleCommand();
        var console = new TestConsole();
        var ctx = new TestCommandContext();
        var bc = new BatchContext { Console = console, Context = ctx };

        var originalTitle = System.Console.Title;
        var exitCode = await cmd.ExecuteAsync(TestArgs.For<TitleCommand>(), bc, []);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(originalTitle, System.Console.Title);
    }

    [TestMethod]
    public async Task Title_HelpRequest_DisplaysHelp()
    {
        var cmd = new TitleCommand();
        var console = new TestConsole();
        var ctx = new TestCommandContext();
        var bc = new BatchContext { Console = console, Context = ctx };

        var exitCode = await cmd.ExecuteAsync(TestArgs.For<TitleCommand>(Token.Text("/?")), bc, []);

        Assert.AreEqual(0, exitCode);
        Assert.IsTrue(console.OutLines[0].Contains("Sets the window title"));
    }
}
