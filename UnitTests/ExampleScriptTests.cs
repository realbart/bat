using System.Diagnostics;
using System.Reflection;
using System.Text;
using Bat.Execution;
using BatD.Files;

namespace Bat.UnitTests;

/// <summary>
/// Integration tests for batch scripts in the Examples folder.
///
/// Two test methods:
///
/// 1. <b>Script_BatMatchesCmd</b> (Windows only) — oracle test.
///    Runs each script through both cmd.exe and Bat against the <b>real</b> filesystem
///    and asserts their output matches. This is the source of truth.
///
/// 2. <b>Script_BatMatchesGolden</b> (all platforms) — regression test.
///    Runs each script through Bat against a <b>virtual</b> filesystem and compares
///    against a golden file (.txt / .err.txt). Golden files are generated on Windows
///    by running Bat after a successful oracle test.
///
/// Usage: drop a .bat file in UnitTests\Examples\ and both tests pick it up automatically.
/// </summary>
[TestClass]
public class ExampleScriptTests
{
    private static readonly string ExamplesDir =
        Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Examples");

    /// <summary>Path to the source Examples folder, used for writing golden files back to the repo.</summary>
    private static readonly string? SourceExamplesDir = FindSourceExamplesDir();

    public static IEnumerable<object[]> GetScripts()
    {
        if (!Directory.Exists(ExamplesDir)) yield break;
        foreach (var file in Directory.GetFiles(ExamplesDir, "*.bat", SearchOption.AllDirectories))
            yield return [Path.GetFileName(file), file];
    }

    /// <summary>Scripts whose output depends on real filesystem state and cannot produce stable golden files.</summary>
    private static readonly HashSet<string> NoGoldenScripts = new(StringComparer.OrdinalIgnoreCase) { };

    // ── Oracle test: bat vs cmd.exe, both against the real filesystem (Windows only) ──

#if WINDOWS
    [DataTestMethod]
    [DynamicData(nameof(GetScripts), DynamicDataSourceType.Method)]
    [Timeout(10000)]
    public async Task Script_BatMatchesCmd(string name, string scriptPath)
    {
        var scriptDir = Path.GetDirectoryName(scriptPath)!;

        var (cmdOut, cmdErr) = await RunWithCmdAsync(scriptPath);
        var (batOut, batErr) = await RunWithRealFsAsync(scriptPath);

        Assert.AreEqual(cmdOut, batOut, $"stdout mismatch in {name}");
        Assert.AreEqual(cmdErr, batErr, $"stderr mismatch in {name}");

        // Oracle passed — regenerate golden files from virtual-FS bat output
        if (SourceExamplesDir != null && !NoGoldenScripts.Contains(Path.GetFileNameWithoutExtension(name)))
        {
            var baseName = Path.GetFileNameWithoutExtension(name);

            // Write a placeholder golden file to the build-output dir first so that
            // scripts like dir.bat (which list their own directory) see a stable set
            // of files when we run the virtual-FS pass below.
            var buildOutPath = Path.Combine(ExamplesDir, $"{baseName}.txt");
            if (!File.Exists(buildOutPath))
                await File.WriteAllTextAsync(buildOutPath, "");

            var (goldenOut, goldenErr) = await RunWithVirtualFsAsync(scriptPath);
            await File.WriteAllTextAsync(Path.Combine(SourceExamplesDir, $"{baseName}.txt"), goldenOut);
            await File.WriteAllTextAsync(buildOutPath, goldenOut);
            if (!string.IsNullOrEmpty(goldenErr))
            {
                await File.WriteAllTextAsync(Path.Combine(SourceExamplesDir, $"{baseName}.err.txt"), goldenErr);
                await File.WriteAllTextAsync(Path.Combine(ExamplesDir, $"{baseName}.err.txt"), goldenErr);
            }
            else
            {
                foreach (var dir in new[] { SourceExamplesDir, ExamplesDir })
                {
                    var errPath = Path.Combine(dir, $"{baseName}.err.txt");
                    if (File.Exists(errPath)) File.Delete(errPath);
                }
            }
        }
    }
#endif

