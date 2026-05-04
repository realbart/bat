using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Ipc;

namespace Bat;

internal static class Program
{
#if WINDOWS
    private const string DaemonExe = "batd.exe";
#else
    private const string DaemonExe = "batd";
#endif

    static async Task<int> Main(string[] args)
    {
        // Enable UTF-8 and ANSI/VT escape processing
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
#if WINDOWS
        EnableVirtualTerminalProcessing();
#endif

        // bat only handles /? and /N locally. Everything else goes to batd verbatim.
        var showHelp = false;
        var suppressBanner = false;
        var forwardArgs = new List<string>();

        foreach (var arg in args)
        {
            var upper = arg.ToUpperInvariant();
            if (upper is "/?" or "-H" or "--HELP")
            {
                showHelp = true;
            }
            else if (upper is "/N" or "-N" or "--NOLOGO")
            {
                suppressBanner = true;
            }
            else
            {
                forwardArgs.Add(arg);
            }
        }

        if (showHelp)
        {
            await PrintHelpAsync();
            return 0;
        }

        if (!suppressBanner)
        {
            // Pass /N suppression state to cmd.exe so it can show/hide its own banner
        }

        // Connect to daemon (or start it)
        var daemonPath = Path.Combine(AppContext.BaseDirectory, DaemonExe);
        var socket = await ConnectOrStartDaemonAsync(daemonPath);
        if (socket == null)
        {
            await Console.Error.WriteLineAsync("Failed to connect to daemon.");
            return 1;
        }

        await using var stream = new NetworkStream(socket, ownsSocket: true);

        // Send Init with the raw command line (everything except /N and /?)
        var cmdLine = string.Join(" ", forwardArgs);
        var width = 80;
        var height = 25;
        var interactive = true;
        try { width = Console.WindowWidth; height = Console.WindowHeight; } catch { }
        try { interactive = !Console.IsInputRedirected; } catch { }

        await TerminalProtocol.WriteInitAsync(stream, cmdLine, width, height, interactive, default);

        // Proxy loop: read keys → send to daemon, receive output → write to console
        using var cts = new CancellationTokenSource();
        var inputMode = new InputModeSwitch(stream, interactive, cts.Token);
        var outputTask = ReadOutputLoopAsync(stream, inputMode, cts.Token);
        inputMode.Start();

        var exitCode = await outputTask;
        await cts.CancelAsync();
        inputMode.Dispose();
        // Force-exit: on Linux, Console.ReadKey blocks forever and ignores cancellation,
        // so a normal return would leave bat hanging after batd disconnects.
        Environment.Exit(exitCode);
        return exitCode;
    }

    /// <summary>
    /// Manages switching between structured Key input and raw byte input.
    /// Uses a SINGLE input loop to avoid race conditions between concurrent console readers.
    /// When batd sends RawModeOn, switches console to VT raw mode within the same loop.
    /// </summary>
    private sealed class InputModeSwitch : IDisposable
    {
        private readonly Stream _stream;
        private readonly bool _interactive;
        private readonly CancellationToken _ct;
        private CancellationTokenSource? _loopCts;
        private volatile bool _rawMode;

        public InputModeSwitch(Stream stream, bool interactive, CancellationToken ct)
        {
            _stream = stream;
            _interactive = interactive;
            _ct = ct;
        }

        public void Start()
        {
            _loopCts = CancellationTokenSource.CreateLinkedTokenSource(_ct);
            if (_interactive)
            {
#if WINDOWS
                _ = WindowsInputLoopAsync(_stream, _loopCts.Token);
#else
                _ = UnixInputLoopAsync(_stream, _loopCts.Token);
#endif
            }
            else
            {
                _ = ForwardStdinAsync(_stream, _loopCts.Token);
            }
        }

