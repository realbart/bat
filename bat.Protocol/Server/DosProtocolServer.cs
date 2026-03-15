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
    /// <returns>True if handshake succeeded</returns>
    public async Task<bool> WaitForHandshakeAsync(Stream stdout, Stream stdin, string handshake, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[handshake.Length + 1]; // \0 + handshake
        var totalRead = 0;

        while (totalRead < buffer.Length && !cancellationToken.IsCancellationRequested)
        {
            var bytesRead = await stdout.ReadAsync(buffer, totalRead, buffer.Length - totalRead, cancellationToken);
            if (bytesRead == 0) return false; // EOF
            totalRead += bytesRead;
        }

        // Verify: \0 + handshake
        if (buffer[0] != 0) return false;

        var receivedHandshake = Encoding.ASCII.GetString(buffer, 1, handshake.Length);
        if (receivedHandshake != handshake) return false;

        // Send handshake back
        var response = Encoding.ASCII.GetBytes(handshake);
        await stdin.WriteAsync(response, 0, response.Length, cancellationToken);
        await stdin.FlushAsync(cancellationToken);

        return true;
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
        Stream stdout,
        Stream stdin,
        Func<DosCommand, Task> commandHandler,
        Action<string> outputHandler,
        CancellationToken cancellationToken = default)
    {
        var buffer = new byte[8192];
        var jsonBuffer = new List<byte>();
        var inJson = false;
        var bracketCount = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var bytesRead = await stdout.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            if (bytesRead == 0) break; // EOF

            var i = 0;
            while (i < bytesRead)
            {
                if (buffer[i] == 0)
                {
                    // Null byte - potential JSON command start
                    // Check if next byte is '{'
                    if (i + 1 < bytesRead && buffer[i + 1] == '{')
                    {
                        inJson = true;
                        bracketCount = 0;
                        jsonBuffer.Clear();
                        i++; // Skip the \0
                        continue;
                    }
                    else
                    {
                        // Just a literal null byte in output
                        outputHandler("\0");
                        i++;
                        continue;
                    }
                }

                if (inJson)
                {
                    jsonBuffer.Add(buffer[i]);

                    if (buffer[i] == '{') bracketCount++;
                    else if (buffer[i] == '}')
                    {
                        bracketCount--;
                        if (bracketCount == 0)
                        {
                            // Complete JSON
                            var jsonString = Encoding.UTF8.GetString(jsonBuffer.ToArray());
                            await ProcessJsonCommandAsync(jsonString, commandHandler);
                            inJson = false;
                            jsonBuffer.Clear();
                        }
                    }

                    i++;
                }
                else
                {
                    // Regular output
                    var start = i;
                    while (i < bytesRead && buffer[i] != 0) i++;

                    var text = Encoding.Default.GetString(buffer, start, i - start);
                    outputHandler(text);
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
