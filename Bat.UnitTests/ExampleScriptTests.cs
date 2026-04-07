using System.Diagnostics;
using System.Reflection;
using System.Text;
using Bat.Console;
using Bat.Context;
using Bat.Execution;

namespace Bat.UnitTests;

/// <summary>
/// Integration tests that run every .bat file in the Examples folder through both
/// cmd.exe and Bat, then assert their stdout and stderr match.
///
/// Usage: add a .bat file to Bat.UnitTests\Examples\ and it is picked up automatically.
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
    public async Task Script_BatOutputMatchesCmd(string name, string scriptPath)
    {
        var (cmdOut, cmdErr) = RunWithCmd(scriptPath);
        var (batOut, batErr) = await RunWithBatAsync(scriptPath);

        Assert.AreEqual(cmdOut, batOut, $"stdout mismatch in {name}");
        Assert.AreEqual(cmdErr, batErr, $"stderr mismatch in {name}");
    }

    private static (string Out, string Err) RunWithCmd(string scriptPath)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var oemEncoding = Encoding.GetEncoding(
            System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage);

        var psi = new ProcessStartInfo("cmd.exe", $"/C \"{scriptPath}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = oemEncoding,
            StandardErrorEncoding = oemEncoding,
            WorkingDirectory = Path.GetDirectoryName(scriptPath)!
        };
        using var proc = Process.Start(psi)!;
        var outTask = proc.StandardOutput.ReadToEndAsync();
        var errTask = proc.StandardError.ReadToEndAsync();
        proc.WaitForExit();
        return (Normalize(outTask.Result), Normalize(errTask.Result));
    }

    private static async Task<(string Out, string Err)> RunWithBatAsync(string scriptPath)
    {
        // Mirror "bat /m:C=C:\" — map virtual drive C to native C:\
        var fs = new DosFileSystem(new Dictionary<char, string> { ['C'] = @"C:\" });
        var ctx = new DosContext(fs);
        ctx.SetCurrentDrive('C');

        // Set working directory to the script's parent directory
        var scriptDir = Path.GetDirectoryName(scriptPath)!;
        var segments = scriptDir.Split('\\', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1) // drop "C:" drive prefix
            .ToArray();
        ctx.SetPath('C', segments);

        var console = new TestConsole();
        var bc = new BatchContext { Console = console, Context = ctx };
        var executor = new BatchExecutor(console);
        await executor.ExecuteAsync(scriptPath, "", bc, []);

        return (Normalize(console.OutText), Normalize(console.ErrText));
    }

    private static string Normalize(string s)
    {
        var lines = s.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n').Split('\n');
        return string.Join("\n", lines.Select(l => l.TrimEnd())).ToLowerInvariant();
    }
}
