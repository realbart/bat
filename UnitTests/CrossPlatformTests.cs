using System.Diagnostics;

namespace Bat.UnitTests;

[TestClass]
public class CrossPlatformTests
{
    [TestMethod]
    [TestCategory("WSL")]
    public void Bat_LinuxBuild_ShowsUnixHelp()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("WSL tests only run on Windows");
            return;
        }

        var linuxBinary = Path.GetFullPath("../publish/linux-x64/Bat");
        if (!File.Exists(linuxBinary))
        {
            Assert.Inconclusive($"Linux binary not found at {linuxBinary}. Run build-release.ps1 or build in Release mode first.");
            return;
        }

        var wslPath = ConvertToWSLPath(linuxBinary);
        var output = RunWSL($"chmod +x {wslPath} && {wslPath} -h");

        Assert.IsTrue(output.Contains("bat [-h"), $"Expected Unix help, got:\n{output}");
        Assert.IsTrue(output.Contains("-m X path"), "Should contain Unix -m flag syntax");
        Assert.IsFalse(output.Contains("/M"), "Should NOT contain Windows /M syntax");
    }

    [TestMethod]
    [TestCategory("WSL")]
    public void Bat_LinuxBuild_ExecutesCommand()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("WSL tests only run on Windows");
            return;
        }

        var linuxBinary = Path.GetFullPath("../publish/linux-x64/Bat");
        if (!File.Exists(linuxBinary))
        {
            Assert.Inconclusive($"Linux binary not found. Build in Release mode first.");
            return;
        }

        var wslPath = ConvertToWSLPath(linuxBinary);
        var output = RunWSL($"chmod +x {wslPath} && {wslPath} -c 'echo Hello from WSL'");

        Assert.IsTrue(output.Contains("Hello from WSL"), $"Expected command output, got:\n{output}");
    }

    private static string RunWSL(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "wsl.exe",
            Arguments = $"-e bash -c \"{command.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi);
        if (process == null) throw new InvalidOperationException("Failed to start WSL");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return string.IsNullOrEmpty(error) ? output : output + "\nSTDERR:\n" + error;
    }

    private static string ConvertToWSLPath(string windowsPath)
    {
        var fullPath = Path.GetFullPath(windowsPath);
        if (fullPath.Length < 3 || fullPath[1] != ':')
            return fullPath;

        var drive = char.ToLowerInvariant(fullPath[0]);
        var unixPath = fullPath[2..].Replace('\\', '/');
        return $"/mnt/{drive}{unixPath}";
    }
}
