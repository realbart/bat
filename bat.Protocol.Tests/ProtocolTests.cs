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
        var inputStream = new MemoryStream();
        var outputStream = new MemoryStream();
        var inputReader = new StreamReader(inputStream);
        var outputWriter = new StreamWriter(outputStream) { AutoFlush = true };

        var client = new DosProtocolClient(inputReader, outputWriter);

        // Simulate server response: echo handshake back
        var serverResponse = Encoding.ASCII.GetBytes(handshake);
        await inputStream.WriteAsync(serverResponse, 0, serverResponse.Length);
        inputStream.Position = 0;

        // Act
        var result = await client.PerformHandshakeAsync(handshake);

        // Assert
        Assert.True(result);

        // Verify client sent: \0 + handshake
        outputStream.Position = 0;
        var sentData = outputStream.ToArray();
        Assert.Equal(0, sentData[0]); // Null byte
        var sentHandshake = Encoding.ASCII.GetString(sentData, 1, handshake.Length);
        Assert.Equal(handshake, sentHandshake);
    }

    [Fact]
    public async Task Server_ShouldWaitForHandshake_Successfully()
    {
        // Arrange
        var handshake = "abcd5678";
        var clientOutput = new MemoryStream();
        var serverInput = new MemoryStream();

        // Simulate client sending: \0 + handshake
        clientOutput.WriteByte(0);
        var handshakeBytes = Encoding.ASCII.GetBytes(handshake);
        await clientOutput.WriteAsync(handshakeBytes, 0, handshakeBytes.Length);
        clientOutput.Position = 0;

        var server = new DosProtocolServer();

        // Act
        var result = await server.WaitForHandshakeAsync(clientOutput, serverInput, handshake);

        // Assert
        Assert.True(result);

        // Verify server sent handshake back
        serverInput.Position = 0;
        var response = new byte[handshake.Length];
        await serverInput.ReadAsync(response, 0, response.Length);
        var responseStr = Encoding.ASCII.GetString(response);
        Assert.Equal(handshake, responseStr);
    }

    [Fact]
    public async Task Client_ShouldSendCommand_AsJson()
    {
        // Arrange
        var handshake = "test1234";
        var inputStream = new MemoryStream();
        var outputStream = new MemoryStream();

        // Echo handshake for initial handshake
        var serverResponse = Encoding.ASCII.GetBytes(handshake);
        await inputStream.WriteAsync(serverResponse, 0, serverResponse.Length);
        inputStream.Position = 0;

        var inputReader = new StreamReader(inputStream);
        var outputWriter = new StreamWriter(outputStream) { AutoFlush = true };
        var client = new DosProtocolClient(inputReader, outputWriter);

        await client.PerformHandshakeAsync(handshake);

        // Act
        var command = new DosCommand("reset_buffer");
        await client.SendCommandAsync(command);

        // Assert
        outputStream.Position = handshake.Length + 1; // Skip handshake output
        var commandData = outputStream.ToArray()[(handshake.Length + 1)..];
        
        Assert.Equal(0, commandData[0]); // Null byte
        var jsonStr = Encoding.UTF8.GetString(commandData, 1, commandData.Length - 1);
        Assert.Contains("\"command\":\"reset_buffer\"", jsonStr);
    }

    [Fact]
    public async Task Server_ShouldProcessJsonCommands()
    {
        // Arrange
        var clientOutput = new MemoryStream();
        var serverInput = new MemoryStream();

        // Simulate client sending JSON command: \0{"command":"test"}
        clientOutput.WriteByte(0);
        var jsonBytes = Encoding.UTF8.GetBytes("{\"command\":\"test\"}");
        await clientOutput.WriteAsync(jsonBytes, 0, jsonBytes.Length);
        clientOutput.Position = 0;

        var server = new DosProtocolServer();
        DosCommand? receivedCommand = null;

        // Act
        var processTask = Task.Run(async () =>
        {
            await server.ProcessStreamAsync(
                clientOutput,
                serverInput,
                async cmd =>
                {
                    receivedCommand = cmd;
                    await Task.CompletedTask;
                },
                text => { },
                CancellationToken.None);
        });

        // Give it time to process
        await Task.Delay(100);
        
        // Assert
        Assert.NotNull(receivedCommand);
        Assert.Equal("test", receivedCommand!.Command);
    }

    [Fact]
    public async Task Server_ShouldHandleRegularOutput()
    {
        // Arrange
        var clientOutput = new MemoryStream();
        var serverInput = new MemoryStream();

        // Simulate client sending regular text (no null byte prefix)
        var textBytes = Encoding.UTF8.GetBytes("Hello World");
        await clientOutput.WriteAsync(textBytes, 0, textBytes.Length);
        clientOutput.Position = 0;

        var server = new DosProtocolServer();
        var receivedOutput = new StringBuilder();

        // Act
        var cts = new CancellationTokenSource();
        var processTask = Task.Run(async () =>
        {
            await server.ProcessStreamAsync(
                clientOutput,
                serverInput,
                async cmd => await Task.CompletedTask,
                text => receivedOutput.Append(text),
                cts.Token);
        });

        await Task.Delay(100);
        cts.Cancel();

        try { await processTask; } catch { }

        // Assert
        Assert.Contains("Hello World", receivedOutput.ToString());
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
