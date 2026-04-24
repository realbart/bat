using Context;

namespace Bat.Pty;

/// <summary>
/// Runs an interactive process with PTY support.
/// Bridges between the SocketConsole I/O and the PTY.
/// </summary>
internal sealed class PtyProcessRunner : IDisposable
{
    private readonly IPseudoTerminal _pty;
    private readonly IConsole _console;
    private readonly CancellationTokenSource _cts = new();
    private Task? _outputTask;
    private bool _disposed;

    public PtyProcessRunner(IConsole console)
    {
        _console = console;
        _console.Resized += OnResized;
#if WINDOWS
        _pty = new ConPty();
#else
        _pty = new PosixPty();
#endif
    }

    private void OnResized(int columns, int rows)
    {
        if (!_disposed)
            _pty.Resize(columns, rows);
    }

    public void Start(string executable, string arguments, string workingDirectory, IDictionary<string, string>? environment, int columns, int rows)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _pty.Start(executable, arguments, workingDirectory, environment, columns, rows);

        // Start output forwarding task
        _outputTask = ForwardOutputAsync(_cts.Token);
    }

    public void Resize(int columns, int rows)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _pty.Resize(columns, rows);
    }

    public async Task WriteInputAsync(byte[] data, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _pty.WriteAsync(data, ct);
    }

    public async Task<int> WaitForExitAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var exitCode = await _pty.WaitForExitAsync(ct);

        // Wait for output forwarding to complete
        await _cts.CancelAsync();
        if (_outputTask != null)
        {
            try { await _outputTask; }
            catch (OperationCanceledException) { }
        }

        return exitCode;
    }

    private async Task ForwardOutputAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var bytesRead = await _pty.ReadAsync(buffer, ct);
                if (bytesRead == 0)
                    break;

                // Write PTY output to console
                var text = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                await _console.Out.WriteAsync(text);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _console.Resized -= OnResized;
        _cts.Cancel();
        _cts.Dispose();
        _pty.Dispose();
    }
}
