#if WINDOWS
using Bat.Parsing;
using Bat.Nodes;
using Bat.UnitTests;

namespace Bat.IntegrationTests;

[TestClass]
public class IfParsingTests
{
    [TestMethod]
    public void If_StringEqual_ParsesAsIfCommandNode()
    {
        var result = Parser.Parse("if \"hello\"==\"hello\" echo THEN");
        
        Assert.IsFalse(result.HasError, $"Parse error: {result.ErrorMessage}");
        Assert.IsInstanceOfType<IfCommandNode>(result.Root);
        
        var ifNode = (IfCommandNode)result.Root;
        Assert.AreEqual(IfOperator.StringEqual, ifNode.Operator);
    }

    [TestMethod]
    public void If_Defined_ParsesAsIfCommandNode()
    {
        var result = Parser.Parse("if defined VAR echo THEN");
        
        Assert.IsFalse(result.HasError, $"Parse error: {result.ErrorMessage}");
        Assert.IsInstanceOfType<IfCommandNode>(result.Root);
        
        var ifNode = (IfCommandNode)result.Root;
        Assert.AreEqual(IfOperator.Defined, ifNode.Operator);
    }
}
#endif
