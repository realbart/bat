// PtyClient: connects to PtyServer over a Unix domain socket and provides
// an interactive terminal session with the remote cmd.exe process.
//
// Usage:
//   1. Start PtyServer in one window.
//   2. Start PtyClient in another window – type commands, press Enter.
//   3. Type "exit" (or close PtyServer) to disconnect.

using System.Net.Sockets;
using System.Runtime.InteropServices;

#pragma warning disable CA1416 // Windows-only

var socketPath = Path.Combine(Path.GetTempPath(), "pty-demo.sock");
Console.Error.WriteLine($"[PtyClient] Connecting to {socketPath} ...");

using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
try
{
    await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath));
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[PtyClient] Connection failed: {ex.Message}");
    Console.Error.WriteLine("[PtyClient] Is PtyServer running?");
    return 1;
}

Console.Error.WriteLine("[PtyClient] Connected. (cmd.exe is running on the server)");

// ── P/Invoke constants ───────────────────────────────────────────────────────
const int STD_INPUT_HANDLE  = -10;
const int STD_OUTPUT_HANDLE = -11;

// ── Put console into raw / VT mode ───────────────────────────────────────────
var hIn  = GetStdHandle(STD_INPUT_HANDLE);
var hOut = GetStdHandle(STD_OUTPUT_HANDLE);
GetConsoleMode(hIn,  out var origInMode);
GetConsoleMode(hOut, out var origOutMode);

// Input: raw (no echo, no line buffering) + virtual terminal sequences for arrows etc.
const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200;
const uint ENABLE_LINE_INPUT             = 0x0002;
const uint ENABLE_ECHO_INPUT             = 0x0004;
const uint ENABLE_PROCESSED_INPUT        = 0x0001;   // disabling this sends Ctrl+C as 0x03

// Output: enable ANSI/VT escape processing so colors and cursor moves render.
const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

SetConsoleMode(hIn,
    (origInMode & ~(ENABLE_LINE_INPUT | ENABLE_ECHO_INPUT | ENABLE_PROCESSED_INPUT))
    | ENABLE_VIRTUAL_TERMINAL_INPUT);

SetConsoleMode(hOut, origOutMode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);

using var stream = new NetworkStream(socket, ownsSocket: false);
using var cts    = new CancellationTokenSource();

var stdinStream  = Console.OpenStandardInput();
var stdoutStream = Console.OpenStandardOutput();

// stdin → socket  (raw bytes, including Ctrl+C as 0x03, arrow keys as ESC sequences, …)
var pipeIn = Task.Run(async () =>
{
    var buf = new byte[256];
    try
    {
        while (!cts.Token.IsCancellationRequested)
        {
            var n = await stdinStream.ReadAsync(buf, cts.Token);
            if (n <= 0) break;
            await stream.WriteAsync(buf.AsMemory(0, n), cts.Token);
            await stream.FlushAsync(cts.Token);
        }
    }
    catch (OperationCanceledException) { }
    catch { /* connection closed */ }
});

// socket → stdout  (PTY output bytes: ANSI sequences, text, …)
var pipeOut = Task.Run(async () =>
{
    var buf = new byte[4096];
    try
    {
        while (!cts.Token.IsCancellationRequested)
        {
            var n = await stream.ReadAsync(buf, cts.Token);
            if (n <= 0) break;
            await stdoutStream.WriteAsync(buf.AsMemory(0, n), cts.Token);
            await stdoutStream.FlushAsync(cts.Token);
        }
    }
    catch (OperationCanceledException) { }
    catch { /* connection closed */ }
    // When the server closes the socket (cmd.exe exited) we unblock pipeIn too.
    await cts.CancelAsync();
});

await Task.WhenAny(pipeIn, pipeOut);
await cts.CancelAsync();

// ── Restore console ───────────────────────────────────────────────────────────
SetConsoleMode(hIn,  origInMode);
SetConsoleMode(hOut, origOutMode);

Console.Error.WriteLine("\r\n[PtyClient] Disconnected.");
return 0;

// ── P/Invoke ─────────────────────────────────────────────────────────────────

[DllImport("kernel32.dll")] static extern nint GetStdHandle(int nStdHandle);
[DllImport("kernel32.dll")] static extern bool GetConsoleMode(nint hConsole, out uint lpMode);
[DllImport("kernel32.dll")] static extern bool SetConsoleMode(nint hConsole, uint dwMode);
