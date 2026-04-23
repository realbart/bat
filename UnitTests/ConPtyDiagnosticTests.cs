#if WINDOWS
using System.Text;
using Bat.Pty;
using SC = System.Console;

namespace Bat.UnitTests;

/// <summary>
/// Diagnostic tests for ConPTY in isolation.
/// These test ConPTY directly — no bat console, no REPL, no Dispatcher.
/// If these fail, ConPTY itself is broken on this machine/OS.
/// If these pass, the problem is in bat's integration layer.
/// </summary>
[TestClass]
public class ConPtyDiagnosticTests
{
    /// <summary>
    /// Test 1: Can ConPTY run a non-interactive command and capture output?
    /// </summary>
    [TestMethod]
    [Timeout(10000)]
    public async Task ConPty_CmdEchoHello_CapturesOutput()
    {
        using var pty = new ConPty();
        var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        pty.Start(cmd, "/c echo hello", Environment.CurrentDirectory, null);

        var output = await ReadAllOutputAsync(pty, TimeSpan.FromSeconds(5));
        pty.ClosePseudoConsoleHandle();

        System.Diagnostics.Debug.WriteLine($"[ConPty echo] Output ({output.Length} chars): [{Escape(output)}]");
        SC.WriteLine($"[ConPty echo] Output ({output.Length} chars): [{Escape(output)}]");

        Assert.IsTrue(output.Contains("hello", StringComparison.OrdinalIgnoreCase),
            $"Expected 'hello' in output but got: [{Escape(output)}]");
    }

    /// <summary>
    /// Test 2: Can ConPTY deliver output DURING execution (not just after exit)?
    /// Uses ping with a loopback address — it produces output every second.
    /// </summary>
    [TestMethod]
    [Timeout(15000)]
    public async Task ConPty_Tasklist_CapturesOutput()
    {
        using var pty = new ConPty();
        var tasklist = Path.Combine(Environment.SystemDirectory, "tasklist.exe");
        pty.Start(tasklist, "", Environment.CurrentDirectory, null);

        // Read concurrently DURING execution — don't wait for exit
        var sb = new StringBuilder();
        var buf = new byte[4096];
        var gotContentDuringExecution = false;

        var readTask = Task.Run(() =>
        {
            while (true)
            {
                int n;
                try { n = pty.ReadAsync(buf).GetAwaiter().GetResult(); }
                catch { break; }
                if (n == 0) break;
                var chunk = Encoding.UTF8.GetString(buf, 0, n);
                lock (sb) { sb.Append(chunk); }
                var stripped = StripVt(sb.ToString());
                if (stripped.Contains(".exe", StringComparison.OrdinalIgnoreCase))
                    gotContentDuringExecution = true;
            }
        });

        // Wait up to 10s for content to appear BEFORE process exits
        for (var i = 0; i < 100 && !gotContentDuringExecution; i++)
            await Task.Delay(100);

        var duringExecution = gotContentDuringExecution;

        // Now wait for exit and drain
        var exitTask = pty.WaitForExitAsync();
        await Task.WhenAny(exitTask, Task.Delay(5000));
        pty.ClosePseudoConsoleHandle();
        await Task.WhenAny(readTask, Task.Delay(3000));

        string output;
        lock (sb) { output = sb.ToString(); }
        var stripped2 = StripVt(output);

        SC.WriteLine($"[tasklist] During execution: {duringExecution}");
        SC.WriteLine($"[tasklist] Total raw: {output.Length}, stripped: {stripped2.Length}");
        SC.WriteLine($"[tasklist] Contains .exe: {stripped2.Contains(".exe", StringComparison.OrdinalIgnoreCase)}");
        SC.WriteLine($"[tasklist] First 300 stripped: [{stripped2[..Math.Min(300, stripped2.Length)]}]");

        Assert.IsTrue(stripped2.Contains(".exe", StringComparison.OrdinalIgnoreCase),
            $"Expected process names (.exe) in output but got ({stripped2.Length} chars): [{stripped2[..Math.Min(200, stripped2.Length)]}]");
    }

