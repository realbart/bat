using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bat.UnitTests;

[TestClass]
public class RedirectionTests
{
    [TestMethod]
    [Timeout(4000)]
    public async Task Redirect_Output_WritesToFile()
    {
        var h = new TestHarness();

        await h.Execute(@"echo hello> C:\out.txt");

        var content = h.FileSystem.ReadAllText('C', ["out.txt"]);
        Assert.IsTrue(content.Contains("hello"), $"File should contain 'hello', got: '{content}'");
        Assert.AreEqual("", h.Console.OutText);
    }

    [TestMethod]
    [Timeout(4000)]
    public async Task Redirect_OutputAppend_AppendsToFile()
    {
        var h = new TestHarness();
        h.FileSystem.WriteAllText('C', ["out.txt"], "line1\r\n");

        await h.Execute(@"echo line2>> C:\out.txt");

        var content = h.FileSystem.ReadAllText('C', ["out.txt"]);
        Assert.IsTrue(content.Contains("line1"));
        Assert.IsTrue(content.Contains("line2"));
    }

    [TestMethod]
    [Timeout(4000)]
    public async Task Redirect_OutputToNul_SuppressesOutput()
    {
        var h = new TestHarness();

        await h.Execute("echo hello > nul");

        Assert.AreEqual("", h.Console.OutText);
    }

    [TestMethod]
    [Timeout(4000)]
    public async Task Redirect_ErrorToNul_SuppressesErrors()
    {
        var h = new TestHarness();

        await h.Execute("cd nonexistent 2>nul");

        Assert.AreEqual("", h.Console.ErrText);
    }

    [TestMethod]
    [Timeout(4000)]
    public async Task Redirect_ErrorToOutput_MergesStreams()
    {
        var h = new TestHarness();

        await h.Execute("cd nonexistent 2>&1");

        Assert.IsTrue(h.Console.OutLines.Count > 0, "Error should appear on stdout");
        Assert.AreEqual("", h.Console.ErrText);
    }

    [TestMethod]
    [Timeout(4000)]
    public async Task Redirect_Input_ReadsFromFile()
    {
        var h = new TestHarness();
        h.FileSystem.WriteAllText('C', ["in.txt"], "hello from file");

        await h.Execute(@"set /p X=< C:\in.txt");

        Assert.AreEqual("hello from file", h.Context.EnvironmentVariables["X"]);
    }

    [TestMethod]
    [Timeout(4000)]
    public async Task Redirect_Combined_OutputToFileAndErrorToNul()
    {
        var h = new TestHarness();

        await h.Execute(@"echo hello> C:\out.txt 2>nul");

        Assert.IsTrue(h.FileSystem.ReadAllText('C', ["out.txt"]).Contains("hello"));
        Assert.AreEqual("", h.Console.OutText);
        Assert.AreEqual("", h.Console.ErrText);
    }

    [TestMethod]
    [Timeout(4000)]
    public async Task Pipe_ConnectsOutputToInput()
    {
        var h = new TestHarness();

        await h.Execute("echo testvalue| set /p RESULT=");

        Assert.AreEqual("testvalue", h.Context.EnvironmentVariables["RESULT"]);
    }
}
