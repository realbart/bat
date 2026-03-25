using System.Diagnostics;
using Bat.Context;
using Bat.Nodes;

namespace Bat.Execution;

/// <summary>
/// Executes native executables (.exe) via Process.Start.
/// GUI apps start in background, console apps wait for completion.
/// Redirections are not yet implemented (Step 7).
/// </summary>
internal class NativeExecutor : IExecutor
{
    private readonly bool _waitForExit;
    private readonly bool _isGuiApp;

    public NativeExecutor(bool waitForExit = true, bool isGuiApp = false)
    {
        _waitForExit = waitForExit;
        _isGuiApp = isGuiApp;
    }

    public async Task<int> ExecuteAsync(string executablePath, string arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        var context = batchContext.Context;
        var workingDir = context.FileSystem.GetNativePath(context.CurrentDrive, context.CurrentPath);

        var hostExecutablePath = PathTranslator.TranslateBatPathToHost(executablePath, context.FileSystem);

        var hasRedirections = redirections.Count > 0;
        var psi = new ProcessStartInfo(hostExecutablePath, arguments)
        {
            WorkingDirectory = workingDir,
            UseShellExecute = _isGuiApp && !hasRedirections,
            RedirectStandardOutput = hasRedirections,
            RedirectStandardError = hasRedirections
        };

        var process = Process.Start(psi);
        if (process == null)
            return 1;

        if (_waitForExit)
        {
            await process.WaitForExitAsync();
            var exitCode = process.ExitCode;
            context.ErrorCode = exitCode;
            return exitCode;
        }

        return 0;
    }
}
