using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Bat.Shared.Ipc;

/// <summary>
/// Reads and writes length-prefixed JSON messages over a stream.
/// Wire format: [4-byte big-endian length][UTF-8 JSON payload]
/// </summary>
public static class IpcProtocol
{
    private const int MaxMessageSize = 16 * 1024 * 1024; // 16 MB safety limit

    /// <summary>
    /// Writes a message to the stream as length-prefixed JSON.
    /// </summary>
    public static async Task WriteAsync<T>(Stream stream, T message, JsonTypeInfo<T> typeInfo, CancellationToken ct = default)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(message, typeInfo);
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, json.Length);
        await stream.WriteAsync(header, ct);
        await stream.WriteAsync(json, ct);
        await stream.FlushAsync(ct);
    }

    /// <summary>
    /// Reads a length-prefixed JSON message from the stream.
    /// Returns null on end-of-stream.
    /// </summary>
    public static async Task<T?> ReadAsync<T>(Stream stream, JsonTypeInfo<T> typeInfo, CancellationToken ct = default) where T : class
    {
        var header = new byte[4];
        var bytesRead = await ReadExactAsync(stream, header, ct);
        if (bytesRead == 0) return null; // EOF
        if (bytesRead < 4) throw new InvalidOperationException("Unexpected end of stream reading message header.");

        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length <= 0 || length > MaxMessageSize)
            throw new InvalidOperationException($"Invalid message length: {length}");

        var payload = new byte[length];
        bytesRead = await ReadExactAsync(stream, payload, ct);
        if (bytesRead < length) throw new InvalidOperationException("Unexpected end of stream reading message payload.");

        return JsonSerializer.Deserialize(payload, typeInfo);
    }

    /// <summary>
    /// Writes an IPC request to the stream.
    /// </summary>
    public static Task WriteRequestAsync(Stream stream, IpcRequest request, CancellationToken ct = default)
        => WriteAsync(stream, request, IpcJsonContext.Default.IpcRequest, ct);

    /// <summary>
    /// Reads an IPC request from the stream. Returns null on EOF.
    /// </summary>
    public static Task<IpcRequest?> ReadRequestAsync(Stream stream, CancellationToken ct = default)
        => ReadAsync(stream, IpcJsonContext.Default.IpcRequest, ct);

    /// <summary>
    /// Writes an IPC response to the stream.
    /// </summary>
    public static Task WriteResponseAsync(Stream stream, IpcResponse response, CancellationToken ct = default)
        => WriteAsync(stream, response, IpcJsonContext.Default.IpcResponse, ct);

    /// <summary>
    /// Reads an IPC response from the stream. Returns null on EOF.
    /// </summary>
    public static Task<IpcResponse?> ReadResponseAsync(Stream stream, CancellationToken ct = default)
        => ReadAsync(stream, IpcJsonContext.Default.IpcResponse, ct);

    /// <summary>
    /// Creates a JSON payload element from a value.
    /// </summary>
    public static JsonElement ToPayload<T>(T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        using var doc = JsonDocument.Parse(bytes);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Deserializes a JSON payload element to a typed value.
    /// </summary>
    public static T? FromPayload<T>(JsonElement element)
        => JsonSerializer.Deserialize<T>(element.GetRawText());

    /// <summary>
    /// Returns the pipe name for the current user.
    /// </summary>
    public static string GetPipeName()
        => $"batd-{Environment.UserName}";

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct);
            if (read == 0) return totalRead; // EOF
            totalRead += read;
        }
        return totalRead;
    }
}
