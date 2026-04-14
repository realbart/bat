using Bat.Console;
using Bat.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bat.UnitTests;

[TestClass]
public class BatchExecutorTests
{
    private static (BatchExecutor executor, TestConsole console, TestCommandContext ctx, BatchContext bc) Setup(
        TestFileSystem fs, char drive = 'Z')
    {
        var console = new TestConsole();
        var ctx = new TestCommandContext(fs) { Console = console };
        ctx.SetCurrentDrive(drive);
        var bc = new BatchContext { Context = ctx };
        return (new BatchExecutor(console), console, ctx, bc);
    }

    [TestMethod]
    public async Task BatchExecutor_SimpleBatch_ExecutesLines()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddBatchFile('Z', [], "test.bat", "@echo off\necho Hello\necho World");
        var (executor, console, ctx, bc) = Setup(fs);

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
        var (executor, console, ctx, bc) = Setup(fs);

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
        var (executor, console, ctx, bc) = Setup(fs);

        var exitCode = await executor.ExecuteAsync("Z:\\test.bat", "", bc, []);

        Assert.AreEqual(0, exitCode);
        Assert.IsTrue(console.OutLines.Contains("Before"));
        Assert.IsFalse(console.OutLines.Contains("After"));
    }

    // === ScanLabels tests ===

    [TestMethod]
    public void ScanLabels_FindsLabels()
    {
        var labels = BatchExecutor.ScanLabels(":start\r\necho hi\r\n:end\r\necho done");

        Assert.AreEqual(2, labels.Count);
        Assert.IsTrue(labels.ContainsKey("start"));
        Assert.IsTrue(labels.ContainsKey("end"));
    }

    [TestMethod]
    public void ScanLabels_IsCaseInsensitive()
    {
        var labels = BatchExecutor.ScanLabels(":MyLabel\r\necho hi");

        Assert.IsTrue(labels.ContainsKey("mylabel"));
        Assert.IsTrue(labels.ContainsKey("MYLABEL"));
    }

    [TestMethod]
    public void ScanLabels_SkipsComment()
    {
        var labels = BatchExecutor.ScanLabels(":start\r\n:: comment\r\n:end");

        // :: is treated as a label starting with ':'
        Assert.IsTrue(labels.ContainsKey("start"));
        Assert.IsTrue(labels.ContainsKey("end"));
    }

    [TestMethod]
    public void ScanLabels_DuplicateKeepsFirst()
    {
        var labels = BatchExecutor.ScanLabels(":dup\r\necho first\r\n:dup\r\necho second");

        Assert.AreEqual(1, labels.Count);
        Assert.AreEqual(0, labels["dup"]); // Position of first occurrence
    }

    // === GOTO tests ===

    [TestMethod]
    public async Task Goto_JumpsToLabel()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddBatchFile('Z', [], "test.bat", "echo before\r\ngoto end\r\necho skipped\r\n:end\r\necho after");
        var (executor, console, ctx, bc) = Setup(fs);

        await executor.ExecuteAsync("Z:\\test.bat", "", bc, []);

        Assert.IsTrue(console.OutLines.Contains("before"));
        Assert.IsFalse(console.OutLines.Contains("skipped"));
        Assert.IsTrue(console.OutLines.Contains("after"));
    }

    [TestMethod]
    public async Task Goto_Eof_EndsBatch()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddBatchFile('Z', [], "test.bat", "echo before\r\ngoto :eof\r\necho skipped");
        var (executor, console, ctx, bc) = Setup(fs);

        await executor.ExecuteAsync("Z:\\test.bat", "", bc, []);

        Assert.IsTrue(console.OutLines.Contains("before"));
        Assert.IsFalse(console.OutLines.Contains("skipped"));
    }

    [TestMethod]
    public async Task Goto_UnknownLabel_ReportsError()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddBatchFile('Z', [], "test.bat", "goto nonexistent");
        var (executor, console, ctx, bc) = Setup(fs);

        await executor.ExecuteAsync("Z:\\test.bat", "", bc, []);

        Assert.IsTrue(console.ErrLines.Any(l => l.Contains("cannot find the batch label")));
    }

    // === CALL tests ===

    [TestMethod]
    public async Task Call_RunsOtherBatchFile()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddBatchFile('Z', [], "main.bat", "call Z:\\helper.bat\r\necho back");
        fs.AddBatchFile('Z', [], "helper.bat", "echo from helper");
        var (executor, console, ctx, bc) = Setup(fs);

        await executor.ExecuteAsync("Z:\\main.bat", "", bc, []);

        Assert.IsTrue(console.OutLines.Contains("from helper"));
        Assert.IsTrue(console.OutLines.Contains("back"));
    }

    [TestMethod]
    public async Task Call_Label_WorksAsSubroutine()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddBatchFile('Z', [], "test.bat",
            "call :sub\r\necho main\r\ngoto :eof\r\n:sub\r\necho sub\r\nexit /b");
        var (executor, console, ctx, bc) = Setup(fs);

        await executor.ExecuteAsync("Z:\\test.bat", "", bc, []);

        Assert.IsTrue(console.OutLines.Contains("sub"));
        Assert.IsTrue(console.OutLines.Contains("main"));
    }

    // === SHIFT tests ===

    [TestMethod]
    public async Task Shift_MovesParameters()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddBatchFile('Z', [], "test.bat", "echo %1\r\nshift\r\necho %1");
        var (executor, console, ctx, bc) = Setup(fs);

        await executor.ExecuteAsync("Z:\\test.bat", "first second", bc, []);

        Assert.IsTrue(console.OutLines.Contains("first"));
        Assert.IsTrue(console.OutLines.Contains("second"));
    }

    // === EXIT /B tests ===

    [TestMethod]
    public async Task ExitB_SetsErrorCode()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddBatchFile('Z', [], "test.bat", "echo before\r\nexit /b 5\r\necho after");
        var (executor, console, ctx, bc) = Setup(fs);

        await executor.ExecuteAsync("Z:\\test.bat", "", bc, []);

        Assert.IsTrue(console.OutLines.Contains("before"));
        Assert.IsFalse(console.OutLines.Contains("after"));
        Assert.AreEqual(5, ctx.ErrorCode);
    }

    // === Nesting depth test ===

    [TestMethod]
    public async Task NestingTooDeep_ReportsError()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        // A batch that calls itself
        fs.AddBatchFile('Z', [], "loop.bat", "call Z:\\loop.bat");
        var (executor, console, ctx, bc) = Setup(fs);

        await executor.ExecuteAsync("Z:\\loop.bat", "", bc, []);

        Assert.IsTrue(console.ErrLines.Any(l => l.Contains("nesting")));
    }

    // === GOTO :EOF after CALL ===

    [TestMethod]
    public async Task GotoEof_AfterCallLabel_ReturnsToCaller()
    {
        // From GOTO /? help: "Executing GOTO :EOF after a CALL with a target label will return control
        // to the statement immediately following the CALL."
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddBatchFile('Z', [], "test.bat",
            "call :sub\r\necho after_call\r\ngoto :eof\r\n:sub\r\ngoto :eof");
        var (executor, console, ctx, bc) = Setup(fs);

        await executor.ExecuteAsync("Z:\\test.bat", "", bc, []);

        Assert.IsTrue(console.OutLines.Contains("after_call"));
    }

    // === SHIFT /2 in batch ===

    [TestMethod]
    public async Task Shift2_InBatch_LeavesLowerParamsUnaffected()
    {
        // From SHIFT /? help: "SHIFT /2 would shift %3 to %2, %4 to %3, etc. and leave %0 and %1 unaffected."
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddBatchFile('Z', [], "test.bat",
            "shift /2\r\necho %1 %2 %3");
        var (executor, console, ctx, bc) = Setup(fs);

        await executor.ExecuteAsync("Z:\\test.bat", "first second third fourth", bc, []);

        // After SHIFT /2: %1 stays "first", %2=old %3="third", %3=old %4="fourth"
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("first") && l.Contains("third") && l.Contains("fourth")));
    }

    // === CALL :label with arguments ===

    [TestMethod]
    public async Task Call_Label_PassesArgumentsToSubroutine()
    {
        // From CALL /? help: "CALL :label arguments" — arguments available as %1 %2 in the label
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddBatchFile('Z', [], "test.bat",
            "call :greet World\r\ngoto :eof\r\n:greet\r\necho Hello %1\r\nexit /b");
        var (executor, console, ctx, bc) = Setup(fs);

        await executor.ExecuteAsync("Z:\\test.bat", "", bc, []);

        Assert.IsTrue(console.OutLines.Any(l => l.Contains("Hello") && l.Contains("World")));
    }
}


