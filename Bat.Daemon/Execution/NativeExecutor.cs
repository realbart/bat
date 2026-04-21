using System.Diagnostics;
using Bat.Context;
using Bat.Nodes;

namespace Bat.Execution;

/// <summary>
/// Executes native executables (.exe) via Process.Start.
/// GUI apps start in background, console apps wait for completion.
/// Redirections are not yet implemented (Step 7).
/// </summary>
internal class NativeExecutor(bool waitForExit = true, bool isGuiApp = false) : IExecutor
{
    public async Task<int> ExecuteAsync(string executablePath, string arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        var context = batchContext.Context;
        var workingDir = context.FileSystem.GetNativePath(context.CurrentDrive, context.CurrentPath);
        var hostExecutablePath = PathTranslator.TranslateBatPathToHost(executablePath, context.FileSystem);
        var hasRedirections = redirections.Count > 0;

        var psi = new ProcessStartInfo(hostExecutablePath, arguments)
        {
            WorkingDirectory = workingDir,
            UseShellExecute = isGuiApp && !hasRedirections,
            RedirectStandardOutput = hasRedirections,
            RedirectStandardError = hasRedirections
        };

        if (!psi.UseShellExecute)
        {
            // TODO: Cache TranslateBatEnvironmentToHost result per environment snapshot for performance
            foreach (var (key, value) in PathTranslator.TranslateBatEnvironmentToHost((IReadOnlyDictionary<string, string>)context.EnvironmentVariables, context.FileSystem))
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
