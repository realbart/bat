// ConPty: Windows ConPTY implementation.
// Self-contained – no dependency on the rest of the Bat solution.
// Based on https://learn.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

#pragma warning disable CA1416 // Windows-only

internal sealed class ConPty : IDisposable
{
    private SafeFileHandle? _inputWriteHandle;
    private SafeFileHandle? _outputReadHandle;
    private nint _hPC;
    private SafeProcessHandle? _processHandle;
    private int _processId;
    private bool _disposed;

    public int ProcessId => _processId;
    public bool HasExited => _processHandle != null && WaitForSingleObject(_processHandle.DangerousGetHandle(), 0) == 0;

    public void Start(string executable, string arguments, string workingDirectory, int columns, int rows)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        CreatePipePair(out var inputRead, out var inputWrite);
        CreatePipePair(out var outputRead, out var outputWrite);

        _inputWriteHandle = inputWrite;
        _outputReadHandle = outputRead;

        columns = Math.Max(1, columns);
        rows    = Math.Max(1, rows);
        var size = new COORD { X = (short)columns, Y = (short)rows };
        var hr = CreatePseudoConsole(size, inputRead.DangerousGetHandle(), outputWrite.DangerousGetHandle(), 0, out _hPC);
        if (hr != 0)
            Marshal.ThrowExceptionForHR(hr);

        // The PTY owns these ends now
        inputRead.Dispose();
        outputWrite.Dispose();

        var attrList = AllocAttributeList();
        try
        {
            var siEx = new STARTUPINFOEXW();
            siEx.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEXW>();
            siEx.lpAttributeList = attrList;

            var cmdLine = string.IsNullOrEmpty(arguments)
                ? $"\"{executable}\""
                : $"\"{executable}\" {arguments}";

            if (!CreateProcessW(
                    null, cmdLine,
                    nint.Zero, nint.Zero,
                    false,
                    EXTENDED_STARTUPINFO_PRESENT,
                    nint.Zero, workingDirectory,
                    ref siEx, out var pi))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            _processHandle = new SafeProcessHandle(pi.hProcess, true);
            _processId = pi.dwProcessId;
            CloseHandle(pi.hThread);
        }
        finally
        {
            FreeAttributeList(attrList);
        }
    }

    public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed || _inputWriteHandle == null, this);
        var h = _inputWriteHandle.DangerousGetHandle();
        return Task.Run(() =>
        {
            unsafe
            {
                fixed (byte* p = data.Span)
                    WriteFile(h, p, (uint)data.Length, out _, nint.Zero);
            }
        }, ct);
    }

    public Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed || _outputReadHandle == null, this);
        var h = _outputReadHandle.DangerousGetHandle();
        return Task.Run(() =>
        {
            try
            {
                unsafe
                {
                    fixed (byte* p = buffer.Span)
                    {
                        if (ReadFile(h, p, (uint)buffer.Length, out var bytesRead, nint.Zero))
                            return (int)bytesRead;
                        return 0;
                    }
                }
            }
            catch { return 0; }
        }, ct);
    }

    public void Resize(int columns, int rows)
    {
        if (_disposed || _hPC == nint.Zero) return;
        columns = Math.Max(1, columns);
        rows    = Math.Max(1, rows);
        ResizePseudoConsole(_hPC, new COORD { X = (short)columns, Y = (short)rows });
    }

    public async Task<int> WaitForExitAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed || _processHandle == null, this);
        var h = _processHandle.DangerousGetHandle();
        await Task.Run(() => WaitForSingleObject(h, 0xFFFFFFFF), ct);
        GetExitCodeProcess(h, out var code);
        return (int)code;
    }

    /// <summary>
    /// Closes the pseudoconsole handle to signal EOF on the output pipe.
    /// Call this after the child process exits, before draining remaining output.
    /// </summary>
    public void ClosePseudoConsoleHandle()
    {
        if (_hPC != nint.Zero) { ClosePseudoConsole(_hPC); _hPC = nint.Zero; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _inputWriteHandle?.Dispose();
        _outputReadHandle?.Dispose();
        _processHandle?.Dispose();
        ClosePseudoConsoleHandle();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static void CreatePipePair(out SafeFileHandle read, out SafeFileHandle write)
    {
        if (!CreatePipe(out var r, out var w, nint.Zero, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        read  = new SafeFileHandle(r, true);
        write = new SafeFileHandle(w, true);
    }

    private nint AllocAttributeList()
    {
        nint size = 0;
        InitializeProcThreadAttributeList(nint.Zero, 1, 0, ref size);
        var list = Marshal.AllocHGlobal(size);
        if (!InitializeProcThreadAttributeList(list, 1, 0, ref size))
        {
            Marshal.FreeHGlobal(list);
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        if (!UpdateProcThreadAttribute(list, 0, PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                _hPC, (nint)nint.Size, nint.Zero, nint.Zero))
        {
            DeleteProcThreadAttributeList(list);
            Marshal.FreeHGlobal(list);
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        return list;
    }

    private static void FreeAttributeList(nint list)
    {
        DeleteProcThreadAttributeList(list);
        Marshal.FreeHGlobal(list);
    }

    // ── Constants / Structs / P/Invokes ─────────────────────────────────────

    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    private static readonly nint PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;

    [StructLayout(LayoutKind.Sequential)] private struct COORD { public short X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFOEXW
    {
        public STARTUPINFOW StartupInfo;
        public nint lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFOW
    {
        public int cb;
        public nint lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public nint lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public nint hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [DllImport("kernel32.dll")]
    private static extern int CreatePseudoConsole(COORD size, nint hInput, nint hOutput, uint flags, out nint phPC);

    [DllImport("kernel32.dll")]
    private static extern int ResizePseudoConsole(nint hPC, COORD size);

    [DllImport("kernel32.dll")]
    private static extern void ClosePseudoConsole(nint hPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreatePipe(out nint read, out nint write, nint sa, uint size);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint h);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern unsafe bool ReadFile(nint hFile, byte* lpBuffer, uint nToRead, out uint nRead, nint overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern unsafe bool WriteFile(nint hFile, byte* lpBuffer, uint nToWrite, out uint nWritten, nint overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(nint list, int count, uint flags, ref nint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(nint list, uint flags, nint attr, nint value, nint cbSize, nint prev, nint retSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(nint list);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessW(
        string? app, string cmdLine, nint procAttr, nint threadAttr,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint flags, nint env, string cwd,
        ref STARTUPINFOEXW si, out PROCESS_INFORMATION pi);

    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(nint h, uint ms);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(nint h, out uint code);
}
