namespace Context;

/// <summary>
/// Platform-agnostic interface for pseudo-terminal operations.
/// Implementations: ConPty (Windows), PosixPty (Linux/macOS).
/// </summary>
public interface IPseudoTerminal : IDisposable
{
    /// <summary>
    /// Spawns a process attached to this PTY.
    /// </summary>
    void Start(string executable, string arguments, string workingDirectory, IDictionary<string, string>? environment, int columns, int rows);

    /// <summary>
    /// Writes input to the PTY (keystrokes from the user).
    /// </summary>
    Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);

    /// <summary>
    /// Reads output from the PTY (program output).
    /// Returns 0 when the process has exited and all output has been read.
    /// </summary>
    Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default);

    /// <summary>
    /// Resizes the PTY to the specified dimensions.
    /// </summary>
    void Resize(int columns, int rows);

    /// <summary>
    /// Waits for the process to exit and returns the exit code.
    /// </summary>
    Task<int> WaitForExitAsync(CancellationToken ct = default);

    /// <summary>
    /// Closes the pseudoconsole handle to signal EOF on the output pipe.
    /// </summary>
    void ClosePseudoConsoleHandle();

    /// <summary>
    /// Gets the process ID of the spawned process.
    /// </summary>
    int ProcessId { get; }

    /// <summary>
    /// Gets whether the process has exited.
    /// </summary>
    bool HasExited { get; }
}
