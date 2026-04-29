#if WINDOWS
using Bat.Console;
using Bat.Parsing;
using Bat.UnitTests;
using Bat.Nodes;

namespace Bat.IntegrationTests;

[TestClass]
public class IfReplTests
{
    [TestMethod]
    public async Task If_StringEqual_True_InRepl_ExecutesThen()
    {
        var console = new TestConsole();
        var ctx = new TestCommandContext { Console = console };
        var dispatcher = new Dispatcher();

        var parser = new Parser();
        parser.Append("if \"hello\"==\"hello\" echo THEN");
        var command = parser.ParseCommand();

        Assert.IsFalse(command.HasError, $"Parse error: {command.ErrorMessage}");
        Assert.IsInstanceOfType<IfCommandNode>(command.Root);

        var ifNode = (IfCommandNode)command.Root;
        Assert.IsNotNull(ifNode.ThenBranch, "ThenBranch should not be null");
        Assert.AreNotEqual(0, string.Join("", ifNode.ThenBranch.GetTokens().Select(t => t.Raw)).Length, "ThenBranch should have tokens");

        await dispatcher.ExecuteCommandAsync(ctx, command);

        Assert.IsTrue(console.OutText.Contains("THEN"), $"Expected THEN. Output: {console.OutText}. Errors: {console.ErrText}");
    }
}
#endif
