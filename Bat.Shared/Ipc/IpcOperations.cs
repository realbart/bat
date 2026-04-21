namespace Bat.Shared.Ipc;

/// <summary>
/// Well-known IPC operation types for daemon communication.
/// IPC is only used for session registration and shared state sync.
/// All command execution and filesystem access runs in-process (cmd.exe loads batd assemblies).
/// </summary>
public static class IpcOperations
{
    // ── Session management ──────────────────────────────────────────────────
    public const string Ping = "Ping";
    public const string RegisterSession = "RegisterSession";
    public const string UnregisterSession = "UnregisterSession";

    // ── Shared state: drive mappings ────────────────────────────────────────
    public const string GetSubsts = "GetSubsts";
    public const string AddSubst = "AddSubst";
    public const string RemoveSubst = "RemoveSubst";
    public const string MergeSubsts = "MergeSubsts";
}
