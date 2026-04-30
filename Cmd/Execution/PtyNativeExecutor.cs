using System.Diagnostics;
using Bat.Context;
using Bat.Nodes;
using Bat.Pty;
using BatD.Files;
using Context;

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
        var workingDir = await context.FileSystem.GetNativePathAsync(new BatPath(context.CurrentDrive, context.CurrentPath));
        var hostExecutablePath = await PathTranslator.TranslateBatPathToHost(executablePath, context.FileSystem);
        var hasRedirections = redirections.Count > 0;

        var usePty = !hasRedirections && waitForExit && context.Console.IsInteractive && !context.Console.IsNative;

        if (usePty)
        {
            try
            {
                return await ExecuteWithPtyAsync(hostExecutablePath, arguments, workingDir.Path, context);
            }
            catch (Exception ex)
            {
                // ConPTY failed — fall back to regular process and report
                await context.Console.Error.WriteLineAsync($"[pty] {ex.Message} — falling back");
                return await ExecuteWithProcessAsync(hostExecutablePath, arguments, workingDir.Path, context, false);
            }
        }

        return await ExecuteWithProcessAsync(hostExecutablePath, arguments, workingDir.Path, context, hasRedirections);
    }

    private static async Task<int> ExecuteWithPtyAsync(string executable, string arguments, string workingDir, global::Context.IContext context)
    {
        using var pty = CreatePty();

        var hostEnv = await PathTranslator.TranslateBatEnvironmentToHost(
            (IReadOnlyDictionary<string, string>)context.EnvironmentVariables,
            context.FileSystem);

        pty.Start(executable, arguments, workingDir, hostEnv, context.Console.WindowWidth, context.Console.WindowHeight);

        // Signal the terminal client to switch to raw byte mode
        await context.Console.EnterRawModeAsync();

        using var cts = new CancellationTokenSource();

        // Output: PTY → console (raw bytes, NO string conversion to preserve ANSI sequences)
        var outputTask = Task.Run(async () =>
        {
            var buf = new byte[4096];
            while (true)
            {
                var n = await pty.ReadAsync(buf, cts.Token);
                if (n == 0) break;

                // Write raw bytes directly to console (for SocketConsole this goes to the socket)
                await context.Console.Out.WriteAsync(System.Text.Encoding.UTF8.GetString(buf, 0, n));
                await context.Console.Out.FlushAsync();  // ← ADD EXPLICIT FLUSH!
            }
        });

        // Input: console raw bytes → PTY (direct pipe, no KeyToBytes conversion)
        _ = Task.Run(async () =>
        {
            var buf = new byte[256];
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var n = await context.Console.ReadRawAsync(buf, cts.Token);
                    if (n <= 0) break;
                    await pty.WriteAsync(buf.AsMemory(0, n), cts.Token);
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
            var exitCode = await pty.WaitForExitAsync();
            pty.ClosePseudoConsoleHandle();
            await cts.CancelAsync();
            try { await outputTask; } catch { }
            context.ErrorCode = exitCode;
            return exitCode;
        }
        finally
        {
            context.Console.Resized -= onResize;
            await context.Console.LeaveRawModeAsync();
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

    private async Task<int> ExecuteWithProcessAsync(string executable, string arguments, string workingDir, global::Context.IContext context, bool hasRedirections)
    {
        var useShell = isGuiApp && !hasRedirections;
        var redirectStreams = !useShell && !(context.Console.IsNative && !hasRedirections);

        var psi = new ProcessStartInfo(executable, arguments)
        {
            WorkingDirectory = workingDir,
            UseShellExecute = useShell,
            RedirectStandardOutput = redirectStreams,
            RedirectStandardError = redirectStreams,
            RedirectStandardInput = redirectStreams
        };
        if (!psi.UseShellExecute)
        {
            foreach (var (key, value) in await PathTranslator.TranslateBatEnvironmentToHost(
                (IReadOnlyDictionary<string, string>)context.EnvironmentVariables, context.FileSystem))
                psi.Environment[key] = value;
        }
        var process = Process.Start(psi);
        if (process == null) return 1;
        if (!waitForExit) return 0;

        if (redirectStreams)
        {
            // Forward stdout and stderr to the context console
            var outTask = ForwardStreamAsync(process.StandardOutput, context.Console.Out);
            var errTask = ForwardStreamAsync(process.StandardError, context.Console.Error);

            // Forward console input to process stdin
            using var cts = new CancellationTokenSource();
            _ = ForwardInputAsync(context.Console, process.StandardInput, cts.Token);

            await Task.WhenAll(outTask, errTask);
            await process.WaitForExitAsync();
            await cts.CancelAsync();
        }
        else
        {
            await process.WaitForExitAsync();
        }

        context.ErrorCode = process.ExitCode;
        return process.ExitCode;
    }

    private static async Task ForwardInputAsync(global::Context.IConsole console, StreamWriter stdin, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var key = await console.ReadKeyAsync(true, ct);
                if (key.KeyChar != '\0')
                    await stdin.WriteAsync(key.KeyChar);
                if (key.Key == ConsoleKey.Enter)
                    await stdin.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    private static async Task ForwardStreamAsync(StreamReader source, TextWriter dest)
    {
        var buffer = new char[4096];
        int read;
        while ((read = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
            await dest.WriteAsync(buffer, 0, read);
    }
}


