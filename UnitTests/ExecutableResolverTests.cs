using Bat.Execution;

namespace Bat.UnitTests;

[TestClass]
public class ExecutableResolverTests
{
    [TestMethod]
    public void Resolve_CurrentDirectory_BeatsPath()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddEntry('Z', [], "test.exe", false);
        fs.AddDir('Z', ["bin"]);
        fs.AddEntry('Z', ["bin"], "test.exe", false);

        var context = new TestCommandContext(fs);
        context.EnvironmentVariables["PATH"] = "Z:\\bin";

        var result = ExecutableResolver.Resolve("test", context);

        Assert.IsNotNull(result);
        Assert.AreEqual("Z:\\test.exe", result);
    }

    [TestMethod]
    public void Resolve_NotInCurrentDir_SearchesPath()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddDir('Z', ["bin"]);
        fs.AddEntry('Z', ["bin"], "git.exe", false);

        var context = new TestCommandContext(fs);
        context.EnvironmentVariables["PATH"] = "Z:\\bin";

        var result = ExecutableResolver.Resolve("git", context);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.EndsWith("\\bin\\git.exe"));
    }

    [TestMethod]
    public void Resolve_ExtensionPriority_BatBeforeExe()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddEntry('Z', [], "test.bat", false);
        fs.AddEntry('Z', [], "test.exe", false);

        var context = new TestCommandContext(fs);

        var result = ExecutableResolver.Resolve("test", context);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.EndsWith(".bat"));
    }

    [TestMethod]
    public void Resolve_ExplicitExtension_NoImplicitSearch()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddEntry('Z', [], "test.txt", false);

        var context = new TestCommandContext(fs);

        var result = ExecutableResolver.Resolve("test.txt", context);

        Assert.IsNotNull(result);
        Assert.AreEqual("Z:\\test.txt", result);
    }

    [TestMethod]
    public void Resolve_NotFound_ReturnsNull()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);

        var context = new TestCommandContext(fs);
        context.EnvironmentVariables["PATH"] = "Z:\\bin";

        var result = ExecutableResolver.Resolve("notfound", context);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Resolve_AbsoluteBackslashPath_WithExtension_FindsFile()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddDir('Z', ["Windows"]);
        fs.AddEntry('Z', ["Windows"], "notepad.exe", false);

        var context = new TestCommandContext(fs);

        var result = ExecutableResolver.Resolve(@"\Windows\notepad.exe", context);

        Assert.IsNotNull(result);
        Assert.AreEqual(@"Z:\Windows\notepad.exe", result);
    }

    [TestMethod]
    public void Resolve_AbsoluteBackslashPath_NoExtension_FindsFile()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddDir('Z', ["Windows"]);
        fs.AddEntry('Z', ["Windows"], "notepad.exe", false);

        var context = new TestCommandContext(fs);

        var result = ExecutableResolver.Resolve(@"\Windows\notepad", context);

        Assert.IsNotNull(result);
        Assert.AreEqual(@"Z:\Windows\notepad.exe", result);
    }

    [TestMethod]
    public void Resolve_AbsoluteBackslashPath_RootFile_FindsFile()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddEntry('Z', [], "setup.exe", false);

        var context = new TestCommandContext(fs);

        var result = ExecutableResolver.Resolve(@"\setup.exe", context);

        Assert.IsNotNull(result);
        Assert.AreEqual(@"Z:\setup.exe", result);
    }

    // tree.com exists only as .com (no .exe counterpart) → must be found in PATH
    [TestMethod]
    public void Resolve_ComExtension_InPath_FindsFile()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddDir('Z', ["Windows", "System32"]);
        fs.AddEntry('Z', ["Windows", "System32"], "tree.com", false);

        var context = new TestCommandContext(fs);
        context.EnvironmentVariables["PATH"] = @"Z:\Windows\System32";

        var result = ExecutableResolver.Resolve("tree", context);

        Assert.IsNotNull(result);
        Assert.IsTrue(result!.EndsWith("tree.com"));
    }

    // .com in current directory is found without PATH
    [TestMethod]
    public void Resolve_ComExtension_InCurrentDir_FindsFile()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddEntry('Z', [], "myprog.com", false);

        var context = new TestCommandContext(fs);

        var result = ExecutableResolver.Resolve("myprog", context);

        Assert.IsNotNull(result);
        Assert.AreEqual(@"Z:\myprog.com", result);
    }

    // CMD.EXE priority: .bat > .cmd > .com > .exe > .dll  — .com beats .exe
    [TestMethod]
    public void Resolve_ComPriorityOverExe_MatchesCmdOrder()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', []);
        fs.AddEntry('Z', [], "prog.exe", false);
        fs.AddEntry('Z', [], "prog.com", false);

        var context = new TestCommandContext(fs);

        var result = ExecutableResolver.Resolve("prog", context);

        Assert.IsNotNull(result);
        Assert.IsTrue(result!.EndsWith(".com"), ".com has priority over .exe, matching CMD.EXE order");
    }
}
