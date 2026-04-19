namespace Bat.UnitTests;

#if WINDOWS
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
        var scriptDir = Path.GetDirectoryName(scriptPath)!;
        var batDir = Path.GetDirectoryName(BatExe)!;

        // Map the script's drive letter so bat can find the script.
        // Prefix bat's output dir to PATH so satellite commands (tree, subst) are found.
        var scriptDrive = char.ToUpperInvariant(scriptDir[0]);
        var driveRoot = $"{scriptDrive}:\\";

        var (cmdOut, cmdErr) = await RunAsync("cmd.exe", $"/C \"{scriptPath}\"", scriptDir);
        var (batOut, batErr) = await RunAsync(BatExe,
            $"/N /M:{scriptDrive}={driveRoot} /C \"{scriptPath}\"",
            scriptDir, extraPath: batDir);

        var nativeDrive = char.ToLowerInvariant(scriptDrive);
        Assert.AreEqual(cmdOut, batOut, $"stdout mismatch in {name}");
        Assert.AreEqual(cmdErr, batErr, $"stderr mismatch in {name}");
    }

    private static async Task<(string Out, string Err)> RunAsync(
        string exe, string args, string workingDir, string? extraPath = null)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var oemEncoding = Encoding.GetEncoding(
            System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage);

        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            StandardOutputEncoding = oemEncoding,
            StandardErrorEncoding = oemEncoding,
            WorkingDirectory = workingDir
        };

        if (extraPath != null)
            psi.Environment["PATH"] = extraPath + ";" + Environment.GetEnvironmentVariable("PATH");

        using var proc = Process.Start(psi)!;
        proc.StandardInput.Close();
        var outTask = proc.StandardOutput.ReadToEndAsync();
        var errTask = proc.StandardError.ReadToEndAsync();
        
        // Add a 10s timeout to avoid hangs in script tests
        var exitTask = proc.WaitForExitAsync();
        if (await Task.WhenAny(exitTask, Task.Delay(10000)) != exitTask)
        {
            try { proc.Kill(true); } catch { }
            throw new TimeoutException($"Process {exe} {args} timed out after 10s in {workingDir}");
        }
        
        return (Normalize(outTask.Result), Normalize(errTask.Result));
    }

    private static string Normalize(string s)
    {
        var lines = s.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n').Split('\n');
        return string.Join("\n", lines.Select(l => l.TrimEnd())).ToLowerInvariant();
    }
}
#endif
