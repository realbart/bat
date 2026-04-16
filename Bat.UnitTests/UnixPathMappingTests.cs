using Bat.Context;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bat.UnitTests;

[TestClass]
public class UnixPathMappingTests
{
    [TestMethod]
    public void ReadPath_TildeExpansion_OnUnix()
    {
        var parser = new BatArgumentParser('/'); // Unix mode
        var args = parser.Parse(["-m:c=~/.c"]);
        
        Assert.IsNotNull(args.DriveMappings);
        Assert.IsTrue(args.DriveMappings.ContainsKey('C'));
        
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home)) home = Environment.GetEnvironmentVariable("HOME") ?? "/";
        var expected = Path.Combine(home, ".c");
        Assert.AreEqual(expected, args.DriveMappings['C']);
    }

    [TestMethod]
    public void ReadPath_QuotedTildeExpansion_OnUnix()
    {
        var parser = new BatArgumentParser('/'); // Unix mode
        var args = parser.Parse(["-m:c=\"~/.c\""]);
        
        Assert.IsNotNull(args.DriveMappings);
        Assert.IsTrue(args.DriveMappings.ContainsKey('C'));
        
        var home2 = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home2)) home2 = Environment.GetEnvironmentVariable("HOME") ?? "/";
        var expected2 = Path.Combine(home2, ".c");
        Assert.AreEqual(expected2, args.DriveMappings['C']);
    }

    [TestMethod]
    public void ReadPath_NoTildeExpansion_OnWindows()
    {
        var parser = new BatArgumentParser('\\'); // Windows mode
        var args = parser.Parse(["/M:c=~/.c"]);
        
        Assert.IsNotNull(args.DriveMappings);
        Assert.IsTrue(args.DriveMappings.ContainsKey('C'));
        Assert.AreEqual("~/.c", args.DriveMappings['C']);
    }
}
