using System.Diagnostics;
using Bat.Context;
using Bat.Nodes;
using Bat.Pty;

namespace Bat.Execution;

/// <summary>
/// Executes native executables with PTY support.
/// Uses PTY when running interactively without redirections.
/// </summary>
internal class PtyNativeExecutor(bool waitForExit = true, bool isGuiApp = false) : IExecutor
{
    public async Task<int> ExecuteAsync(string executablePath, string arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        var context = batchContext.Context;
        var workingDir = context.FileSystem.GetNativePath(context.CurrentDrive, context.CurrentPath);
        var hostExecutablePath = PathTranslator.TranslateBatPathToHost(executablePath, context.FileSystem);
        var hasRedirections = redirections.Count > 0;

        var usePty = !isGuiApp && !hasRedirections && waitForExit && context.Console.IsInteractive;

        if (usePty)
        {
            try
            {
                return await ExecuteWithPtyAsync(hostExecutablePath, arguments, workingDir, context);
            }
            catch (Exception ex)
            {
                // ConPTY failed — fall back to regular process and report
                await context.Console.Error.WriteLineAsync($"[pty] {ex.Message} — falling back");
                return await ExecuteWithProcessAsync(hostExecutablePath, arguments, workingDir, context, false);
            }
        }

        return await ExecuteWithProcessAsync(hostExecutablePath, arguments, workingDir, context, hasRedirections);
    }

    private static async Task<int> ExecuteWithPtyAsync(string executable, string arguments, string workingDir, global::Context.IContext context)
    {
        using var pty = CreatePty();

        pty.Start(executable, arguments, workingDir, environment: null);

        using var cts = new CancellationTokenSource();

        // Output: PTY → console (must drain fully before returning)
        var outputTask = Task.Run(async () =>
        {
            var buf = new byte[4096];
            while (true)
            {
                var n = await pty.ReadAsync(buf, cts.Token);
                if (n == 0) break;
                await context.Console.Out.WriteAsync(
                    System.Text.Encoding.UTF8.GetString(buf, 0, n));
            }
        });

        // Input: console → PTY (fire-and-forget, cancelled when process exits)
        _ = Task.Run(async () =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var key = await context.Console.ReadKeyAsync(true, cts.Token);
                    var bytes = KeyToBytes(key);
                    if (bytes.Length > 0)
                        await pty.WriteAsync(bytes, cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
        });

        // Resize forwarding
        void onResize(int w, int h) => pty.Resize(w, h);
        context.Console.Resized += onResize;

        try
        {
            // Wait for process exit, then drain remaining output
            var exitCode = await pty.WaitForExitAsync();
            try { await outputTask; } catch { }
            context.ErrorCode = exitCode;
            return exitCode;
        }
        finally
        {
            context.Console.Resized -= onResize;
            await cts.CancelAsync();
        }
    }

    private static IPseudoTerminal CreatePty()
    {
#if WINDOWS
        return new ConPty();
#else
        return new PosixPty();
#endif
    }

    private static byte[] KeyToBytes(ConsoleKeyInfo key) => key.Key switch
    {
        ConsoleKey.Enter => "\r"u8.ToArray(),
        ConsoleKey.Backspace => "\x7f"u8.ToArray(),
        ConsoleKey.Tab => "\t"u8.ToArray(),
        ConsoleKey.Escape => "\x1b"u8.ToArray(),
        ConsoleKey.UpArrow => "\x1b[A"u8.ToArray(),
        ConsoleKey.DownArrow => "\x1b[B"u8.ToArray(),
        ConsoleKey.RightArrow => "\x1b[C"u8.ToArray(),
        ConsoleKey.LeftArrow => "\x1b[D"u8.ToArray(),
        ConsoleKey.Home => "\x1b[H"u8.ToArray(),
        ConsoleKey.End => "\x1b[F"u8.ToArray(),
        ConsoleKey.Delete => "\x1b[3~"u8.ToArray(),
        ConsoleKey.PageUp => "\x1b[5~"u8.ToArray(),
        ConsoleKey.PageDown => "\x1b[6~"u8.ToArray(),
        _ when key.KeyChar != '\0' => System.Text.Encoding.UTF8.GetBytes([key.KeyChar]),
        _ => []
    };

    private async Task<int> ExecuteWithProcessAsync(string executable, string arguments, string workingDir, global::Context.IContext context, bool hasRedirections)
    {
        var psi = new ProcessStartInfo(executable, arguments)
        {
            WorkingDirectory = workingDir,
            UseShellExecute = isGuiApp && !hasRedirections,
            RedirectStandardOutput = hasRedirections,
            RedirectStandardError = hasRedirections
        };
        if (!psi.UseShellExecute)
        {
            foreach (var (key, value) in PathTranslator.TranslateBatEnvironmentToHost(
                (IReadOnlyDictionary<string, string>)context.EnvironmentVariables, context.FileSystem))
                psi.Environment[key] = value;
        }
        var process = Process.Start(psi);
        if (process == null) return 1;
        if (!waitForExit) return 0;
        await process.WaitForExitAsync();
        context.ErrorCode = process.ExitCode;
        return process.ExitCode;
    }
}
