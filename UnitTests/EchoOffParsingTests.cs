#if WINDOWS
using Bat.Parsing;
using Bat.Nodes;

namespace Bat.UnitTests;

[TestClass]
public class EchoOffParsingTests
{
    [TestMethod]
    public void EchoOff_ParsesAsQuietNode()
    {
        var parser = new Parser();
        parser.Append("@echo off");
        var result = parser.ParseCommand();
        
        Assert.IsFalse(result.HasError, $"Parse error: {result.ErrorMessage}");
        Assert.IsInstanceOfType<QuietNode>(result.Root);
    }
}
#endif