        public void EnterRawMode() => _rawMode = true;
        public void LeaveRawMode() => _rawMode = false;

#if WINDOWS
        /// <summary>
        /// Single input loop for Windows. Polls console input with short sleeps.
        /// In key mode: reads ConsoleKeyInfo via ReadConsoleInputW, sends Key messages.
        /// In raw mode: switches console to VT input, reads stdin as raw bytes, sends RawInput.
        /// Modeled after PtyClient's proven approach.
        /// </summary>
        private async Task WindowsInputLoopAsync(Stream stream, CancellationToken ct)
        {
            var hIn = GetStdHandle(STD_INPUT_HANDLE);
            GetConsoleMode(hIn, out var originalMode);

            // Key mode: no echo, no line input, but structured key events
            var keyMode = (originalMode & ~(ENABLE_ECHO_INPUT | ENABLE_LINE_INPUT)) | ENABLE_WINDOW_INPUT;

            // Raw mode: VT input sequences as raw bytes on stdin (exactly like PtyClient)
            const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200;
            const uint ENABLE_PROCESSED_INPUT = 0x0001;
            var rawModeFlags = (originalMode & ~(ENABLE_ECHO_INPUT | ENABLE_LINE_INPUT | ENABLE_PROCESSED_INPUT))
                               | ENABLE_VIRTUAL_TERMINAL_INPUT;

            SetConsoleMode(hIn, keyMode);
            var currentlyRaw = false;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // Mode switch check
                    if (_rawMode && !currentlyRaw)
                    {
                        FlushConsoleInputBuffer(hIn);
                        SetConsoleMode(hIn, rawModeFlags);
                        currentlyRaw = true;
                    }
                    else if (!_rawMode && currentlyRaw)
                    {
                        FlushConsoleInputBuffer(hIn);
                        SetConsoleMode(hIn, keyMode);
                        currentlyRaw = false;
                    }

                    if (currentlyRaw)
                    {
                        // Raw mode: read stdin bytes (like PtyClient), one chunk at a time
                        // then return to top of loop to check mode flag
                        using var stdinStream = Console.OpenStandardInput();
                        var buf = new byte[256];
                        while (_rawMode && !ct.IsCancellationRequested)
                        {
                            var n = await stdinStream.ReadAsync(buf, ct);
                            if (n <= 0) break;
                            await TerminalProtocol.WriteAsync(stream, TerminalMessageType.RawInput, buf.AsMemory(0, n), ct);
                        }
                    }
                    else
                    {
                        // Key mode: poll for input events without blocking
                        if (!GetNumberOfConsoleInputEvents(hIn, out var count) || count == 0)
                        {
                            await Task.Delay(10, ct);
                            continue;
                        }

                        if (!ReadConsoleInputW(hIn, out var rec, 1, out _))
                            continue;

                        if (rec.EventType == KEY_EVENT && rec.KeyEvent.bKeyDown != 0)
                        {
                            var state = rec.KeyEvent.dwControlKeyState;
                            var key = new ConsoleKeyInfo(
                                rec.KeyEvent.UnicodeChar,
                                (ConsoleKey)rec.KeyEvent.wVirtualKeyCode,
                                (state & SHIFT_PRESSED) != 0,
                                (state & (LEFT_ALT_PRESSED | RIGHT_ALT_PRESSED)) != 0,
                                (state & (LEFT_CTRL_PRESSED | RIGHT_CTRL_PRESSED)) != 0);
                            await TerminalProtocol.WriteKeyAsync(stream, key, ct);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            finally
            {
                SetConsoleMode(hIn, originalMode);
            }
        }
#endif

#if !WINDOWS
        private async Task UnixInputLoopAsync(Stream stream, CancellationToken ct)
        {
            var currentlyRaw = false;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // Mode switch
                    if (_rawMode && !currentlyRaw)
                    {
                        UnixTerminal.EnterRawMode();
                        currentlyRaw = true;
                    }
                    else if (!_rawMode && currentlyRaw)
                    {
                        UnixTerminal.LeaveRawMode();
                        currentlyRaw = false;
                    }

                    if (currentlyRaw)
                    {
                        var stdinStream = Console.OpenStandardInput();
                        var buf = new byte[256];
                        while (_rawMode && !ct.IsCancellationRequested)
                        {
                            var n = await stdinStream.ReadAsync(buf, ct);
                            if (n <= 0) break;
                            await TerminalProtocol.WriteAsync(stream, TerminalMessageType.RawInput, buf.AsMemory(0, n), ct);
                        }
                    }
                    else
                    {
                        var key = Console.ReadKey(true);
                        await TerminalProtocol.WriteKeyAsync(stream, key, ct);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            finally
            {
                if (currentlyRaw)
                    UnixTerminal.LeaveRawMode();
            }
        }
#endif

        public void Dispose()
        {
            _loopCts?.Cancel();
            _loopCts?.Dispose();
        }
    }

    private static async Task<int> ReadOutputLoopAsync(Stream stream, InputModeSwitch inputMode, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var msg = await TerminalProtocol.ReadAsync(stream, ct);
                if (msg == null) break;

                switch (msg.Value.Type)
                {
                    case TerminalMessageType.Out:
                        await Console.Out.WriteAsync(Encoding.UTF8.GetString(msg.Value.Payload));
                        await Console.Out.FlushAsync();
                        break;
                    case TerminalMessageType.Err:
                        await Console.Error.WriteAsync(Encoding.UTF8.GetString(msg.Value.Payload));
                        await Console.Error.FlushAsync();
                        break;
                    case TerminalMessageType.RawModeOn:
                        inputMode.EnterRawMode();
                        break;
                    case TerminalMessageType.RawModeOff:
                        inputMode.LeaveRawMode();
                        break;
                    case TerminalMessageType.Exit:
                        return TerminalProtocol.ParseExitCode(msg.Value.Payload);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }

        await Console.Error.WriteLineAsync("batd: connection lost");
        return 1;
    }

    // WriteKeyLoopAsync removed — input is now handled by InputModeSwitch.WindowsInputLoopAsync

#if WINDOWS
    private const int STD_INPUT_HANDLE = -10;
    private const uint ENABLE_ECHO_INPUT = 0x0004;
    private const uint ENABLE_LINE_INPUT = 0x0002;
    private const uint ENABLE_WINDOW_INPUT = 0x0008;

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern nint GetStdHandle(int nStdHandle);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(nint h, out uint mode);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetConsoleMode(nint h, uint mode);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool ReadConsoleInputW(nint hConsoleInput, out INPUT_RECORD lpBuffer, uint nLength, out uint lpNumberOfEventsRead);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool FlushConsoleInputBuffer(nint hConsoleInput);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetNumberOfConsoleInputEvents(nint hConsoleInput, out uint lpcNumberOfEvents);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
    private struct INPUT_RECORD
    {
        [System.Runtime.InteropServices.FieldOffset(0)] public ushort EventType;
        [System.Runtime.InteropServices.FieldOffset(4)] public KEY_EVENT_RECORD KeyEvent;
        [System.Runtime.InteropServices.FieldOffset(4)] public WINDOW_BUFFER_SIZE_RECORD WindowBufferSizeEvent;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct KEY_EVENT_RECORD
    {
        public int bKeyDown;
        public ushort wRepeatCount;
        public ushort wVirtualKeyCode;
        public ushort wVirtualScanCode;
        public char UnicodeChar;
        public uint dwControlKeyState;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct WINDOW_BUFFER_SIZE_RECORD
    {
        public short X, Y;
    }

    private const ushort KEY_EVENT = 0x0001;
    private const ushort WINDOW_BUFFER_SIZE_EVENT = 0x0004;
    private const uint SHIFT_PRESSED = 0x0010;
    private const uint LEFT_ALT_PRESSED = 0x0002;
    private const uint RIGHT_ALT_PRESSED = 0x0001;
    private const uint LEFT_CTRL_PRESSED = 0x0008;
    private const uint RIGHT_CTRL_PRESSED = 0x0004;

    private static void EnableVirtualTerminalProcessing()
    {
        const int STD_OUTPUT_HANDLE = -11;
        const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        var hOut = GetStdHandle(STD_OUTPUT_HANDLE);
        if (GetConsoleMode(hOut, out var mode))
            SetConsoleMode(hOut, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
    }
#endif

    private static async Task ForwardStdinAsync(Stream stream, CancellationToken ct)
    {
        try
        {
            var stdinStream = Console.OpenStandardInput();
            var buf = new byte[256];
            while (!ct.IsCancellationRequested)
            {
                var n = await stdinStream.ReadAsync(buf, ct);
                if (n <= 0) break;
                // Non-interactive: always send as raw input (no structured key info available)
                await TerminalProtocol.WriteAsync(stream, TerminalMessageType.RawInput, buf.AsMemory(0, n), ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    private static async Task<Socket?> ConnectOrStartDaemonAsync(string daemonPath)
    {
        var socketPath = TerminalProtocol.GetSocketPath();

        var socket = await TryConnectAsync(socketPath);
        if (socket != null) return socket;

        if (!File.Exists(daemonPath))
        {
            await Console.Error.WriteLineAsync($"Daemon not found: {daemonPath}");
            return null;
        }

        try
        {
            // batd is a WinExe (no console), so we don't redirect streams.
            // Redirecting streams can interfere with ConPTY child processes.
            Process.Start(new ProcessStartInfo(daemonPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch { return null; }

        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(100);
            socket = await TryConnectAsync(socketPath);
            if (socket != null) return socket;
        }

        return null;
    }

    private static async Task<Socket?> TryConnectAsync(string socketPath, int timeoutMs = 500)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cts.Token);
            return socket;
        }
        catch
        {
            socket.Dispose();
            return null;
        }
    }

    private static async Task PrintHelpAsync()
    {
#if WINDOWS
        await Console.Out.WriteAsync("""
            Starts BAT command interpreter with virtual drive mappings.

            BAT [/? | /N] [/Q] [/M:X=path,...] [[/C | /K] string | filename]

              /?              Display this help message.
              /N              Suppress startup banner.
              /Q              Turns echo off.
              /C string       Carries out the command specified by string and then terminates.
              /K string       Carries out the command specified by string but remains.
              /M:X=path,...   Map virtual drive X: to native path.
              /D              Disable execution of AutoRun commands (ignored).
              filename        Execute batch file then terminate.

            """);
#else
        await Console.Out.WriteAsync("""
            Starts bat command interpreter with virtual drive mappings.

            bat [-h | --help | -n | --nologo] [-q] [-m X=path,...] [[-c | -k] string | filename]

              -h, --help      Display this help message.
              -n, --nologo    Suppress startup banner.
              -q              Turns echo off.
              -c string       Carries out the command specified by string and then terminates.
              -k string       Carries out the command specified by string but remains.
              -m X=path,...   Map virtual drive X: to native path.
              -d              Disable execution of AutoRun commands (ignored).
              filename        Execute batch file then terminate.

            """);
#endif
    }
}
