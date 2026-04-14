using Bat.Commands;
using Bat.Console;
using Bat.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bat.UnitTests;

[TestClass]
public class SetLocalEndLocalTests
{
    [TestMethod]
    public async Task Setlocal_SnapshotsEnvironmentVariables()
    {
        var h = new TestHarness();
        h.Context.EnvironmentVariables["X"] = "before";

        await h.Execute("setlocal");
        h.Context.EnvironmentVariables["X"] = "inside";
        await h.Execute("endlocal");

        Assert.AreEqual("before", h.Context.EnvironmentVariables["X"]);
    }

    [TestMethod]
    public async Task Endlocal_OnEmptyStack_IsNoop()
    {
        var h = new TestHarness();
        h.Context.EnvironmentVariables["X"] = "original";

        await h.Execute("endlocal");

        Assert.AreEqual("original", h.Context.EnvironmentVariables["X"]);
    }

    [TestMethod]
    public async Task Setlocal_NestedSetlocal_RestoresAllLevels()
    {
        var h = new TestHarness();
        h.Context.EnvironmentVariables["X"] = "outer";

        await h.Execute("setlocal");
        h.Context.EnvironmentVariables["X"] = "level1";

        await h.Execute("setlocal");
        h.Context.EnvironmentVariables["X"] = "level2";

        await h.Execute("endlocal");
        Assert.AreEqual("level1", h.Context.EnvironmentVariables["X"]);

        await h.Execute("endlocal");
        Assert.AreEqual("outer", h.Context.EnvironmentVariables["X"]);
    }

    [TestMethod]
    public async Task Setlocal_EnableDelayedExpansion_SetsFlag()
    {
        var h = new TestHarness();
        Assert.IsFalse(h.Context.DelayedExpansion);

        await h.Execute("setlocal EnableDelayedExpansion");

        Assert.IsTrue(h.Context.DelayedExpansion);
    }

    [TestMethod]
    public async Task Endlocal_RestoresDelayedExpansionFlag()
    {
        var h = new TestHarness();
        h.Context.DelayedExpansion = false;

        await h.Execute("setlocal EnableDelayedExpansion");
        Assert.IsTrue(h.Context.DelayedExpansion);

        await h.Execute("endlocal");
        Assert.IsFalse(h.Context.DelayedExpansion);
    }

    [TestMethod]
    public async Task Setlocal_EnableExtensions_SetsFlag()
    {
        var h = new TestHarness();
        h.Context.ExtensionsEnabled = false;

        await h.Execute("setlocal EnableExtensions");

        Assert.IsTrue(h.Context.ExtensionsEnabled);
    }

    [TestMethod]
    public async Task Setlocal_DisableExtensions_ClearsFlag()
    {
        var h = new TestHarness();
        h.Context.ExtensionsEnabled = true;

        await h.Execute("setlocal DisableExtensions");

        Assert.IsFalse(h.Context.ExtensionsEnabled);
    }

    [TestMethod]
    public async Task Setlocal_RestoresPerDrivePaths()
    {
        var h = new TestHarness();
        h.Context.SetPath('C', ["Users"]);

        await h.Execute("setlocal");
        h.Context.SetPath('C', ["Windows"]);
        await h.Execute("endlocal");

        CollectionAssert.AreEqual(new[] { "Users" }, h.Context.GetPathForDrive('C'));
    }

    [TestMethod]
    public async Task Setlocal_RemovedVariable_IsRestoredByEndlocal()
    {
        var h = new TestHarness();
        h.Context.EnvironmentVariables["Y"] = "present";

        await h.Execute("setlocal");
        h.Context.EnvironmentVariables.Remove("Y");
        Assert.IsFalse(h.Context.EnvironmentVariables.ContainsKey("Y"));

        await h.Execute("endlocal");
        Assert.AreEqual("present", h.Context.EnvironmentVariables["Y"]);
    }

    [TestMethod]
    public async Task BatchExit_UnwindsSetLocalStack()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddBatchFile('Z', [], "test.bat", "setlocal\r\nset X=inside\r\nexit /b");

        var console = new TestConsole();
        var ctx = new TestCommandContext(fs) { Console = console };
        ctx.SetCurrentDrive('Z');
        ctx.EnvironmentVariables["X"] = "before";
        var bc = new BatchContext { Context = ctx };

        var executor = new BatchExecutor(console);
        await executor.ExecuteAsync("Z:\\test.bat", "", bc, []);

        Assert.AreEqual("before", ctx.EnvironmentVariables["X"]);
    }

    [TestMethod]
    public async Task Setlocal_ValidArgument_ReturnsZero()
    {
        var h = new TestHarness();
        await h.Execute("setlocal EnableDelayedExpansion");
        Assert.AreEqual(0, h.Context.ErrorCode);
    }

    [TestMethod]
    public async Task Setlocal_InvalidArgument_ReturnsOne()
    {
        var h = new TestHarness();
        await h.Execute("setlocal BOGUS");
        Assert.AreEqual(1, h.Context.ErrorCode);
    }

    [TestMethod]
    public async Task Setlocal_NoArgument_ReturnsZero()
    {
        var h = new TestHarness();
        await h.Execute("setlocal");
        Assert.AreEqual(0, h.Context.ErrorCode);
    }

    [TestMethod]
    public async Task Setlocal_Help_ShowsHelpText()
    {
        var h = new TestHarness();
        await h.Execute("setlocal /?");
        StringAssert.Contains(h.Console.OutText, "SETLOCAL");
        StringAssert.Contains(h.Console.OutText, "ENABLEDELAYEDEXPANSION");
    }

    [TestMethod]
    public async Task Endlocal_Help_ShowsHelpText()
    {
        var h = new TestHarness();
        await h.Execute("endlocal /?");
        StringAssert.Contains(h.Console.OutText, "ENDLOCAL");
    }

    [TestMethod]
    public async Task Endlocal_RestoresCurrentDrive()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddDir('D', []);
        fs.AddBatchFile('Z', [], "test.bat",
            "setlocal\r\nD:\r\nendlocal\r\nexit /b");

        var console = new TestConsole();
        var ctx = new TestCommandContext(fs) { Console = console };
        ctx.SetCurrentDrive('Z');
        var bc = new BatchContext { Context = ctx };

        var executor = new BatchExecutor(console);
        await executor.ExecuteAsync("Z:\\test.bat", "", bc, []);

        Assert.AreEqual('Z', ctx.CurrentDrive);
    }

    [TestMethod]
    public async Task Setlocal_ExtensionsDisabled_CanReenableExtensions()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddBatchFile('Z', [], "test.bat",
            "setlocal DisableExtensions\r\nsetlocal EnableExtensions\r\nexit /b");

        var console = new TestConsole();
        var ctx = new TestCommandContext(fs) { Console = console };
        ctx.SetCurrentDrive('Z');
        var bc = new BatchContext { Context = ctx };

        var executor = new BatchExecutor(console);
        await executor.ExecuteAsync("Z:\\test.bat", "", bc, []);

        // SETLOCAL EnableExtensions works even after DisableExtensions
        Assert.IsTrue(ctx.ExtensionsEnabled);
    }
}


