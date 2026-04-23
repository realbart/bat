using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Ipc;

namespace Bat;

internal static class Program
{
    private static string BannerText =>
        $"🦇Bat [Version {typeof(Program).Assembly.GetName().Version}]\r\n(c) Bart Kemps. Released under GPLv3+.\r\n\r\n";

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
        var outputTask = ReadOutputLoopAsync(stream, cts.Token);

        if (interactive)
        {
            _ = WriteKeyLoopAsync(stream, cts.Token);
            var exitCode = await outputTask;
            await cts.CancelAsync();
            return exitCode;
        }
        else
        {
            _ = ForwardStdinAsync(stream, cts.Token);
            var exitCode = await outputTask;
            await cts.CancelAsync();
            return exitCode;
        }
    }

    private static async Task<int> ReadOutputLoopAsync(Stream stream, CancellationToken ct)
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
                        break;
                    case TerminalMessageType.Err:
                        await Console.Error.WriteAsync(Encoding.UTF8.GetString(msg.Value.Payload));
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

    private static async Task WriteKeyLoopAsync(Stream stream, CancellationToken ct)
    {
        try
        {
#if WINDOWS
            var hIn = GetStdHandle(STD_INPUT_HANDLE);
            GetConsoleMode(hIn, out var savedMode);
            // Disable echo and line input ONCE to avoid per-key mode flipping
            // that conflicts with Console.Out.Write on the output thread.
            SetConsoleMode(hIn, (savedMode & ~(ENABLE_ECHO_INPUT | ENABLE_LINE_INPUT)) | ENABLE_WINDOW_INPUT);
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var key = await Task.Run(() => ReadKeyFromConsole(hIn), ct);
                    if (key.HasValue)
                        await TerminalProtocol.WriteKeyAsync(stream, key.Value, ct);
                }
            }
            finally
            {
                SetConsoleMode(hIn, savedMode);
            }
#else
            while (!ct.IsCancellationRequested)
            {
                var key = Console.ReadKey(true);
                await TerminalProtocol.WriteKeyAsync(stream, key, ct);
            }
#endif
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

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

    /// <summary>
    /// Reads a key event from the console input handle without changing console mode.
    /// Returns null for non-key events (like resize — those are handled separately).
    /// </summary>
    private static ConsoleKeyInfo? ReadKeyFromConsole(nint hIn)
    {
        while (true)
        {
            if (!ReadConsoleInputW(hIn, out var rec, 1, out _))
                return null;

            if (rec.EventType == KEY_EVENT && rec.KeyEvent.bKeyDown != 0)
            {
                var state = rec.KeyEvent.dwControlKeyState;
                return new ConsoleKeyInfo(
                    rec.KeyEvent.UnicodeChar,
                    (ConsoleKey)rec.KeyEvent.wVirtualKeyCode,
                    (state & SHIFT_PRESSED) != 0,
                    (state & (LEFT_ALT_PRESSED | RIGHT_ALT_PRESSED)) != 0,
                    (state & (LEFT_CTRL_PRESSED | RIGHT_CTRL_PRESSED)) != 0);
            }
            // Skip key-up events, mouse events, etc.
        }
    }

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
            while (!ct.IsCancellationRequested)
            {
                var ch = Console.In.Read();
                if (ch < 0) break;
                await TerminalProtocol.WriteKeyAsync(stream, new ConsoleKeyInfo((char)ch, 0, false, false, false), ct);
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
            Process.Start(new ProcessStartInfo(daemonPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
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
