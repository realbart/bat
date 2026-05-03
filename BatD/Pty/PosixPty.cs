#if UNIX
using System.Runtime.InteropServices;
using System.Text;

namespace BatD.Pty;

/// <summary>
/// POSIX PTY implementation using forkpty.
/// Works on Linux, macOS, and other POSIX-compliant systems.
/// </summary>
internal sealed partial class PosixPty : global::Context.IPseudoTerminal
{
    private int _masterFd = -1;
    private int _childPid = -1;
    private bool _disposed;

    public int ProcessId => _childPid;
    public bool HasExited => _childPid > 0 && Waitpid(_childPid, out _, WNOHANG) != 0;

    public void Start(string executable, string arguments, string workingDirectory, IDictionary<string, string>? environment, int columns, int rows)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        columns = Math.Max(1, columns);
        rows = Math.Max(1, rows);
        var winSize = new WinSize { ws_col = (ushort)columns, ws_row = (ushort)rows, ws_xpixel = 0, ws_ypixel = 0 };

        // forkpty creates master/slave PTY pair and forks
        var pid = ForkPty(out _masterFd, nint.Zero, nint.Zero, ref winSize);

        if (pid < 0)
            throw new InvalidOperationException($"forkpty failed with errno {Marshal.GetLastPInvokeError()}");

        if (pid == 0)
        {
            // Child process
            if (!string.IsNullOrEmpty(workingDirectory))
                Environment.CurrentDirectory = workingDirectory;

            // Set environment variables
            if (environment != null)
            {
                foreach (var kvp in environment)
                    Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
            }

            // Parse arguments and exec
            var args = ParseArguments(arguments);
            var argv = new string[args.Length + 2];
            argv[0] = executable;
            for (var i = 0; i < args.Length; i++)
                argv[i + 1] = args[i];

            Execvp(executable, argv);

            // If we get here, exec failed
            Environment.Exit(127);
        }

        // Parent process
        _childPid = pid;

        // Set master to non-blocking for async operations
        var flags = Fcntl(_masterFd, F_GETFL, 0);
        Fcntl(_masterFd, F_SETFL, flags | O_NONBLOCK);
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed || _masterFd < 0, this);

        await Task.Run(() =>
        {
            unsafe
            {
                using var pin = data.Pin();
                var written = 0;
                while (written < data.Length)
                {
                    ct.ThrowIfCancellationRequested();
                    var result = Write(_masterFd, (nint)pin.Pointer + written, (nuint)(data.Length - written));
                    if (result < 0)
                    {
                        var errno = Marshal.GetLastPInvokeError();
                        if (errno == EAGAIN || errno == EWOULDBLOCK)
                        {
                            Thread.Sleep(1);
                            continue;
                        }
                        throw new InvalidOperationException($"write failed with errno {errno}");
                    }
                    written += (int)result;
                }
            }
        }, ct);
    }

    public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed || _masterFd < 0, this);

        return await Task.Run(() =>
        {
            unsafe
            {
                using var pin = buffer.Pin();
                while (!ct.IsCancellationRequested)
                {
                    var result = Read(_masterFd, (nint)pin.Pointer, (nuint)buffer.Length);
                    if (result < 0)
                    {
                        var errno = Marshal.GetLastPInvokeError();
                        if (errno == EAGAIN || errno == EWOULDBLOCK)
                        {
                            // Check if child has exited
                            if (Waitpid(_childPid, out _, WNOHANG) != 0)
                                return 0;
                            Thread.Sleep(1);
                            continue;
                        }
                        if (errno == EIO)
                            return 0; // PTY closed
                        throw new InvalidOperationException($"read failed with errno {errno}");
                    }
                    return (int)result;
                }
                return 0;
            }
        }, ct);
    }

    public void Resize(int columns, int rows)
    {
        ObjectDisposedException.ThrowIf(_disposed || _masterFd < 0, this);
        var winSize = new WinSize { ws_col = (ushort)columns, ws_row = (ushort)rows };
        Ioctl(_masterFd, TIOCSWINSZ, ref winSize);
    }

    public async Task<int> WaitForExitAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed || _childPid < 0, this);

        return await Task.Run(() =>
        {
            while (!ct.IsCancellationRequested)
            {
                var result = Waitpid(_childPid, out var status, 0);
                if (result == _childPid)
                {
                    if (WIFEXITED(status))
                        return WEXITSTATUS(status);
                    if (WIFSIGNALED(status))
                        return 128 + WTERMSIG(status);
                    return -1;
                }
                if (result < 0)
                {
                    var errno = Marshal.GetLastPInvokeError();
                    if (errno == EINTR)
                        continue;
                    throw new InvalidOperationException($"waitpid failed with errno {errno}");
                }
            }
            return -1;
        }, ct);
    }

    public void ClosePseudoConsoleHandle()
    {
        // On POSIX, closing the master fd signals EOF; handled in Dispose.
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_masterFd >= 0)
        {
            Close(_masterFd);
            _masterFd = -1;
        }

        if (_childPid > 0)
        {
            Kill(_childPid, SIGTERM);
            Waitpid(_childPid, out _, 0);
            _childPid = -1;
        }
    }

    private static string[] ParseArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return [];

        var args = new List<string>();
        var current = new StringBuilder();
        var inQuote = false;
        var escape = false;

        foreach (var c in arguments)
        {
            if (escape)
            {
                current.Append(c);
                escape = false;
            }
            else if (c == '\\')
            {
                escape = true;
            }
            else if (c == '"')
            {
                inQuote = !inQuote;
            }
            else if (c == ' ' && !inQuote)
            {
                if (current.Length > 0)
                {
                    args.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            args.Add(current.ToString());

        return [.. args];
    }

    // Wait status macros
    private static bool WIFEXITED(int status) => (status & 0x7F) == 0;
    private static int WEXITSTATUS(int status) => (status >> 8) & 0xFF;
    private static bool WIFSIGNALED(int status) => ((status & 0x7F) + 1) >> 1 > 0;
    private static int WTERMSIG(int status) => status & 0x7F;

    // Constants
    private const int WNOHANG = 1;
    private const int EINTR = 4;
    private const int EAGAIN = 11;
    private const int EWOULDBLOCK = EAGAIN;
    private const int EIO = 5;
    private const int F_GETFL = 3;
    private const int F_SETFL = 4;
    private const int O_NONBLOCK = 2048;
    private const int SIGTERM = 15;
    private const uint TIOCSWINSZ = 0x5414;

    [StructLayout(LayoutKind.Sequential)]
    private struct WinSize
    {
        public ushort ws_row;
        public ushort ws_col;
        public ushort ws_xpixel;
        public ushort ws_ypixel;
    }

    [LibraryImport("libc", SetLastError = true)]
    private static partial int ForkPty(out int master, nint name, nint termp, ref WinSize winp);

    [LibraryImport("libc", SetLastError = true)]
    private static partial nint Read(int fd, nint buf, nuint count);

    [LibraryImport("libc", SetLastError = true)]
    private static partial nint Write(int fd, nint buf, nuint count);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int Close(int fd);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int Ioctl(int fd, uint request, ref WinSize winp);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int Waitpid(int pid, out int status, int options);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int Kill(int pid, int sig);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int Fcntl(int fd, int cmd, int arg);

    [LibraryImport("libc", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Execvp(string file, string?[] argv);
}
#endif
