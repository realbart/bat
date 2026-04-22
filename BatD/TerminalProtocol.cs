using System.Buffers.Binary;
using System.Net.Sockets;

namespace Ipc;

/// <summary>
/// Message types for the bat ↔ batd terminal protocol.
/// Upstream (bat → batd): 0x01–0x7F. Downstream (batd → bat): 0x81–0xFF.
/// </summary>
public enum TerminalMessageType : byte
{
    // Upstream (bat → batd)
    Init = 0x01,
    Key = 0x02,
    Resize = 0x03,

    // Downstream (batd → bat)
    Out = 0x81,
    Err = 0x82,
    Exit = 0x83,
}

/// <summary>
/// Binary terminal protocol over Unix domain sockets.
/// Wire format: [1-byte type][4-byte big-endian length][payload]
/// </summary>
public static class TerminalProtocol
{
    private const int HeaderSize = 5; // 1 type + 4 length
    private const int MaxPayloadSize = 4 * 1024 * 1024;

    /// <summary>Returns the socket path for the current user.</summary>
    public static string GetSocketPath()
    {
        var tmp = OperatingSystem.IsWindows()
            ? Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath()
            : "/tmp";
        return Path.Combine(tmp, $"batd-{Environment.UserName}.sock");
    }

    /// <summary>Writes a framed message to the socket stream.</summary>
    public static async Task WriteAsync(Stream stream, TerminalMessageType type, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        var header = new byte[HeaderSize];
        header[0] = (byte)type;
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(1), payload.Length);
        await stream.WriteAsync(header, ct);
        if (payload.Length > 0)
            await stream.WriteAsync(payload, ct);
        await stream.FlushAsync(ct);
    }

    /// <summary>Reads a framed message from the socket stream. Returns null on EOF.</summary>
    public static async Task<(TerminalMessageType Type, byte[] Payload)?> ReadAsync(Stream stream, CancellationToken ct = default)
    {
        var header = new byte[HeaderSize];
        var read = await ReadExactAsync(stream, header, ct);
        if (read == 0) return null;
        if (read < HeaderSize) throw new IOException("Unexpected end of stream reading frame header.");

        var type = (TerminalMessageType)header[0];
        var length = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(1));
        if (length < 0 || length > MaxPayloadSize)
            throw new IOException($"Invalid payload length: {length}");

        var payload = new byte[length];
        if (length > 0)
        {
            read = await ReadExactAsync(stream, payload, ct);
            if (read < length) throw new IOException("Unexpected end of stream reading payload.");
        }

        return (type, payload);
    }

    // ── Init message ────────────────────────────────────────────────────────

    /// <summary>Sends the Init message with the full command line.</summary>
    public static Task WriteInitAsync(Stream stream, string commandLine, int windowWidth, int windowHeight, bool isInteractive, CancellationToken ct = default)
    {
        var cmdBytes = System.Text.Encoding.UTF8.GetBytes(commandLine);
        var payload = new byte[4 + 4 + 1 + cmdBytes.Length]; // width(4) + height(4) + interactive(1) + cmdLine
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(0), windowWidth);
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(4), windowHeight);
        payload[8] = isInteractive ? (byte)1 : (byte)0;
        cmdBytes.CopyTo(payload.AsSpan(9));
        return WriteAsync(stream, TerminalMessageType.Init, payload, ct);
    }

    /// <summary>Parses an Init payload.</summary>
    public static (string CommandLine, int WindowWidth, int WindowHeight, bool IsInteractive) ParseInit(byte[] payload)
    {
        var width = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(0));
        var height = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(4));
        var interactive = payload[8] != 0;
        var cmdLine = System.Text.Encoding.UTF8.GetString(payload, 9, payload.Length - 9);
        return (cmdLine, width, height, interactive);
    }

    // ── Key message ─────────────────────────────────────────────────────────

    /// <summary>Sends a ConsoleKeyInfo over the socket.</summary>
    public static Task WriteKeyAsync(Stream stream, ConsoleKeyInfo key, CancellationToken ct = default)
    {
        var payload = new byte[8];
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0), key.KeyChar);
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(2), (int)key.Key);
        payload[6] = (byte)(
            ((key.Modifiers & ConsoleModifiers.Shift) != 0 ? 1 : 0) |
            ((key.Modifiers & ConsoleModifiers.Alt) != 0 ? 2 : 0) |
            ((key.Modifiers & ConsoleModifiers.Control) != 0 ? 4 : 0));
        payload[7] = 0; // reserved
        return WriteAsync(stream, TerminalMessageType.Key, payload, ct);
    }

    /// <summary>Parses a Key payload into ConsoleKeyInfo.</summary>
    public static ConsoleKeyInfo ParseKey(byte[] payload)
    {
        var keyChar = (char)BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(0));
        var key = (ConsoleKey)BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(2));
        var mods = payload[6];
        return new ConsoleKeyInfo(keyChar, key,
            (mods & 1) != 0,
            (mods & 2) != 0,
            (mods & 4) != 0);
    }

    // ── Resize message ──────────────────────────────────────────────────────

    public static Task WriteResizeAsync(Stream stream, int width, int height, CancellationToken ct = default)
    {
        var payload = new byte[8];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(4), height);
        return WriteAsync(stream, TerminalMessageType.Resize, payload, ct);
    }

    public static (int Width, int Height) ParseResize(byte[] payload)
    {
        return (BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(0)),
                BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(4)));
    }

    // ── Out / Err messages ──────────────────────────────────────────────────

    public static Task WriteOutAsync(Stream stream, ReadOnlyMemory<byte> data, CancellationToken ct = default)
        => WriteAsync(stream, TerminalMessageType.Out, data, ct);

    public static Task WriteErrAsync(Stream stream, ReadOnlyMemory<byte> data, CancellationToken ct = default)
        => WriteAsync(stream, TerminalMessageType.Err, data, ct);

    // ── Exit message ────────────────────────────────────────────────────────

    public static Task WriteExitAsync(Stream stream, int exitCode, CancellationToken ct = default)
    {
        var payload = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(payload, exitCode);
        return WriteAsync(stream, TerminalMessageType.Exit, payload, ct);
    }

    public static int ParseExitCode(byte[] payload)
        => BinaryPrimitives.ReadInt32BigEndian(payload);

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct);
            if (read == 0) return totalRead;
            totalRead += read;
        }
        return totalRead;
    }
}
