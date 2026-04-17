using Microsoft.VisualStudio.TestTools.UnitTesting;
using Bat.UnitTests;
using Bat.Execution;

namespace Bat.Tests;

[TestClass]
public class ReproductionTests
{
    [TestMethod]
    public async Task Call_BatchFile_ShouldNotRunTwice()
    {
        var h = new TestHarness();
        h.FileSystem.AddBatchFile('C', [], "inner.bat", "echo INNER");
        h.FileSystem.AddBatchFile('C', [], "outer.bat", "call inner.bat\necho OUTER");
        
        await h.Execute("outer.bat");
        
        var innerCount = h.Console.OutText.Split("\n", StringSplitOptions.RemoveEmptyEntries).Count(l => l.Trim() == "INNER");
        var outerCount = h.Console.OutText.Split("\n", StringSplitOptions.RemoveEmptyEntries).Count(l => l.Trim() == "OUTER");
        
        Assert.AreEqual(1, innerCount, "INNER should be printed exactly once");
        Assert.AreEqual(1, outerCount, "OUTER should be printed exactly once");
    }

    [TestMethod]
    public async Task Direct_BatchFile_ShouldNotRunTwice()
    {
        var h = new TestHarness();
        h.FileSystem.AddBatchFile('C', [], "inner.bat", "echo INNER");
        h.FileSystem.AddBatchFile('C', [], "outer.bat", "inner.bat\necho OUTER");
        
        await h.Execute("outer.bat");
        
        var innerCount = h.Console.OutText.Split("\n", StringSplitOptions.RemoveEmptyEntries).Count(l => l.Trim() == "INNER");
        var outerCount = h.Console.OutText.Split("\n", StringSplitOptions.RemoveEmptyEntries).Count(l => l.Trim() == "OUTER");
        
        Assert.AreEqual(1, innerCount, "INNER should be printed exactly once");
        // In CMD, if you call a batch file WITHOUT 'call', it never returns to the caller.
        // We should check what 'bat' does.
    }
    
    [TestMethod]
    public async Task Call_Recursion_ShouldNotHang()
    {
        var h = new TestHarness();
        // This batch file calls itself once.
        h.FileSystem.AddBatchFile('C', [], "recurse.bat", "if %1==done exit /b\necho RECURSING\ncall recurse.bat done\necho BACK");
        
        await h.Execute("recurse.bat");
        
        var recursingCount = h.Console.OutText.Split("\n", StringSplitOptions.RemoveEmptyEntries).Count(l => l.Trim() == "RECURSING");
        var backCount = h.Console.OutText.Split("\n", StringSplitOptions.RemoveEmptyEntries).Count(l => l.Trim() == "BACK");
        
        Assert.AreEqual(2, recursingCount, "RECURSING should be printed twice");
        Assert.AreEqual(2, backCount, "BACK should be printed twice");
    }

    [TestMethod]
    public async Task Label_Recursion_ShouldNotHang()
    {
        var h = new TestHarness();
        h.FileSystem.AddBatchFile('C', [], "label_recurse.bat", "if %1==done exit /b\necho RECURSING\ncall :label done\necho BACK\ngoto :eof\n:label\necho IN_LABEL\ncall label_recurse.bat done\necho LABEL_BACK");
        
        await h.Execute("label_recurse.bat");
        
        var inLabelCount = h.Console.OutText.Split("\n", StringSplitOptions.RemoveEmptyEntries).Count(l => l.Trim() == "IN_LABEL");
        Assert.AreEqual(1, inLabelCount);
    }
}