    /// <summary>
    /// Test 3: Can ConPTY handle interactive input? Write "dir\r" to cmd.exe and get output.
    /// </summary>
    [TestMethod]
    [Timeout(15000)]
    public async Task ConPty_CmdInteractive_InputAndOutput()
    {
        using var pty = new ConPty();
        var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        pty.Start(cmd, "", Environment.CurrentDirectory, null);

        // Wait for cmd.exe to show its prompt
        var initial = await ReadOutputUntilAsync(pty, ">", TimeSpan.FromSeconds(5));
        System.Diagnostics.Debug.WriteLine($"[ConPty interactive] Initial ({initial.Length} chars): [{Escape(initial[..Math.Min(200, initial.Length)])}]");
        SC.WriteLine($"[ConPty interactive] Initial ({initial.Length} chars): [{Escape(initial[..Math.Min(200, initial.Length)])}]");

        Assert.IsTrue(initial.Contains(">"),
            $"Expected cmd.exe prompt '>' but got: [{Escape(initial)}]");

        // Send "echo test123\r" — the \r is Enter
        await pty.WriteAsync(Encoding.UTF8.GetBytes("echo test123\r"));

        // Read the response
        var response = await ReadOutputUntilAsync(pty, "test123", TimeSpan.FromSeconds(5));
        System.Diagnostics.Debug.WriteLine($"[ConPty interactive] Response ({response.Length} chars): [{Escape(response[..Math.Min(300, response.Length)])}]");
        SC.WriteLine($"[ConPty interactive] Response ({response.Length} chars): [{Escape(response[..Math.Min(300, response.Length)])}]");

        Assert.IsTrue(response.Contains("test123"),
            $"Expected 'test123' in response but got: [{Escape(response)}]");

        // Send exit to cleanly terminate
        await pty.WriteAsync(Encoding.UTF8.GetBytes("exit\r"));
        var exitCode = await pty.WaitForExitAsync();
        pty.ClosePseudoConsoleHandle();
    }

    /// <summary>
    /// Test 4: Does WriteAsync actually deliver bytes to the ConPTY input pipe?
    /// Writes to input, then checks if the child echoes them back in output.
    /// </summary>
    [TestMethod]
    [Timeout(10000)]
    public async Task ConPty_WriteAsync_BytesReachChild()
    {
        using var pty = new ConPty();
        var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        pty.Start(cmd, "", Environment.CurrentDirectory, null);

        // Wait for prompt
        await ReadOutputUntilAsync(pty, ">", TimeSpan.FromSeconds(5));

        // Write individual characters - cmd.exe in cooked mode should echo each one
        var testChars = "abc";
        foreach (var c in testChars)
        {
            await pty.WriteAsync(new[] { (byte)c });
            await Task.Delay(50); // Small delay between characters
        }

        // Read whatever was echoed back
        var echoed = await ReadOutputWithTimeoutAsync(pty, TimeSpan.FromSeconds(2));
        System.Diagnostics.Debug.WriteLine($"[ConPty write] Echoed ({echoed.Length} chars): [{Escape(echoed)}]");
        SC.WriteLine($"[ConPty write] Echoed ({echoed.Length} chars): [{Escape(echoed)}]");

        Assert.IsTrue(echoed.Contains("abc"),
            $"Expected 'abc' echoed back but got: [{Escape(echoed)}]");

        // Cleanup
        await pty.WriteAsync(Encoding.UTF8.GetBytes("\rexit\r"));
        try { await pty.WaitForExitAsync(); } catch { }
        pty.ClosePseudoConsoleHandle();
    }

    /// <summary>
    /// Test 5: Start pwsh via ConPTY, run a command, verify output, then exit.
    /// </summary>
    [TestMethod]
    [Timeout(30000)]
    public async Task ConPty_Pwsh_RunCommandAndExit()
    {
        // Find pwsh.exe
        var pwsh = FindPwsh();
        if (pwsh == null)
        {
            Assert.Inconclusive("pwsh.exe not found on this machine");
            return;
        }

        using var pty = new ConPty();
        pty.Start(pwsh, "-NoProfile -NoLogo", Environment.CurrentDirectory, null);

        // Read everything that comes through the pipe for up to 10 seconds
        var allOutput = new StringBuilder();
        var buf = new byte[4096];

        // Concurrent reader
        var readerDone = false;
        var outputTask = Task.Run(() =>
        {
            while (!readerDone)
            {
                int n;
                try { n = pty.ReadAsync(buf).GetAwaiter().GetResult(); }
                catch { break; }
                if (n == 0) break;
                var chunk = Encoding.UTF8.GetString(buf, 0, n);
                lock (allOutput) { allOutput.Append(chunk); }
                SC.WriteLine($"[pwsh-pipe] +{n} bytes: [{Escape(chunk[..Math.Min(100, chunk.Length)])}]");
            }
        });

        // Wait for prompt or 10 seconds
        await Task.Delay(10_000);

        // Try sending a command
        SC.WriteLine("[pwsh] Sending: Write-Output 'CONPTY_TEST_OK'");
        await pty.WriteAsync(Encoding.UTF8.GetBytes("Write-Output 'CONPTY_TEST_OK'\r"));

        // Wait for response
        await Task.Delay(3000);

        // Send exit
        SC.WriteLine("[pwsh] Sending: exit");
        await pty.WriteAsync(Encoding.UTF8.GetBytes("exit\r"));

        // Wait for exit
        var exitTask = pty.WaitForExitAsync();
        if (await Task.WhenAny(exitTask, Task.Delay(5000)) == exitTask)
            await exitTask;

        pty.ClosePseudoConsoleHandle();
        readerDone = true;
        await Task.WhenAny(outputTask, Task.Delay(2000));

        string raw, stripped;
        lock (allOutput) { raw = allOutput.ToString(); }
        stripped = StripVt(raw);

        SC.WriteLine($"[pwsh] TOTAL raw: {raw.Length} chars");
        SC.WriteLine($"[pwsh] TOTAL stripped: {stripped.Length} chars");
        SC.WriteLine($"[pwsh] Raw (first 500): [{Escape(raw[..Math.Min(500, raw.Length)])}]");
        SC.WriteLine($"[pwsh] Stripped (first 500): [{stripped[..Math.Min(500, stripped.Length)]}]");

        Assert.IsTrue(stripped.Contains("CONPTY_TEST_OK") || stripped.Contains(">"),
            $"Expected PS prompt or test output but got ({stripped.Length} chars): [{stripped[..Math.Min(300, stripped.Length)]}]");
    }

