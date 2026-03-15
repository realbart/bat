using System.Text;
using Bat.Protocol.Client;
using Bat.Protocol.Models;
using Bat.Protocol.Server;
using Xunit;

namespace Bat.Protocol.Tests;

public class ProtocolTests
{
    [Fact]
    public async Task Client_ShouldPerformHandshake_Successfully()
    {
        // Arrange
        var handshake = "test1234";
        var clientStream = new MemoryStream();
        var serverStream = new MemoryStream();
        
        // We simuleren de pipe door twee streams of een custom stream te gebruiken
        // Maar voor de client test is het makkelijker om te weten wat hij schrijft
        // en wat hij leest.
        
        var client = new DosProtocolClient(new BidirectionalMemoryStream(serverStream, clientStream));

        // Simulate server response: echo handshake back
        var handshakeBytes = Encoding.ASCII.GetBytes(handshake);
        serverStream.Write(handshakeBytes, 0, handshakeBytes.Length);
        serverStream.Position = 0;

        // Act
        var result = await client.PerformHandshakeAsync(handshake);

        // Assert
        Assert.True(result);

        // Verify client sent: handshake
        clientStream.Position = 0;
        var sentData = clientStream.ToArray();
        var sentHandshake = Encoding.ASCII.GetString(sentData, 0, handshake.Length);
        Assert.Equal(handshake, sentHandshake);
    }

    [Fact]
    public async Task Server_ShouldWaitForHandshake_Successfully()
    {
        // Arrange
        var handshake = "abcd5678";
        var clientStream = new MemoryStream();
        var serverStream = new MemoryStream();

        // Simulate client sending: handshake
        var handshakeBytes = Encoding.ASCII.GetBytes(handshake);
        clientStream.Write(handshakeBytes, 0, handshakeBytes.Length);
        clientStream.Position = 0;

        var server = new DosProtocolServer();

        // Act
        var result = await server.WaitForHandshakeAsync(new BidirectionalMemoryStream(clientStream, serverStream), handshake);

        // Assert
        Assert.True(result.Success);

        // Verify server sent handshake back
        serverStream.Position = 0;
        var response = new byte[handshake.Length];
        await serverStream.ReadAsync(response, 0, response.Length);
        var responseStr = Encoding.ASCII.GetString(response);
        Assert.Equal(handshake, responseStr);
    }

    [Fact]
    public async Task Server_ShouldFailHandshake_OnInvalidContent()
    {
        var server = new DosProtocolServer();
        var handshake = "abc12345";
        
        using var stream = new MemoryStream();
        var bytes = Encoding.ASCII.GetBytes("WRONG_HS");
        stream.Write(bytes, 0, bytes.Length);
        stream.Position = 0;
        
        var result = await server.WaitForHandshakeAsync(stream, handshake);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Server_ShouldFailHandshake_OnTimeout()
    {
        var server = new DosProtocolServer();
        var handshake = "abc12345";
        
        using var stream = new MemoryStream(); // Empty stream will timeout
        
        var result = await server.WaitForHandshakeAsync(stream, handshake);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Client_ShouldSendCommand_AsJson()
    {
        // Arrange
        var handshake = "test1234";
        var clientStream = new MemoryStream();
        var serverStream = new MemoryStream();

        // Echo handshake for initial handshake
        var handshakeBytes = Encoding.ASCII.GetBytes(handshake);
        serverStream.Write(handshakeBytes, 0, handshakeBytes.Length);
        serverStream.Position = 0;

        var client = new DosProtocolClient(new BidirectionalMemoryStream(serverStream, clientStream));

        await client.PerformHandshakeAsync(handshake);

        // Act
        var command = new DosCommand("reset_buffer");
        await client.SendCommandAsync(command);

        // Assert
        clientStream.Position = handshake.Length; // Skip handshake
        var commandData = clientStream.ToArray()[handshake.Length..];
        
        var jsonStr = Encoding.UTF8.GetString(commandData);
        Assert.Contains("\"command\":\"reset_buffer\"", jsonStr);
    }

    private class BidirectionalMemoryStream : Stream
    {
        private readonly Stream _readStream;
        private readonly Stream _writeStream;

        public BidirectionalMemoryStream(Stream readStream, Stream writeStream)
        {
            _readStream = readStream;
            _writeStream = writeStream;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => _writeStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _readStream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => _writeStream.Write(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _readStream.ReadAsync(buffer, offset, count, cancellationToken);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _writeStream.WriteAsync(buffer, offset, count, cancellationToken);
    }

    [Fact]
    public async Task Server_ShouldProcessJsonCommands()
    {
        // Arrange
        var stream = new MemoryStream();

        // Simulate client sending JSON command: {"command":"test"}
        var jsonBytes = Encoding.UTF8.GetBytes("{\"command\":\"test\"}");
        await stream.WriteAsync(jsonBytes, 0, jsonBytes.Length);
        stream.Position = 0;

        var server = new DosProtocolServer();
        DosCommand? receivedCommand = null;

        // Act
        var processTask = Task.Run(async () =>
        {
            await server.ProcessStreamAsync(
                stream,
                async cmd =>
                {
                    receivedCommand = cmd;
                    await Task.CompletedTask;
                },
                CancellationToken.None);
        });

        // Give it time to process
        await Task.Delay(100);
        
        // Assert
        Assert.NotNull(receivedCommand);
        Assert.Equal("test", receivedCommand!.Command);
    }

    [Fact]
    public async Task Server_ShouldHandleMultipleJsonCommands()
    {
        // Arrange
        var stream = new MemoryStream();

        // Simulate client sending two JSON commands
        var jsonBytes1 = Encoding.UTF8.GetBytes("{\"command\":\"test1\"}");
        var jsonBytes2 = Encoding.UTF8.GetBytes("{\"command\":\"test2\"}");
        await stream.WriteAsync(jsonBytes1, 0, jsonBytes1.Length);
        await stream.WriteAsync(jsonBytes2, 0, jsonBytes2.Length);
        stream.Position = 0;

        var server = new DosProtocolServer();
        var receivedCommands = new List<DosCommand>();

        // Act
        var processTask = Task.Run(async () =>
        {
            await server.ProcessStreamAsync(
                stream,
                async cmd =>
                {
                    receivedCommands.Add(cmd);
                    await Task.CompletedTask;
                },
                CancellationToken.None);
        });

        // Give it time to process
        await Task.Delay(100);
        
        // Assert
        Assert.Equal(2, receivedCommands.Count);
        Assert.Equal("test1", receivedCommands[0].Command);
        Assert.Equal("test2", receivedCommands[1].Command);
    }

    [Fact]
    public void DosCommand_ShouldSerialize_WithJsonSourceGenerator()
    {
        // Arrange
        var command = new DosCommand("resize_buffer")
        {
            Width = 120,
            Height = 50,
            Text = "Test"
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(command, DosCommandContext.Default.DosCommand);

        // Assert
        Assert.Contains("\"command\":\"resize_buffer\"", json);
        Assert.Contains("\"width\":120", json);
        Assert.Contains("\"height\":50", json);
        Assert.Contains("\"text\":\"Test\"", json);
    }

    [Fact]
    public void DosCommand_ShouldNotSerialize_NullProperties()
    {
        // Arrange
        var command = new DosCommand("reset_buffer");

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(command, DosCommandContext.Default.DosCommand);

        // Assert
        Assert.Contains("\"command\":\"reset_buffer\"", json);
        Assert.DoesNotContain("width", json);
        Assert.DoesNotContain("height", json);
        Assert.DoesNotContain("text", json);
    }
}
