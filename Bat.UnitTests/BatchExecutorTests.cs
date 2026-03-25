using Bat.Console;
using Bat.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bat.UnitTests;

[TestClass]
public class BatchExecutorTests
{
    [TestMethod]
    public async Task BatchExecutor_SimpleBatch_ExecutesLines()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddBatchFile('Z', [], "test.bat", "@echo off\necho Hello\necho World");

        var console = new TestConsole();
        var context = new TestCommandContext(fs);
        var bc = new BatchContext { Console = console, Context = context };

        var executor = new BatchExecutor(console);
        var exitCode = await executor.ExecuteAsync("Z:\\test.bat", "", bc, []);

        Assert.AreEqual(0, exitCode);
        Assert.IsTrue(console.OutLines.Contains("Hello"));
        Assert.IsTrue(console.OutLines.Contains("World"));
    }

    [TestMethod]
    public async Task BatchExecutor_WithParameters_ExpandsCorrectly()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddBatchFile('Z', [], "test.bat", "echo First arg: %1\necho Second arg: %2");

        var console = new TestConsole();
        var context = new TestCommandContext(fs);
        var bc = new BatchContext { Console = console, Context = context };

        var executor = new BatchExecutor(console);
        var exitCode = await executor.ExecuteAsync("Z:\\test.bat", "foo bar", bc, []);

        Assert.AreEqual(0, exitCode);
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("First arg: foo")));
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("Second arg: bar")));
    }

    [TestMethod]
    public async Task BatchExecutor_ExitCommand_StopsBatch()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddBatchFile('Z', [], "test.bat", "echo Before\nexit /b\necho After");

        var console = new TestConsole();
        var context = new TestCommandContext(fs);
        var bc = new BatchContext { Console = console, Context = context };

        var executor = new BatchExecutor(console);
        var exitCode = await executor.ExecuteAsync("Z:\\test.bat", "", bc, []);

        Assert.AreEqual(0, exitCode);
        Assert.IsTrue(console.OutLines.Contains("Before"));
        Assert.IsFalse(console.OutLines.Contains("After"));
    }
}