    // ── Golden file test: bat (virtual FS) vs .txt golden file (all platforms) ──

    [DataTestMethod]
    [DynamicData(nameof(GetScripts), DynamicDataSourceType.Method)]
    [Timeout(10000)]
    public async Task Script_BatMatchesGolden(string name, string scriptPath)
    {
        var baseName = Path.GetFileNameWithoutExtension(name);
        if (NoGoldenScripts.Contains(baseName))
        {
            Assert.Inconclusive($"{name} has filesystem-dependent output — golden test skipped.");
            return;
        }
        var goldenOutPath = Path.Combine(ExamplesDir, $"{baseName}.txt");
        if (!File.Exists(goldenOutPath))
        {
            Assert.Inconclusive($"No golden file for {name} — run tests on Windows first to generate.");
            return;
        }

        var (batOut, batErr) = await RunWithVirtualFsAsync(scriptPath);

        var goldenOut = await File.ReadAllTextAsync(goldenOutPath);
        var goldenErrPath = Path.Combine(ExamplesDir, $"{baseName}.err.txt");
        var goldenErr = File.Exists(goldenErrPath)
            ? await File.ReadAllTextAsync(goldenErrPath)
            : "";

        Assert.AreEqual(goldenOut, batOut, $"stdout mismatch in {name}");
        Assert.AreEqual(goldenErr, batErr, $"stderr mismatch in {name}");
    }

    // ── Runners ─────────────────────────────────────────────────────────────

#if WINDOWS
    /// <summary>Runs a script through cmd.exe against the real filesystem.</summary>
    private static async Task<(string Out, string Err)> RunWithCmdAsync(string scriptPath)
    {
        var scriptDir = Path.GetDirectoryName(scriptPath)!;
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
            WorkingDirectory = scriptDir
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

        return (Normalize(await outTask, scriptDir), Normalize(await errTask, scriptDir));
    }

