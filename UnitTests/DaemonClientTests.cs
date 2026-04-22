using Ipc;

namespace Bat.UnitTests;

[TestClass]
public class TerminalProtocolTests
{
    [TestMethod]
    [Timeout(4000)]
    public async Task Init_RoundTrip()
    {
        using var stream = new MemoryStream();
        await TerminalProtocol.WriteInitAsync(stream, "/C echo hello", 120, 40, true, default);
        stream.Position = 0;

        var msg = await TerminalProtocol.ReadAsync(stream);
        Assert.IsNotNull(msg);
        Assert.AreEqual(TerminalMessageType.Init, msg.Value.Type);

        var (cmdLine, w, h, interactive) = TerminalProtocol.ParseInit(msg.Value.Payload);
        Assert.AreEqual("/C echo hello", cmdLine);
        Assert.AreEqual(120, w);
        Assert.AreEqual(40, h);
        Assert.IsTrue(interactive);
    }

    [TestMethod]
    [Timeout(4000)]
    public async Task Key_RoundTrip()
    {
        using var stream = new MemoryStream();
        var key = new ConsoleKeyInfo('A', ConsoleKey.A, shift: true, alt: false, control: true);
        await TerminalProtocol.WriteKeyAsync(stream, key, default);
        stream.Position = 0;

        var msg = await TerminalProtocol.ReadAsync(stream);
        Assert.IsNotNull(msg);
        Assert.AreEqual(TerminalMessageType.Key, msg.Value.Type);

        var parsed = TerminalProtocol.ParseKey(msg.Value.Payload);
        Assert.AreEqual('A', parsed.KeyChar);
        Assert.AreEqual(ConsoleKey.A, parsed.Key);
        Assert.IsTrue((parsed.Modifiers & ConsoleModifiers.Shift) != 0);
        Assert.IsFalse((parsed.Modifiers & ConsoleModifiers.Alt) != 0);
        Assert.IsTrue((parsed.Modifiers & ConsoleModifiers.Control) != 0);
    }

    [TestMethod]
    [Timeout(4000)]
    public async Task Resize_RoundTrip()
    {
        using var stream = new MemoryStream();
        await TerminalProtocol.WriteResizeAsync(stream, 200, 50, default);
        stream.Position = 0;

        var msg = await TerminalProtocol.ReadAsync(stream);
        Assert.IsNotNull(msg);
        Assert.AreEqual(TerminalMessageType.Resize, msg.Value.Type);

        var (w, h) = TerminalProtocol.ParseResize(msg.Value.Payload);
        Assert.AreEqual(200, w);
        Assert.AreEqual(50, h);
    }

    [TestMethod]
    [Timeout(4000)]
    public async Task Out_RoundTrip()
    {
        using var stream = new MemoryStream();
        var data = System.Text.Encoding.UTF8.GetBytes("hello world");
        await TerminalProtocol.WriteOutAsync(stream, data, default);
        stream.Position = 0;

        var msg = await TerminalProtocol.ReadAsync(stream);
        Assert.IsNotNull(msg);
        Assert.AreEqual(TerminalMessageType.Out, msg.Value.Type);
        Assert.AreEqual("hello world", System.Text.Encoding.UTF8.GetString(msg.Value.Payload));
    }

    [TestMethod]
    [Timeout(4000)]
    public async Task Err_RoundTrip()
    {
        using var stream = new MemoryStream();
        var data = System.Text.Encoding.UTF8.GetBytes("error!");
        await TerminalProtocol.WriteErrAsync(stream, data, default);
        stream.Position = 0;

        var msg = await TerminalProtocol.ReadAsync(stream);
        Assert.IsNotNull(msg);
        Assert.AreEqual(TerminalMessageType.Err, msg.Value.Type);
        Assert.AreEqual("error!", System.Text.Encoding.UTF8.GetString(msg.Value.Payload));
    }

    [TestMethod]
    [Timeout(4000)]
    public async Task Exit_RoundTrip()
    {
        using var stream = new MemoryStream();
        await TerminalProtocol.WriteExitAsync(stream, 42, default);
        stream.Position = 0;

        var msg = await TerminalProtocol.ReadAsync(stream);
        Assert.IsNotNull(msg);
        Assert.AreEqual(TerminalMessageType.Exit, msg.Value.Type);
        Assert.AreEqual(42, TerminalProtocol.ParseExitCode(msg.Value.Payload));
    }

    [TestMethod]
    [Timeout(4000)]
    public async Task ReadAsync_EmptyStream_ReturnsNull()
    {
        using var stream = new MemoryStream();
        var msg = await TerminalProtocol.ReadAsync(stream);
        Assert.IsNull(msg);
    }

    [TestMethod]
    [Timeout(4000)]
    public async Task MultipleMessages_RoundTrip()
    {
        using var stream = new MemoryStream();
        await TerminalProtocol.WriteInitAsync(stream, "/C dir", 80, 25, true, default);
        await TerminalProtocol.WriteKeyAsync(stream, new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false), default);
        await TerminalProtocol.WriteExitAsync(stream, 0, default);
        stream.Position = 0;

        var m1 = await TerminalProtocol.ReadAsync(stream);
        var m2 = await TerminalProtocol.ReadAsync(stream);
        var m3 = await TerminalProtocol.ReadAsync(stream);
        var m4 = await TerminalProtocol.ReadAsync(stream);

        Assert.IsNotNull(m1);
        Assert.AreEqual(TerminalMessageType.Init, m1.Value.Type);
        Assert.IsNotNull(m2);
        Assert.AreEqual(TerminalMessageType.Key, m2.Value.Type);
        Assert.IsNotNull(m3);
        Assert.AreEqual(TerminalMessageType.Exit, m3.Value.Type);
        Assert.IsNull(m4);
    }

    [TestMethod]
    public void GetSocketPath_ContainsUsername()
    {
        var path = TerminalProtocol.GetSocketPath();
        Assert.IsTrue(path.Contains("batd-"));
        Assert.IsTrue(path.Contains(Environment.UserName));
    }
}
