using System.Text;
using System.Text.Json;
using Bat.Protocol.Models;

namespace Bat.Protocol.Client;

/// <summary>
/// Client-side implementation of the DOS protocol for external applications.
/// Handles handshake, command sending, and response reading.
/// </summary>
public class DosProtocolClient
{
    private readonly Stream _stream;
    private bool _handshakeCompleted;

    public DosProtocolClient(Stream stream)
    {
        _stream = stream;
    }

    /// <summary>
    /// Performs the handshake with the DOS host.
    /// </summary>
    /// <param name="handshake">The handshake string (from DOS_HANDSHAKE environment variable)</param>
    /// <returns>True if handshake succeeded, false otherwise</returns>
    public async Task<bool> PerformHandshakeAsync(string handshake)
    {
        if (string.IsNullOrEmpty(handshake))
            return false;

        // Send: handshake
        var bytes = Encoding.ASCII.GetBytes(handshake);
        await _stream.WriteAsync(bytes, 0, bytes.Length);
        await _stream.FlushAsync();

        // Wait for response (handshake echo)
        var response = new byte[handshake.Length];
        var totalRead = 0;
        while (totalRead < response.Length)
        {
            var read = await _stream.ReadAsync(response, totalRead, response.Length - totalRead);
            if (read == 0) break;
            totalRead += read;
        }
        var responseStr = Encoding.ASCII.GetString(response, 0, totalRead);

        _handshakeCompleted = responseStr == handshake;
        return _handshakeCompleted;
    }

    /// <summary>
    /// Sends a command to the DOS host.
    /// </summary>
    public async Task SendCommandAsync(DosCommand command)
    {
        if (!_handshakeCompleted)
            throw new InvalidOperationException("Handshake must be completed before sending commands");

        var json = JsonSerializer.Serialize(command, DosCommandContext.Default.DosCommand);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _stream.WriteAsync(bytes, 0, bytes.Length);
        await _stream.FlushAsync();
    }

    /// <summary>
    /// Reads a response from the DOS host.
    /// </summary>
    /// <returns>The response, or null if stream ended</returns>
    public async Task<DosResponse?> ReadResponseAsync()
    {
        // Read JSON until closing brace
        var json = await ReadJsonAsync();
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            return JsonSerializer.Deserialize(json, DosResponseContext.Default.DosResponse);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Runs the command loop: reads responses and calls the handler until exit or EOF.
    /// </summary>
    /// <param name="handler">Handler that processes responses. Return false to exit loop.</param>
    public async Task RunCommandLoopAsync(Func<DosResponse, Task<bool>> handler)
    {
        while (true)
        {
            var response = await ReadResponseAsync();
            if (response == null) break;

            var continueLoop = await handler(response);
            if (!continueLoop) break;
        }
    }

    private async Task<string> ReadJsonAsync()
    {
        var sb = new StringBuilder();
        var bracketCount = 0;
        var started = false;
        var buffer = new byte[1];

        while (true)
        {
            var read = await _stream.ReadAsync(buffer, 0, 1);
            if (read == 0) break; // EOF

            var c = (char)buffer[0];
            sb.Append(c);

            if (c == '{')
            {
                bracketCount++;
                started = true;
            }
            else if (c == '}')
            {
                bracketCount--;
                if (started && bracketCount == 0)
                {
                    break; // Complete JSON
                }
            }
        }

        return sb.ToString();
    }
}
