using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bat.Shared.Ipc;

/// <summary>
/// IPC request message sent from client to daemon.
/// Length-prefixed JSON over a named pipe.
/// </summary>
public sealed class IpcRequest
{
    /// <summary>Unique request ID for correlating responses.</summary>
    public int Id { get; set; }

    /// <summary>Operation type (e.g. "FileExists", "Execute", "GetSubsts").</summary>
    public string Type { get; set; } = "";

    /// <summary>Operation-specific payload as raw JSON.</summary>
    public JsonElement? Payload { get; set; }
}

/// <summary>
/// IPC response message sent from daemon to client.
/// </summary>
public sealed class IpcResponse
{
    /// <summary>Correlating request ID.</summary>
    public int Id { get; set; }

    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Error message when Success is false.</summary>
    public string? Error { get; set; }

    /// <summary>Operation-specific result payload as raw JSON.</summary>
    public JsonElement? Payload { get; set; }
}

/// <summary>
/// Serialization context for IPC messages (source-generated for performance).
/// </summary>
[JsonSerializable(typeof(IpcRequest))]
[JsonSerializable(typeof(IpcResponse))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class IpcJsonContext : JsonSerializerContext;
