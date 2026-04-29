#if WINDOWS
using System.Reflection;
using Bat.Context.Dos;
using Bat.Execution;
using Bat.Parsing;
using Bat.UnitTests;
using Context;

namespace Bat.IntegrationTests;

[TestClass]
public class IfDebugTests
{
    [TestMethod]
    public async Task If_StringEqual_True_ExecutesThenBranch()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', ["test"]);
        fs.AddBatchFile('C', ["test"], "test.bat", "@echo off\r\nif \"hello\"==\"hello\" echo THEN");

        var console = new TestConsole();
        var ctx = new TestCommandContext(fs) { Console = console };
        ctx.SetCurrentDrive('C');
        ctx.SetPath('C', ["test"]);

        var bc = new BatchContext { Context = ctx };
        var executor = new BatchExecutor();
        await executor.ExecuteAsync("C:\\test\\test.bat", "", bc, []);

        Assert.IsTrue(console.OutText.Contains("THEN"), $"Expected THEN. Output: {console.OutText}. Errors: {console.ErrText}");
    }

    [TestMethod]
    public async Task If_StringEqual_False_ExecutesElseBranch()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', ["test"]);
        fs.AddBatchFile('C', ["test"], "test.bat", "@echo off\r\nif \"hello\"==\"world\" echo WRONG else echo CORRECT");

        var console = new TestConsole();
        var ctx = new TestCommandContext(fs) { Console = console };
        ctx.SetCurrentDrive('C');
        ctx.SetPath('C', ["test"]);

        var bc = new BatchContext { Context = ctx };
        var executor = new BatchExecutor();
        await executor.ExecuteAsync("C:\\test\\test.bat", "", bc, []);

        var output = console.OutText;
        var errors = console.ErrText;
        Assert.IsTrue(output.Contains("CORRECT"), $"Expected CORRECT. Output: {output}. Errors: {errors}");
        Assert.IsFalse(output.Contains("WRONG"), $"Should not contain WRONG. Output: {output}");
    }

    [TestMethod]
    public async Task If_Defined_Variable_Exists_ExecutesThen()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', ["test"]);
        fs.AddBatchFile('C', ["test"], "test.bat", "@echo off\r\nset VAR=value\r\nif defined VAR echo DEFINED");

        var console = new TestConsole();
        var ctx = new TestCommandContext(fs) { Console = console };
        ctx.SetCurrentDrive('C');
        ctx.SetPath('C', ["test"]);

        var bc = new BatchContext { Context = ctx };
        var executor = new BatchExecutor();
        await executor.ExecuteAsync("C:\\test\\test.bat", "", bc, []);

        Assert.IsTrue(console.OutText.Contains("DEFINED"), $"Expected DEFINED. Output: {console.OutText}. Errors: {console.ErrText}");
    }

    [TestMethod]
    public async Task If_Defined_Variable_NotExists_ExecutesElse()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', ["test"]);
        fs.AddBatchFile('C', ["test"], "test.bat", "@echo off\r\nif defined NOSUCHVAR echo WRONGBRANCH else echo CORRECTBRANCH");

        var console = new TestConsole();
        var ctx = new TestCommandContext(fs) { Console = console };
        ctx.SetCurrentDrive('C');
        ctx.SetPath('C', ["test"]);

        var bc = new BatchContext { Context = ctx };
        var executor = new BatchExecutor();
        await executor.ExecuteAsync("C:\\test\\test.bat", "", bc, []);

        Assert.IsTrue(console.OutText.Contains("CORRECTBRANCH"), $"Expected CORRECTBRANCH. Output: {console.OutText}. Errors: {console.ErrText}");
        Assert.IsFalse(console.OutText.Contains("WRONGBRANCH"), $"Should not contain WRONGBRANCH. Output: {console.OutText}");
    }

    [TestMethod]
    public async Task If_RoundTrip_PreservesOriginal()
    {
        var result = Parser.Parse("if \"a\"==\"b\" echo yes");
        Assert.AreEqual("if \"a\"==\"b\" echo yes", result.ToString());
    }
}
#endif
