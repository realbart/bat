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
        using var pty = CreatePty(context);

        var hostEnv = await PathTranslator.TranslateBatEnvironmentToHost(
            (IReadOnlyDictionary<string, string>)context.EnvironmentVariables,
            context.FileSystem);
        PathTranslator.StripHostDirectoryFromPath(hostEnv, Path.Combine(AppContext.BaseDirectory, "bin"), context.FileSystem);

        pty.Start(executable, arguments, workingDir, hostEnv, context.Console.WindowWidth, context.Console.WindowHeight);

        // Signal the terminal client to switch to raw byte mode
        await context.Console.EnterRawModeAsync();

        using var inputCts = new CancellationTokenSource();
        using var drainCts = new CancellationTokenSource();

        // Output: PTY → console (raw bytes, NO string conversion to preserve ANSI sequences)
        var outputTask = Task.Run(async () =>
        {
            var buf = new byte[4096];
            while (true)
            {
                var n = await pty.ReadAsync(buf, drainCts.Token);
                if (n == 0) break;

                // Write raw bytes directly to console (for SocketConsole this goes to the socket)
                await context.Console.Out.WriteAsync(System.Text.Encoding.UTF8.GetString(buf, 0, n));
                await context.Console.Out.FlushAsync();
            }
        });

        // Input: console raw bytes → PTY (direct pipe, no KeyToBytes conversion)
        _ = Task.Run(async () =>
        {
            var buf = new byte[256];
            try
            {
                while (!inputCts.Token.IsCancellationRequested)
                {
                    var n = await context.Console.ReadRawAsync(buf, inputCts.Token);
                    if (n <= 0) break;
                    await pty.WriteAsync(buf.AsMemory(0, n), inputCts.Token);
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
            await inputCts.CancelAsync();                          // stop forwarding input
            pty.ClosePseudoConsoleHandle();                        // Windows: triggers EOF on read pipe; Linux: closes master fd → EIO
            drainCts.CancelAfter(TimeSpan.FromMilliseconds(500)); // safety timeout in case EOF never arrives
            try { await outputTask; } catch { }                    // drain remaining output
            context.ErrorCode = exitCode;
            return exitCode;
        }
        finally
        {
            context.Console.Resized -= onResize;
            await context.Console.LeaveRawModeAsync();
        }
    }

    private static global::Context.IPseudoTerminal CreatePty(global::Context.IContext context)
    {
        return context.CreatePty();
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
            var hostEnv = await PathTranslator.TranslateBatEnvironmentToHost(
                (IReadOnlyDictionary<string, string>)context.EnvironmentVariables, context.FileSystem);
            PathTranslator.StripHostDirectoryFromPath(hostEnv, Path.Combine(AppContext.BaseDirectory, "bin"), context.FileSystem);
            foreach (var (key, value) in hostEnv)
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

            // Forward stdin: when not interactive (piped input), copy the TextReader directly
            // so the process receives the full pipe content and stdin closes on EOF.
            // When interactive, forward keystrokes one at a time.
            using var cts = new CancellationTokenSource();
            if (!context.Console.IsInteractive)
                _ = ForwardTextReaderAsync(context.Console.In, process.StandardInput, cts.Token);
            else
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

    private static async Task ForwardTextReaderAsync(TextReader reader, StreamWriter stdin, CancellationToken ct)
    {
        try
        {
            var buffer = new char[4096];
            int read;
            while (!ct.IsCancellationRequested &&
                   (read = await reader.ReadAsync(buffer.AsMemory(), ct)) > 0)
            {
                await stdin.WriteAsync(buffer.AsMemory(0, read), ct);
                await stdin.FlushAsync(ct);
            }
            stdin.Close();
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


