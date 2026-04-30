#if WINDOWS
using System.Diagnostics;
using System.Reflection;

namespace Bat.UnitTests;

/// <summary>
/// Runs the real-filesystem test suite inside WSL (linux-x64) so that the
/// UxFileSystemAdapter and UNIX code-paths are exercised for real.
///
/// On Linux these tests don't run — the tests themselves cover that platform.
/// The test is Inconclusive when WSL is not installed.
/// </summary>
[TestClass]
public class WslTests
{
    /// <summary>
    /// Filter that selects the test classes which exercise the real filesystem
    /// and have UNIX-specific code paths.
    /// </summary>
    private const string Filter =
        "FullyQualifiedName~UxFileSystemTests" +
        "|FullyQualifiedName~DirVolumeTests" +
        "|FullyQualifiedName~UnixPathMappingTests" +
        "|FullyQualifiedName~UnixDirDisplayTests" +
        "|FullyQualifiedName~ExampleScriptTests";

    [TestMethod]
    [Timeout(300_000)] // 5 min — includes restore + linux-x64 build
    public async Task RealFilesystemTests_PassUnderWsl()
    {
        if (!IsWslAvailable())
        {
            Assert.Inconclusive("WSL is not available on this machine.");
            return;
        }

        var projectFile = FindProjectFile();
        if (projectFile == null)
        {
            Assert.Inconclusive("UnitTests.csproj not found.");
            return;
        }

        var projectDir = Path.GetDirectoryName(projectFile)!;

        // Restore linux-x64 packages from Windows — downloads to the host NuGet cache,
        // accessible in WSL as /mnt/<drive>/... so no network needed inside WSL.
        var (restoreOut, restoreCode) = await RunProcessAsync("dotnet",
            $"restore \"{projectFile}\" -r linux-x64");
        if (restoreCode != 0)
            Assert.Fail($"dotnet restore linux-x64 failed.\n\n{restoreOut}");

        var nugetCacheWsl = ToWslPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages"));

        var wslProjectDir = ToWslPath(projectDir);
        var wslProjectFile = ToWslPath(projectFile);

        // Build and test in WSL with UNIX defined; point NUGET_PACKAGES at the host cache.
        var bashCommand =
            $"cd '{wslProjectDir}' && " +
            $"NUGET_PACKAGES='{nugetCacheWsl}' " +
            $"dotnet test '{wslProjectFile}' -r linux-x64 -f net10.0 --no-restore " +
            $"--filter \"{Filter}\" " +
            $"--logger \"console;verbosity=minimal\" 2>&1";

        var (output, exitCode) = await RunWslAsync($"bash -c \"{bashCommand.Replace("\"", "\\\"")}\"");

        Assert.AreEqual(0, exitCode,
            $"WSL test run failed (exit {exitCode}).\n\n{output}");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static bool IsWslAvailable()
    {
        var wslExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsl.exe");
        if (!File.Exists(wslExe)) return false;

        try
        {
            using var p = Process.Start(new ProcessStartInfo("wsl.exe", "--status")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            })!;
            p.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    private static async Task<(string Output, int ExitCode)> RunWslAsync(string bashArgs)
    {
        return await RunProcessAsync("wsl.exe", $"-- {bashArgs}");
    }

    private static async Task<(string Output, int ExitCode)> RunProcessAsync(string exe, string args)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)!;
        var output = await proc.StandardOutput.ReadToEndAsync();
        var err = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        var combined = string.IsNullOrEmpty(err) ? output : output + "\nSTDERR:\n" + err;
        return (combined, proc.ExitCode);
    }

    /// <summary>Converts a Windows path to the WSL /mnt/&lt;drive&gt;/... form.</summary>
    private static string ToWslPath(string winPath)
    {
        // "C:\foo\bar" → "/mnt/c/foo/bar"
        if (winPath.Length < 2 || winPath[1] != ':')
            return winPath.Replace('\\', '/');
        return "/mnt/" + char.ToLower(winPath[0]) + winPath[2..].Replace('\\', '/');
    }

    private static string? FindProjectFile()
    {
        var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "UnitTests", "UnitTests.csproj");
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}
#endif
