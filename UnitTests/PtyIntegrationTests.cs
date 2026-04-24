#if WINDOWS
using System.Text;
using Bat.Pty;
using Bat.Daemon;
using Ipc;

namespace Bat.UnitTests;

/// <summary>
/// Tests that trace the exact bat → batd → ConPTY → pwsh chain.
/// Each test isolates a layer to find where it breaks.
/// </summary>
[TestClass]
public class PtyIntegrationTests
{
    /// <summary>
    /// Layer 1: ConPTY alone (already known to work from ConPtyDiagnosticTests).
    /// Sanity check — if this fails, ConPTY itself is broken.
    /// </summary>
    [TestMethod]
    [Timeout(15000)]
    public async Task Layer1_ConPty_Alone_Works()
    {
        using var pty = new ConPty();
        var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        pty.Start(cmd, "/c echo LAYER1_OK", Environment.CurrentDirectory, null, 80, 24);

        var exitCode = await pty.WaitForExitAsync();
        await Task.Delay(500);
        pty.ClosePseudoConsoleHandle();

        var buf = new byte[4096];
        var sb = new StringBuilder();
        while (true)
        {
            var n = await pty.ReadAsync(buf);
            if (n == 0) break;
            sb.Append(Encoding.UTF8.GetString(buf, 0, n));
        }

        var output = sb.ToString();
        System.Console.WriteLine($"[Layer1] Output ({output.Length} chars): {output[..Math.Min(200, output.Length)]}");
        Assert.AreEqual(0, exitCode);
    }

    /// <summary>
    /// Layer 2: ConPTY + SocketConsole output path.
    /// Does PTY output reach the SocketConsole's Out writer?
    /// Uses a real SocketConsole with a MemoryStream to capture messages.
    /// </summary>
    [TestMethod]
    [Timeout(15000)]
    public async Task Layer2_ConPty_Through_SocketConsole_Output()
    {
        // Create a pipe to simulate the socket
        var (serverStream, clientStream) = CreateStreamPair();

        using var console = new SocketConsole(serverStream, 80, 24, true);

        // Start ConPTY with a simple command
        using var pty = new ConPty();
        var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        pty.Start(cmd, "/c echo LAYER2_OK", Environment.CurrentDirectory, null, 80, 24);

        // Pipe PTY output → SocketConsole.Out (same as PtyNativeExecutor does)
        var outputDone = new TaskCompletionSource();
        _ = Task.Run(async () =>
        {
            var buf = new byte[4096];
            while (true)
            {
                var n = await pty.ReadAsync(buf);
                if (n == 0) break;
                await console.Out.WriteAsync(Encoding.UTF8.GetString(buf, 0, n));
            }
            outputDone.SetResult();
        });

        // Read messages from the client side of the pipe
        var received = new StringBuilder();
        var readTask = Task.Run(async () =>
        {
            while (true)
            {
                var msg = await TerminalProtocol.ReadAsync(clientStream);
                if (msg == null) break;
                if (msg.Value.Type == TerminalMessageType.Out)
                    received.Append(Encoding.UTF8.GetString(msg.Value.Payload));
            }
        });

        await pty.WaitForExitAsync();
        await Task.Delay(500);
        pty.ClosePseudoConsoleHandle();
        await Task.WhenAny(outputDone.Task, Task.Delay(3000));

        // Close server side to unblock client reader
        serverStream.Close();
        await Task.WhenAny(readTask, Task.Delay(2000));

        var output = received.ToString();
        System.Console.WriteLine($"[Layer2] Received ({output.Length} chars): {output[..Math.Min(300, output.Length)]}");
        Assert.IsTrue(output.Length > 0, "Expected SocketConsole to forward PTY output but got nothing");
    }

    /// <summary>
    /// Layer 3: EnterRawModeAsync + ReadRawAsync.
    /// Does the SocketConsole correctly send RawModeOn and receive RawInput?
    /// </summary>
    [TestMethod]
    [Timeout(10000)]
    public async Task Layer3_RawMode_Signaling()
    {
        var (serverStream, clientStream) = CreateStreamPair();
        using var console = new SocketConsole(serverStream, 80, 24, true);

        // Server sends EnterRawMode
        await console.EnterRawModeAsync();

        // Client should receive RawModeOn message
        var msg = await TerminalProtocol.ReadAsync(clientStream);
        Assert.IsNotNull(msg);
        Assert.AreEqual(TerminalMessageType.RawModeOn, msg.Value.Type);

        // Client sends RawInput
        var testData = "hello\r"u8.ToArray();
        await TerminalProtocol.WriteAsync(clientStream, TerminalMessageType.RawInput, testData);

        // Server should receive raw bytes via ReadRawAsync
        var buf = new byte[256];
        var n = await console.ReadRawAsync(buf);
        Assert.AreEqual(testData.Length, n);
        Assert.AreEqual("hello\r", Encoding.UTF8.GetString(buf, 0, n));

        // Server sends LeaveRawMode
        await console.LeaveRawModeAsync();

        var msg2 = await TerminalProtocol.ReadAsync(clientStream);
        Assert.IsNotNull(msg2);
        Assert.AreEqual(TerminalMessageType.RawModeOff, msg2.Value.Type);
    }

