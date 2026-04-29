#if WINDOWS
using Bat.Parsing;
using Bat.Nodes;

namespace Bat.UnitTests;

[TestClass]
public class IfBlockParsingTests
{
    [TestMethod]
    public void If_Defined_WithBlock_ParsesCorrectly()
    {
        var parser = new Parser();
        parser.Append("if defined VAR (echo THEN) else echo ELSE");
        var result = parser.ParseCommand();

        var roundTrip = result.ToString();
        Assert.AreEqual("if defined VAR (echo THEN) else echo ELSE", roundTrip, $"Round-trip failed. Parse error: {result.ErrorMessage}");

        Assert.IsFalse(result.HasError, $"Parse error: {result.ErrorMessage}");
        Assert.IsInstanceOfType<IfCommandNode>(result.Root);

        var ifNode = (IfCommandNode)result.Root;
        Assert.AreEqual(IfOperator.Defined, ifNode.Operator);
        Assert.IsInstanceOfType<BlockNode>(ifNode.ThenBranch, "ThenBranch should be BlockNode");
        Assert.IsNotNull(ifNode.ElseBranch, "ElseBranch should not be null");
    }
}
#endif
