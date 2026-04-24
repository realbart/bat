// PtyServer: runs cmd.exe in a ConPTY and serves it over a Unix domain socket.
// Start PtyServer first, then connect with PtyClient.

using System.Net.Sockets;
using System.Runtime.InteropServices;

#pragma warning disable CA1416

#if WINDOWS
// REPRODUCE batd issue: Started with CreateNoWindow, then AllocConsole + ShowWindow(SW_HIDE)
// This simulates how bat starts batd

// Check if we already have a console (running from VS debugger)
bool hasConsole = false;
try
{
    _ = Console.WindowWidth;
    hasConsole = true;
}
catch { }

if (!hasConsole)
{
    // No console yet - allocate one and hide it (like batd does)
    AllocConsole();

    // Read console size BEFORE hiding the window
    int cols = 120;
    int rows = 30;
    try
    {
        cols = Console.WindowWidth is > 0 and < 500 ? Console.WindowWidth : 120;
        rows = Console.WindowHeight is > 0 and < 200 ? Console.WindowHeight : 30;
    }
    catch { }

    var hwnd = GetConsoleWindow();
    if (hwnd != 0)
        ShowWindow(hwnd, 0); // SW_HIDE - this breaks ConPTY!
}
else
{
    // Already have a console (VS debugger) - use normal size detection
    int cols = Console.WindowWidth is > 0 and < 500 ? Console.WindowWidth : 120;
    int rows = Console.WindowHeight is > 0 and < 200 ? Console.WindowHeight : 30;
}
#else
int cols = 120;
int rows = 30;
#endif

var socketPath = Path.Combine(Path.GetTempPath(), "pty-demo.sock");

if (File.Exists(socketPath))
    File.Delete(socketPath);

// Use the server's console window size as the initial PTY size

Console.Error.WriteLine($"[PtyServer] Listening on {socketPath}  ({cols}x{rows})");

using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
listener.Bind(new UnixDomainSocketEndPoint(socketPath));
listener.Listen(4);

Console.Error.WriteLine("[PtyServer] Waiting for client...");
using var clientSocket = await listener.AcceptAsync();
Console.Error.WriteLine("[PtyServer] Client connected.");
using var stream = new NetworkStream(clientSocket, ownsSocket: false);

// Start cmd.exe inside a ConPTY
using var pty = new ConPty();
pty.Start(@"C:\Program Files\PowerShell\7\pwsh.exe", "", Environment.CurrentDirectory, cols, rows);
Console.Error.WriteLine($"[PtyServer] cmd.exe started (PID {pty.ProcessId})");

using var cts = new CancellationTokenSource();

// PTY output → socket
var pipeOut = Task.Run(async () =>
{
    var buf = new byte[4096];
    try
    {
        while (!cts.Token.IsCancellationRequested)
        {
            var n = await pty.ReadAsync(buf, cts.Token);
            if (n <= 0) break;
            await stream.WriteAsync(buf.AsMemory(0, n), cts.Token);
            await stream.FlushAsync(cts.Token);
        }
    }
    catch (OperationCanceledException) { }
    catch (Exception ex) { Console.Error.WriteLine($"[PtyServer] Output pipe error: {ex.Message}"); }
});

// Socket input → PTY
var pipeIn = Task.Run(async () =>
{
    var buf = new byte[256];
    try
    {
        while (!cts.Token.IsCancellationRequested)
        {
            var n = await stream.ReadAsync(buf, cts.Token);
            if (n <= 0) break;
            await pty.WriteAsync(buf.AsMemory(0, n), cts.Token);
        }
    }
    catch (OperationCanceledException) { }
    catch (Exception ex) { Console.Error.WriteLine($"[PtyServer] Input pipe error: {ex.Message}"); }
});

// Wait for cmd.exe to exit
var exitCode = await pty.WaitForExitAsync();
Console.Error.WriteLine($"[PtyServer] cmd.exe exited (code {exitCode})");

pty.ClosePseudoConsoleHandle();

// Drain remaining PTY output (give it up to 500 ms)
await Task.WhenAny(pipeOut, Task.Delay(500));
await cts.CancelAsync();

try { await Task.WhenAll(pipeOut, pipeIn).WaitAsync(TimeSpan.FromSeconds(2)); }
catch { /* ignore timeout / cancellation */ }

try { File.Delete(socketPath); } catch { }
Console.Error.WriteLine("[PtyServer] Done.");
return exitCode;