    /// <summary>Detects the console width that cmd.exe inherits from the parent process.</summary>
    private static readonly Lazy<int> CmdConsoleWidth = new(() =>
    {
        try
        {
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/C mode con")
            {
                RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
            };
            using var proc = Process.Start(psi)!;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            var match = System.Text.RegularExpressions.Regex.Match(output, @"Columns:\s+(\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 80;
        }
        catch { return 80; }
    });

    /// <summary>Runs a script through Bat against the real filesystem (DosFileSystem).</summary>
    private static async Task<(string Out, string Err)> RunWithRealFsAsync(string scriptPath)
    {
        var scriptDir = Path.GetDirectoryName(scriptPath)!;
        var scriptDrive = char.ToUpperInvariant(scriptDir[0]);
        var driveRoot = $"{scriptDrive}:\\";

        var fs = new BatD.Context.Dos.DosFileSystem(new Dictionary<char, string> { [scriptDrive] = driveRoot });
        var console = new TestConsole { WindowWidth = CmdConsoleWidth.Value };
        var ctx = new TestCommandContext(fs) { Console = console, FileCulture = NormalizedFileCulture.Create(System.Globalization.CultureInfo.CurrentCulture) };
        ctx.SetCurrentDrive(scriptDrive);

        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            ctx.EnvironmentVariables[entry.Key.ToString()!] = entry.Value?.ToString() ?? "";

        var testOutputDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var existingPath = ctx.EnvironmentVariables.TryGetValue("PATH", out var p) ? p : "";
        ctx.EnvironmentVariables["PATH"] = testOutputDir + ";" + existingPath;

        var segments = scriptDir[3..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
        ctx.SetPath(scriptDrive, segments);

        var bc = new BatchContext { Context = ctx };
        var executor = new BatchExecutor();
        await executor.ExecuteAsync(scriptPath, "", bc, []);

        return (Normalize(console.OutText, scriptDir), Normalize(console.ErrText, scriptDir));
    }
#endif

    /// <summary>Runs a script through Bat against the platform's virtual filesystem.</summary>
    /// <remarks>
    /// Currently still backed by the real filesystem. For scripts that produce
    /// filesystem-dependent output (e.g. dir.bat with timestamps and sizes),
    /// golden files will only match on the same machine at the same point in time.
    /// A fully deterministic in-memory IFileSystem is needed for stable golden files.
    /// </remarks>
    private static async Task<(string Out, string Err)> RunWithVirtualFsAsync(string scriptPath)
    {
        var scriptDir = Path.GetDirectoryName(scriptPath)!;
        var scriptDrive = char.ToUpperInvariant(scriptDir[0]);
        var driveRoot = $"{scriptDrive}:\\";

#if WINDOWS
        var fs = new BatD.Context.Dos.DosFileSystem(new Dictionary<char, string> { [scriptDrive] = driveRoot });
#else
        var fs = new BatD.Context.Ux.UxFileSystemAdapter(new Dictionary<char, string> { [scriptDrive] = driveRoot });
#endif
        var console = new TestConsole();
        var ctx = new TestCommandContext(fs) { Console = console, FileCulture = NormalizedFileCulture.Create(System.Globalization.CultureInfo.CurrentCulture) };
        ctx.SetCurrentDrive(scriptDrive);

        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            ctx.EnvironmentVariables[entry.Key.ToString()!] = entry.Value?.ToString() ?? "";

        var testOutputDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var existingPath = ctx.EnvironmentVariables.TryGetValue("PATH", out var p) ? p : "";
        ctx.EnvironmentVariables["PATH"] = testOutputDir + ";" + existingPath;

        var segments = scriptDir[3..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
        ctx.SetPath(scriptDrive, segments);

        var bc = new BatchContext { Context = ctx };
        var executor = new BatchExecutor();
        await executor.ExecuteAsync(scriptPath, "", bc, []);

        return (Normalize(console.OutText, scriptDir), Normalize(console.ErrText, scriptDir));
    }

    // ── Helpers

    /// <summary>
    /// Normalizes output for comparison: unifies line endings, trims trailing whitespace,
    /// lowercases, and replaces the script directory path with a {dir} placeholder so that
    /// golden files are machine-independent.
    /// </summary>
    private static string Normalize(string s, string scriptDir)
    {
        s = s.Replace(scriptDir + "\\", "{dir}", StringComparison.OrdinalIgnoreCase);
        s = s.Replace(scriptDir, "{dir}", StringComparison.OrdinalIgnoreCase);

        // Normalize AM/PM designators to their OEM fallback so bat (UTF-8) and cmd.exe (OEM) compare equal.
        // Characters outside the OEM codepage are replaced with '?' by cmd.exe; we do the same for bat output.
        var am = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.AMDesignator;
        var pm = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.PMDesignator;
        if (!string.IsNullOrEmpty(am) && am.Any(c => c > 127)) s = s.Replace(am, new string('?', am.Length));
        if (!string.IsNullOrEmpty(pm) && pm.Any(c => c > 127)) s = s.Replace(pm, new string('?', pm.Length));

        // Normalize volatile dir output: timestamps+sizes, summaries, and volume serial vary between runs
        s = System.Text.RegularExpressions.Regex.Replace(s,
            @"\d{4}\.\d{2}\.\d{2}\s+\d{2}:\d{2}\s*\S*\s+(?:<DIR>|[\d,]+)",
            "{entry}",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        s = System.Text.RegularExpressions.Regex.Replace(s,
            @"\d+ File\(s\)\s+[\d,.]+ bytes",
            "{files}",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        s = System.Text.RegularExpressions.Regex.Replace(s,
            @"\d+ Dir\(s\)\s+[\d,.]+ bytes free",
            "{dirs}",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        s = System.Text.RegularExpressions.Regex.Replace(s,
            @"Volume Serial Number is [0-9a-fA-F]{4}-[0-9a-fA-F]{4}",
            "Volume Serial Number is {serial}",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var lines = s.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n').Split('\n');
        return string.Join("\n", lines.Select(l => l.TrimEnd())).ToLowerInvariant();
    }

    private static string? FindSourceExamplesDir()
    {
        var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "UnitTests", "Examples");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}
