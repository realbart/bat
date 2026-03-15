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
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private bool _handshakeCompleted;

    public DosProtocolClient(TextReader input, TextWriter output)
    {
        _input = input;
        _output = output;
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

        // Send: \0 + handshake
        await _output.WriteAsync('\0');
        await _output.WriteAsync(handshake);
        await _output.FlushAsync();

        // Wait for response (handshake echo)
        var response = new char[handshake.Length];
        var read = await _input.ReadAsync(response, 0, handshake.Length);
        var responseStr = new string(response, 0, read);

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
        await _output.WriteAsync('\0');
        await _output.WriteAsync(json);
        await _output.FlushAsync();
    }

    /// <summary>
    /// Reads a response from the DOS host.
    /// </summary>
    /// <returns>The response, or null if stream ended</returns>
    public async Task<DosResponse?> ReadResponseAsync()
    {
        // Read until \0
        var ch = _input.Read();
        if (ch == -1) return null; // EOF
        if (ch != 0) return null; // Invalid protocol

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

        while (true)
        {
            var ch = _input.Read();
            if (ch == -1) break; // EOF

            var c = (char)ch;
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
