namespace Context;

public interface IConsole
{
    TextWriter Error { get; }
    TextReader In { get; }
    TextWriter Out { get; }
    int WindowWidth { get; }
    int WindowHeight { get; }
    int CursorLeft { get; set; }
    bool IsInteractive { get; }
    bool IsNative { get; }
    Task<ConsoleKeyInfo> ReadKeyAsync(bool intercept, CancellationToken cancellationToken = default);
    IConsole WithOutput(TextWriter newOut);
    IConsole WithError(TextWriter newError);
    IConsole WithInput(TextReader newIn);

    /// <summary>
    /// Event raised when the terminal window is resized.
    /// Used by PTY to forward resize to child process.
    /// </summary>
    event Action<int, int>? Resized;

    /// <summary>
    /// Signals the terminal client to switch to raw byte input mode.
    /// In raw mode, keystrokes are sent as raw terminal bytes (VT sequences)
    /// instead of structured ConsoleKeyInfo messages.
    /// </summary>
    Task EnterRawModeAsync(CancellationToken ct = default);

    /// <summary>
    /// Signals the terminal client to switch back to structured key input mode.
    /// </summary>
    Task LeaveRawModeAsync(CancellationToken ct = default);

    /// <summary>
    /// Reads raw bytes from the terminal input stream.
    /// Only valid after <see cref="EnterRawModeAsync"/> has been called.
    /// Returns 0 when the raw mode session ends or the client disconnects.
    /// </summary>
    Task<int> ReadRawAsync(Memory<byte> buffer, CancellationToken ct = default);
}
