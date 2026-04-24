using System.Diagnostics;
using System.Reflection;
using System.Text;
using Bat.Context.Dos;
using Bat.Execution;

namespace Bat.UnitTests;

#if WINDOWS
/// <summary>
/// Integration tests that run every .bat file in the Examples folder through both
/// cmd.exe (external process) and Bat (in-process via BatchExecutor), then assert
/// their stdout and stderr match.
///
/// Usage: add a .bat file to UnitTests\Examples\ and it is picked up automatically.
/// A failing test means Bat's output diverges from CMD — use it as a TDD signal.
/// </summary>
[TestClass]
public class ExampleScriptTests
{
    private static readonly string ExamplesDir =
        Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Examples");

    public static IEnumerable<object[]> GetScripts()
    {
        if (!Directory.Exists(ExamplesDir)) yield break;
        foreach (var file in Directory.GetFiles(ExamplesDir, "*.bat", SearchOption.AllDirectories))
            yield return [Path.GetFileName(file), file];
    }

    [DataTestMethod]
    [DynamicData(nameof(GetScripts), DynamicDataSourceType.Method)]
    [Timeout(10000)]
    public async Task Script_BatOutputMatchesCmd(string name, string scriptPath)
    {
        var (cmdOut, cmdErr) = await RunWithCmdAsync(scriptPath);
        var (batOut, batErr) = await RunWithBatAsync(scriptPath);

        Assert.AreEqual(cmdOut, batOut, $"stdout mismatch in {name}");
        Assert.AreEqual(cmdErr, batErr, $"stderr mismatch in {name}");
    }

    private static async Task<(string Out, string Err)> RunWithCmdAsync(string scriptPath)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var oemEncoding = Encoding.GetEncoding(
            System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage);

        var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), $"/C \"{scriptPath}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            StandardOutputEncoding = oemEncoding,
            StandardErrorEncoding = oemEncoding,
            WorkingDirectory = Path.GetDirectoryName(scriptPath)!
        };

        using var proc = Process.Start(psi)!;
        proc.StandardInput.Close();
        var outTask = proc.StandardOutput.ReadToEndAsync();
        var errTask = proc.StandardError.ReadToEndAsync();

        var exitTask = proc.WaitForExitAsync();
        if (await Task.WhenAny(exitTask, Task.Delay(8000)) != exitTask)
        {
            try { proc.Kill(true); } catch { }
            throw new TimeoutException($"cmd.exe timed out for {scriptPath}");
        }

        return (Normalize(await outTask), Normalize(await errTask));
    }

    private static async Task<(string Out, string Err)> RunWithBatAsync(string scriptPath)
    {
        var scriptDir = Path.GetDirectoryName(scriptPath)!;
        var scriptDrive = char.ToUpperInvariant(scriptDir[0]);
        var driveRoot = $"{scriptDrive}:\\";

        var fs = new DosFileSystem(new Dictionary<char, string> { [scriptDrive] = driveRoot });
        var console = new TestConsole();
        var ctx = new TestCommandContext(fs) { Console = console };
        ctx.SetCurrentDrive(scriptDrive);

        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            ctx.EnvironmentVariables[entry.Key.ToString()!] = entry.Value?.ToString() ?? "";

        // Add the test output directory to PATH so satellite executables (doskey.exe, tree.com) are found
        var testOutputDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var existingPath = ctx.EnvironmentVariables.TryGetValue("PATH", out var p) ? p : "";
        ctx.EnvironmentVariables["PATH"] = testOutputDir + ";" + existingPath;

        var segments = scriptDir[3..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
        ctx.SetPath(scriptDrive, segments);

        var bc = new BatchContext { Context = ctx };
        var executor = new BatchExecutor();
        await executor.ExecuteAsync(scriptPath, "", bc, []);

        return (Normalize(console.OutText), Normalize(console.ErrText));
    }

    private static string Normalize(string s)
    {
        var lines = s.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n').Split('\n');
        return string.Join("\n", lines.Select(l => l.TrimEnd())).ToLowerInvariant();
    }
}
#endif
