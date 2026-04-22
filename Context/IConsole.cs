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
    Task<ConsoleKeyInfo> ReadKeyAsync(bool intercept, CancellationToken cancellationToken = default);
    IConsole WithOutput(TextWriter newOut);
    IConsole WithError(TextWriter newError);
    IConsole WithInput(TextReader newIn);

    /// <summary>
    /// Event raised when the terminal window is resized.
    /// Used by PTY to forward resize to child process.
    /// </summary>
    event Action<int, int>? Resized;
}
