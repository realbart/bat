using System.Diagnostics;
using Bat.Context;
using Bat.Nodes;

namespace Bat.Execution;

/// <summary>
/// Executes native executables (.exe) via Process.Start.
/// Always redirects stdout/stderr and forwards through IConsole,
/// because batd runs without a console window.
/// </summary>
internal class NativeExecutor(bool waitForExit = true, bool isGuiApp = false) : IExecutor
{
    public async Task<int> ExecuteAsync(string executablePath, string arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        var context = batchContext.Context;
        var workingDir = context.FileSystem.GetNativePath(context.CurrentDrive, context.CurrentPath);
        var hostExecutablePath = PathTranslator.TranslateBatPathToHost(executablePath, context.FileSystem);

        var psi = new ProcessStartInfo(hostExecutablePath, arguments)
        {
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };

        foreach (var (key, value) in PathTranslator.TranslateBatEnvironmentToHost(
            (IReadOnlyDictionary<string, string>)context.EnvironmentVariables, context.FileSystem))
            psi.Environment[key] = value;

        var process = Process.Start(psi);
        if (process == null) return 1;
        if (!waitForExit) return 0;

        // Forward stdout and stderr to the IConsole
        var console = batchContext.Console;
        var outTask = ForwardStreamAsync(process.StandardOutput, console.Out);
        var errTask = ForwardStreamAsync(process.StandardError, console.Error);

        // Forward console input to process stdin
        using var cts = new CancellationTokenSource();
        _ = ForwardInputAsync(console, process.StandardInput, cts.Token);

        await Task.WhenAll(outTask, errTask);
        await process.WaitForExitAsync();
        await cts.CancelAsync();

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
