using System.Diagnostics;
using System.Reflection;
using System.Text;

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

    private static readonly string BatExe =
        Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "bat.exe");

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
        var (cmdOut, cmdErr) = await RunAsync("cmd.exe", $"/C \"{scriptPath}\"", scriptPath);
        var (batOut, batErr) = await RunAsync(BatExe, $"/N /C \"{scriptPath}\"", scriptPath);

        Assert.AreEqual(cmdOut, batOut, $"stdout mismatch in {name}");
        Assert.AreEqual(cmdErr, batErr, $"stderr mismatch in {name}");
    }

    private static async Task<(string Out, string Err)> RunAsync(string exe, string args, string scriptPath)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var oemEncoding = Encoding.GetEncoding(
            System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage);

        var psi = new ProcessStartInfo(exe, args)
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
        await proc.WaitForExitAsync();
        return (Normalize(outTask.Result), Normalize(errTask.Result));
    }

    private static string Normalize(string s)
    {
        var lines = s.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n').Split('\n');
        return string.Join("\n", lines.Select(l => l.TrimEnd())).ToLowerInvariant();
    }
}
