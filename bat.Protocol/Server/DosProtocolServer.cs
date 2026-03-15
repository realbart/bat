using System.Text;
using System.Text.Json;
using Bat.Protocol.Models;

namespace Bat.Protocol.Server;

/// <summary>
/// Server-side implementation of the DOS protocol.
/// Handles handshake verification and stream processing for external applications.
/// </summary>
public class DosProtocolServer
{
    /// <summary>
    /// Waits for and validates the handshake from a client application.
    /// </summary>
    /// <param name="stdout">Client's stdout stream to read from</param>
    /// <param name="stdin">Client's stdin stream to write to</param>
    /// <param name="handshake">Expected handshake string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HandshakeResult containing success and consumed bytes</returns>
    public async Task<HandshakeResult> WaitForHandshakeAsync(Stream stream, string handshake, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[handshake.Length];
        var totalRead = 0;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(1)); // Iets langer voor de pipe

        try
        {
            while (totalRead < buffer.Length && !cts.Token.IsCancellationRequested)
            {
                var bytesRead = await stream.ReadAsync(buffer, totalRead, buffer.Length - totalRead, cts.Token);
                if (bytesRead == 0) break;
                totalRead += bytesRead;
            }
        }
        catch (OperationCanceledException) { }

        if (totalRead >= buffer.Length)
        {
            var receivedHandshake = Encoding.ASCII.GetString(buffer, 0, handshake.Length);
            if (receivedHandshake == handshake)
            {
                // Echo back
                await stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                return new HandshakeResult { Success = true };
            }
        }

        return new HandshakeResult { Success = false };
    }

    /// <summary>
    /// Processes the stream: handles JSON commands from client and regular output.
    /// </summary>
    /// <param name="stdout">Client's stdout stream</param>
    /// <param name="stdin">Client's stdin stream (for responses)</param>
    /// <param name="commandHandler">Handler for JSON commands</param>
    /// <param name="outputHandler">Handler for regular text output</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task ProcessStreamAsync(
        Stream stream,
        Func<DosCommand, Task> commandHandler,
        CancellationToken cancellationToken = default)
    {
        var buffer = new byte[8192];
        var jsonBuffer = new List<byte>();
        var bracketCount = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            if (bytesRead == 0) break; // EOF

            for (int i = 0; i < bytesRead; i++)
            {
                jsonBuffer.Add(buffer[i]);

                if (buffer[i] == '{') bracketCount++;
                else if (buffer[i] == '}')
                {
                    bracketCount--;
                    if (bracketCount == 0 && jsonBuffer.Count > 0)
                    {
                        // Complete JSON
                        var jsonString = Encoding.UTF8.GetString(jsonBuffer.ToArray());
                        await ProcessJsonCommandAsync(jsonString, commandHandler);
                        jsonBuffer.Clear();
                    }
                }
            }
        }
    }

    private async Task ProcessJsonCommandAsync(string json, Func<DosCommand, Task> commandHandler)
    {
        try
        {
            var command = JsonSerializer.Deserialize(json, DosCommandContext.Default.DosCommand);
            if (command != null)
            {
                await commandHandler(command);
            }
        }
        catch
        {
            // Ignore malformed JSON
        }
    }
}