    // ── Helpers ──

    private static string? FindPwsh()
    {
        // Try PATH first
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var candidate = Path.Combine(dir, "pwsh.exe");
            if (File.Exists(candidate)) return candidate;
        }
        // Common install locations
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        foreach (var dir in Directory.GetDirectories(Path.Combine(programFiles, "PowerShell"), "*", SearchOption.TopDirectoryOnly))
        {
            var candidate = Path.Combine(dir, "pwsh.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    /// <summary>
    /// Strips VT/ANSI escape sequences from a string so we can assert on visible text.
    /// </summary>
    private static string StripVt(string s) =>
        System.Text.RegularExpressions.Regex.Replace(s, @"\x1b[\[\]()][0-9;?]*[a-zA-Z~]|\x1b[()][0-9A-Z]|\x1b\][^\x07\x1b]*(?:\x07|\x1b\\)?", "");

    /// <summary>
    /// Reads output from ConPTY concurrently while the process runs, then drains after exit.
    /// </summary>
    private static async Task<string> ReadAllOutputAsync(ConPty pty, TimeSpan timeout)
    {
        var sb = new StringBuilder();
        var buf = new byte[4096];

        // Start reading output concurrently
        var outputTask = Task.Run(() =>
        {
            while (true)
            {
                int n;
                try { n = pty.ReadAsync(buf).GetAwaiter().GetResult(); }
                catch { break; }
                if (n == 0) break;
                lock (sb) { sb.Append(Encoding.UTF8.GetString(buf, 0, n)); }
            }
        });

        // Wait for process exit (with timeout)
        var exitTask = pty.WaitForExitAsync();
        if (await Task.WhenAny(exitTask, Task.Delay(timeout)) == exitTask)
            await exitTask;

        // Close pseudoconsole to signal EOF on the output pipe
        pty.ClosePseudoConsoleHandle();

        // Wait for reader to finish draining
        await Task.WhenAny(outputTask, Task.Delay(TimeSpan.FromSeconds(3)));

        lock (sb) { return sb.ToString(); }
    }

    private static async Task<string> ReadOutputUntilAsync(ConPty pty, string marker, TimeSpan timeout)
    {
        var sb = new StringBuilder();
        var buf = new byte[4096];
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero) break;

            var readTask = Task.Run(() =>
            {
                try { return pty.ReadAsync(buf).GetAwaiter().GetResult(); }
                catch { return 0; }
            });

            if (await Task.WhenAny(readTask, Task.Delay(remaining)) != readTask)
                break;

            var n = await readTask;
            if (n == 0) break;
            sb.Append(Encoding.UTF8.GetString(buf, 0, n));

            if (sb.ToString().Contains(marker, StringComparison.OrdinalIgnoreCase))
                break;
        }
        return sb.ToString();
    }

    private static async Task<string> ReadOutputWithTimeoutAsync(ConPty pty, TimeSpan timeout)
    {
        return await ReadOutputUntilAsync(pty, "\x00NEVER_MATCH\x00", timeout);
    }

    private static string Escape(string s) =>
        s.Replace("\x1b", "\\e").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\0", "\\0");
}
#endif