    /// <summary>
    /// Layer 4: Full round-trip — ConPTY + SocketConsole + RawInput.
    /// Start cmd.exe in ConPTY, send "echo LAYER4_OK\r" via RawInput, read output.
    /// This is the exact flow that PtyNativeExecutor uses.
    /// </summary>
    [TestMethod]
    [Timeout(15000)]
    public async Task Layer4_FullRoundTrip_ConPty_SocketConsole_RawInput()
    {
        var (serverStream, clientStream) = CreateStreamPair();
        using var console = new SocketConsole(serverStream, 80, 24, true);

        // Start ConPTY
        using var pty = new ConPty();
        var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        pty.Start(cmd, "", Environment.CurrentDirectory, null, 80, 24);

        // Enter raw mode
        await console.EnterRawModeAsync();
        var modeMsg = await TerminalProtocol.ReadAsync(clientStream);
        Assert.AreEqual(TerminalMessageType.RawModeOn, modeMsg!.Value.Type);

        using var cts = new CancellationTokenSource(12000);

        // Output loop: PTY → SocketConsole.Out → client stream
        var received = new StringBuilder();
        var outputTask = Task.Run(async () =>
        {
            var buf = new byte[4096];
            while (!cts.Token.IsCancellationRequested)
            {
                var n = await pty.ReadAsync(buf, cts.Token);
                if (n == 0) break;
                await console.Out.WriteAsync(Encoding.UTF8.GetString(buf, 0, n));
            }
        });

        var clientReadTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var msg = await TerminalProtocol.ReadAsync(clientStream, cts.Token);
                if (msg == null) break;
                if (msg.Value.Type == TerminalMessageType.Out)
                {
                    var text = Encoding.UTF8.GetString(msg.Value.Payload);
                    received.Append(text);
                    System.Console.Write(text);
                }
            }
        });

        // Wait for cmd.exe prompt
        await Task.Delay(2000);
        System.Console.WriteLine($"\n[Layer4] After 2s, received {received.Length} chars");

        // Send input via RawInput: "echo LAYER4_OK\r"
        var input = "echo LAYER4_OK\r"u8.ToArray();
        await TerminalProtocol.WriteAsync(clientStream, TerminalMessageType.RawInput, input);

        // Input loop: client RawInput → SocketConsole.ReadRawAsync → PTY
        var inputTask = Task.Run(async () =>
        {
            var buf = new byte[256];
            while (!cts.Token.IsCancellationRequested)
            {
                var n = await console.ReadRawAsync(buf, cts.Token);
                if (n <= 0) break;
                await pty.WriteAsync(buf.AsMemory(0, n), cts.Token);
            }
        });

        // Wait for response
        await Task.Delay(3000);

        // Send exit
        var exitInput = "exit\r"u8.ToArray();
        await TerminalProtocol.WriteAsync(clientStream, TerminalMessageType.RawInput, exitInput);

        var exitCode = await pty.WaitForExitAsync();
        pty.ClosePseudoConsoleHandle();
        await cts.CancelAsync();

        var output = received.ToString();
        System.Console.WriteLine($"\n[Layer4] Total output ({output.Length} chars)");
        System.Console.WriteLine($"[Layer4] Contains LAYER4_OK: {output.Contains("LAYER4_OK")}");

        Assert.IsTrue(output.Length > 0, "Expected output from ConPTY through SocketConsole");
    }

    private static (Stream Server, Stream Client) CreateStreamPair()
    {
        // Use a pair of connected sockets as the stream pair
        var listener = new System.Net.Sockets.Socket(
            System.Net.Sockets.AddressFamily.Unix,
            System.Net.Sockets.SocketType.Stream,
            System.Net.Sockets.ProtocolType.Unspecified);
        var path = Path.Combine(Path.GetTempPath(), $"bat-test-{Guid.NewGuid()}.sock");
        try
        {
            listener.Bind(new System.Net.Sockets.UnixDomainSocketEndPoint(path));
            listener.Listen(1);

            var client = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.Unix,
                System.Net.Sockets.SocketType.Stream,
                System.Net.Sockets.ProtocolType.Unspecified);
            client.Connect(new System.Net.Sockets.UnixDomainSocketEndPoint(path));
            var server = listener.Accept();

            return (new System.Net.Sockets.NetworkStream(server, true),
                    new System.Net.Sockets.NetworkStream(client, true));
        }
        finally
        {
            listener.Dispose();
            try { File.Delete(path); } catch { }
        }
    }
}
#endif
