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
        // Enable UTF-8 and ANSI escape processing
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

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
            while (!ct.IsCancellationRequested)
            {
                var key = Console.ReadKey(true);
                await TerminalProtocol.WriteKeyAsync(stream, key, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

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
