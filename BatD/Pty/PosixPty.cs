#if UNIX
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BatD.Pty;

/// <summary>
/// POSIX PTY implementation using posix_openpt + Process.Start (no fork).
/// Avoids the fork-safety issues with .NET's GC/threadpool.
/// </summary>
internal sealed partial class PosixPty : global::Context.IPseudoTerminal
{
    private int _masterFd = -1;
    private int _slaveFd = -1;
    private Process? _process;
    private bool _disposed;

    public int ProcessId => _process?.Id ?? -1;
    public bool HasExited => _process?.HasExited ?? true;

    public void Start(string executable, string arguments, string workingDirectory, IDictionary<string, string>? environment, int columns, int rows)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        columns = Math.Max(1, columns);
        rows = Math.Max(1, rows);

        // Open a new PTY master
        _masterFd = PosixOpenpt(O_RDWR | O_NOCTTY);
        if (_masterFd < 0)
            throw new InvalidOperationException($"posix_openpt failed with errno {Marshal.GetLastPInvokeError()}");

        if (Grantpt(_masterFd) != 0)
            throw new InvalidOperationException($"grantpt failed with errno {Marshal.GetLastPInvokeError()}");

        if (Unlockpt(_masterFd) != 0)
            throw new InvalidOperationException($"unlockpt failed with errno {Marshal.GetLastPInvokeError()}");

        var slaveNamePtr = Ptsname(_masterFd);
        if (slaveNamePtr == nint.Zero)
            throw new InvalidOperationException($"ptsname failed with errno {Marshal.GetLastPInvokeError()}");
        var slaveName = Marshal.PtrToStringUTF8(slaveNamePtr)!;

        // Set window size on master
        var winSize = new WinSize { ws_col = (ushort)columns, ws_row = (ushort)rows };
        Ioctl(_masterFd, TIOCSWINSZ, ref winSize);

        // Set master to non-blocking for async operations
        var flags = Fcntl(_masterFd, F_GETFL, 0);
        Fcntl(_masterFd, F_SETFL, flags | O_NONBLOCK);

        // Open slave fd
        _slaveFd = Open(slaveName, O_RDWR, 0);
        if (_slaveFd < 0)
            throw new InvalidOperationException($"open slave failed with errno {Marshal.GetLastPInvokeError()}");

        // Use a helper shell script to attach the slave PTY to the child's stdio,
        // create a new session (setsid), and exec the target.
        // This avoids fork() entirely from .NET.
        var psi = new ProcessStartInfo("/bin/bash")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? null : workingDirectory,
        };

        if (environment != null)
        {
            foreach (var kvp in environment)
                psi.Environment[kvp.Key] = kvp.Value;
        }

        // Pass slave PTY path as env var; the child script will use it
        psi.Environment["__BAT_SLAVE_PTY"] = slaveName;
        psi.Environment["TERM"] = "xterm-256color";

        _process = Process.Start(psi);
        if (_process == null)
            throw new InvalidOperationException("Failed to start child process");

        // Send the shell script that attaches to the slave PTY and execs the command
        var shellScript = $"exec setsid -w bash -c 'exec 0<>\"{slaveName}\" 1>&0 2>&0; exec {EscapeForShell(executable)} {arguments}'";
        _process.StandardInput.WriteLine(shellScript);
        _process.StandardInput.Close();

        // Close slave in parent — child has it now
        Close(_slaveFd);
        _slaveFd = -1;
    }

    private static string EscapeForShell(string s)
    {
        if (s.Contains('\''))
            return "'" + s.Replace("'", "'\\''") + "'";
        if (s.Contains(' ') || s.Contains('"') || s.Contains('$') || s.Contains('`'))
            return "'" + s + "'";
        return s;
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
        if (_disposed || _masterFd < 0) return 0;

        return await Task.Run(() =>
        {
            unsafe
            {
                using var pin = buffer.Pin();
                while (!ct.IsCancellationRequested)
                {
                    if (_masterFd < 0) return 0;
                    var result = Read(_masterFd, (nint)pin.Pointer, (nuint)buffer.Length);
                    if (result < 0)
                    {
                        var errno = Marshal.GetLastPInvokeError();
                        if (errno == EAGAIN || errno == EWOULDBLOCK)
                        {
                            Thread.Sleep(1);
                            continue;
                        }
                        if (errno == EIO)
                            return 0; // PTY slave closed
                        throw new InvalidOperationException($"read failed with errno {errno}");
                    }
                    if (result == 0) return 0;
                    return (int)result;
                }
                return 0;
            }
        }, ct);
    }

    public void Resize(int columns, int rows)
    {
        if (_disposed || _masterFd < 0) return;
        var winSize = new WinSize { ws_col = (ushort)columns, ws_row = (ushort)rows };
        Ioctl(_masterFd, TIOCSWINSZ, ref winSize);
    }

    public async Task<int> WaitForExitAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_process == null) return -1;

        await _process.WaitForExitAsync(ct);
        return _process.ExitCode;
    }

    public void ClosePseudoConsoleHandle()
    {
        if (_masterFd >= 0)
        {
            Close(_masterFd);
            _masterFd = -1;
        }
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
        if (_slaveFd >= 0)
        {
            Close(_slaveFd);
            _slaveFd = -1;
        }
        _process?.Dispose();
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

    // Constants
    private const int EAGAIN = 11;
    private const int EWOULDBLOCK = EAGAIN;
    private const int EIO = 5;
    private const int F_GETFL = 3;
    private const int F_SETFL = 4;
    private const int O_RDWR = 2;
    private const int O_NOCTTY = 256;
    private const int O_NONBLOCK = 2048;
    private const uint TIOCSWINSZ = 0x5414;

    [StructLayout(LayoutKind.Sequential)]
    private struct WinSize
    {
        public ushort ws_row;
        public ushort ws_col;
        public ushort ws_xpixel;
        public ushort ws_ypixel;
    }

    [LibraryImport("libc", EntryPoint = "posix_openpt", SetLastError = true)]
    private static partial int PosixOpenpt(int flags);

    [LibraryImport("libc", EntryPoint = "grantpt", SetLastError = true)]
    private static partial int Grantpt(int fd);

    [LibraryImport("libc", EntryPoint = "unlockpt", SetLastError = true)]
    private static partial int Unlockpt(int fd);

    [LibraryImport("libc", EntryPoint = "ptsname", SetLastError = true)]
    private static partial nint Ptsname(int fd);

    [LibraryImport("libc", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Open(string path, int flags, int mode);

    [LibraryImport("libc", EntryPoint = "read", SetLastError = true)]
    private static partial nint Read(int fd, nint buf, nuint count);

    [LibraryImport("libc", EntryPoint = "write", SetLastError = true)]
    private static partial nint Write(int fd, nint buf, nuint count);

    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    private static partial int Close(int fd);

    [LibraryImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static partial int Ioctl(int fd, uint request, ref WinSize winp);

    [LibraryImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    private static partial int Fcntl(int fd, int cmd, int arg);
}
#endif
