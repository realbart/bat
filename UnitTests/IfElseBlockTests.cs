#if WINDOWS
using Bat.Execution;
using Bat.UnitTests;
using Context;

namespace Bat.IntegrationTests;

[TestClass]
public class IfElseBlockTests
{
    [TestMethod]
    public async Task If_Defined_False_WithParenthesizedElse_ExecutesElse()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', ["test"]);
        fs.AddBatchFile('C', ["test"], "test.bat", "@echo off\r\nif defined NOSUCHVAR (echo WRONG) else echo CORRECT");

        var console = new TestConsole();
        var ctx = new TestCommandContext(fs) { Console = console };
        ctx.SetCurrentDrive('C');
        ctx.SetPath('C', ["test"]);

        var bc = new BatchContext { Context = ctx };
        var executor = new BatchExecutor();
        await executor.ExecuteAsync("C:\\test\\test.bat", "", bc, []);

        Assert.IsTrue(console.OutText.Contains("CORRECT"), $"Expected CORRECT. Output: '{console.OutText}'. Errors: '{console.ErrText}'");
        Assert.IsFalse(console.OutText.Contains("WRONG"), $"Should not contain WRONG. Output: '{console.OutText}'");
    }

    [TestMethod]
    public async Task If_Defined_True_WithParenthesizedThen_ExecutesThen()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', ["test"]);
        fs.AddBatchFile('C', ["test"], "test.bat", "@echo off\r\nset VAR=value\r\nif defined VAR (echo CORRECT) else echo WRONG");

        var console = new TestConsole();
        var ctx = new TestCommandContext(fs) { Console = console };
        ctx.SetCurrentDrive('C');
        ctx.SetPath('C', ["test"]);

        var bc = new BatchContext { Context = ctx };
        var executor = new BatchExecutor();
        await executor.ExecuteAsync("C:\\test\\test.bat", "", bc, []);

        Assert.IsTrue(console.OutText.Contains("CORRECT"), $"Expected CORRECT. Output: '{console.OutText}'. Errors: '{console.ErrText}'");
        Assert.IsFalse(console.OutText.Contains("WRONG"), $"Should not contain WRONG. Output: '{console.OutText}'");
    }
}
#endif
