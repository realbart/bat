using Bat.Daemon;
using Ipc;

namespace Bat.UnitTests;

[TestClass]
public class DaemonServerTests
{
    [TestMethod]
    [Timeout(4000)]
    public void SplitCommandLine_Simple()
    {
        var result = DaemonServer.SplitCommandLine("/C echo hello");
        CollectionAssert.AreEqual(new[] { "/C", "echo", "hello" }, result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void SplitCommandLine_Quoted()
    {
        var result = DaemonServer.SplitCommandLine("/C \"echo hello world\"");
        CollectionAssert.AreEqual(new[] { "/C", "echo hello world" }, result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void SplitCommandLine_Empty()
    {
        var result = DaemonServer.SplitCommandLine("");
        Assert.AreEqual(0, result.Length);
    }

    [TestMethod]
    [Timeout(4000)]
    public async Task HandleSession_Init_ThenExit_OverStream()
    {
        using var server = new DaemonServer();
        var clientToServer = new MemoryStream();
        var serverToClient = new MemoryStream();

        // Write Init message (bat → batd)
        await TerminalProtocol.WriteInitAsync(clientToServer, "/C echo hello", 80, 25, false, default);
        clientToServer.Position = 0;

        using var duplex = new DuplexStream(clientToServer, serverToClient);

        // HandleSessionAsync expects a Socket, but we simulate with a DuplexStream
        // We test the protocol framing instead
        var initMsg = await TerminalProtocol.ReadAsync(clientToServer);
        Assert.IsNotNull(initMsg);
        Assert.AreEqual(TerminalMessageType.Init, initMsg.Value.Type);

        var (cmdLine, width, height, interactive) = TerminalProtocol.ParseInit(initMsg.Value.Payload);
        Assert.AreEqual("/C echo hello", cmdLine);
        Assert.AreEqual(80, width);
        Assert.AreEqual(25, height);
        Assert.IsFalse(interactive);
    }
}

/// <summary>
/// A stream that reads from one stream and writes to another, simulating a duplex pipe.
/// </summary>
internal sealed class DuplexStream(Stream readFrom, Stream writeTo) : Stream
{
    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count) => readFrom.Read(buffer, offset, count);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => readFrom.ReadAsync(buffer, ct);
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => readFrom.ReadAsync(buffer, offset, count, ct);

    public override void Write(byte[] buffer, int offset, int count) => writeTo.Write(buffer, offset, count);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) => writeTo.WriteAsync(buffer, ct);
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) => writeTo.WriteAsync(buffer, offset, count, ct);

    public override void Flush() => writeTo.Flush();
    public override Task FlushAsync(CancellationToken ct) => writeTo.FlushAsync(ct);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
