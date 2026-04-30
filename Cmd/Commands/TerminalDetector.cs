using System.Diagnostics;

namespace Bat.Commands;

/// <summary>
/// Detects the current terminal emulator on Linux and provides launch templates
/// for opening new terminal windows. Data-driven: no if-chains.
/// </summary>
// todo: this is not a command
// also: this seems to be linux-specific. move it
internal static class TerminalDetector
{
    /// <summary>Known terminal emulators with their launch templates. {cmd} is replaced with the command to run.</summary>
    internal static readonly (string ProcessName, string LaunchTemplate)[] KnownTerminals =
    [
        ("x-terminal-emulator", "x-terminal-emulator -e {cmd}"),
        ("konsole",             "konsole -e {cmd}"),
        ("gnome-terminal-server", "gnome-terminal -- {cmd}"),
        ("xfce4-terminal",      "xfce4-terminal -e {cmd}"),
        ("tilix",               "tilix -e {cmd}"),
        ("alacritty",           "alacritty -e {cmd}"),
        ("xterm",               "xterm -e {cmd}"),
    ];

    /// <summary>
    /// Detects the terminal emulator by walking the process tree upward,
    /// then falling back to environment variables, then probing PATH.
    /// Returns the launch template with {cmd} placeholder, or null if none found.
    /// </summary>
    public static string? Detect()
    {
        // 1. Walk process tree (Linux: /proc/<pid>/status PPid)
        var fromTree = DetectFromProcessTree();
        if (fromTree != null) return fromTree;

        // 2. Environment variables
        var fromEnv = DetectFromEnvironment();
        if (fromEnv != null) return fromEnv;

        // 3. Probe known terminals in PATH
        return DetectFromPath();
    }

    /// <summary>
    /// Walks the process tree upward via /proc to find a known terminal.
    /// </summary>
    internal static string? DetectFromProcessTree()
    {
        try
        {
            var pid = Environment.ProcessId;
            var visited = new HashSet<int>();

            while (pid > 1 && visited.Add(pid))
            {
                var name = GetProcessName(pid);
                if (name != null)
                {
                    var template = FindTemplate(name);
                    if (template != null) return template;
                }
                pid = GetParentPid(pid);
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Checks TERM_PROGRAM and TERMINAL environment variables.
    /// </summary>
    internal static string? DetectFromEnvironment()
    {
        var termProgram = Environment.GetEnvironmentVariable("TERM_PROGRAM");
        if (termProgram != null)
        {
            var template = FindTemplate(Path.GetFileNameWithoutExtension(termProgram));
            if (template != null) return template;
        }

        var terminal = Environment.GetEnvironmentVariable("TERMINAL");
        if (terminal != null)
        {
            var template = FindTemplate(Path.GetFileNameWithoutExtension(terminal));
            if (template != null) return template;
        }

        return null;
    }

    /// <summary>
    /// Probes known terminals by checking if they exist on PATH.
    /// </summary>
    internal static string? DetectFromPath()
    {
        foreach (var (processName, template) in KnownTerminals)
        {
            if (IsOnPath(processName))
                return template;
        }
        return null;
    }

    /// <summary>
    /// Finds the launch template for a process name (case-insensitive match).
    /// </summary>
    internal static string? FindTemplate(string processName)
    {
        foreach (var (name, template) in KnownTerminals)
        {
            if (string.Equals(name, processName, StringComparison.OrdinalIgnoreCase))
                return template;
        }
        return null;
    }

    /// <summary>
    /// Builds the full launch command from a template and the command to execute.
    /// </summary>
    internal static (string Executable, string Arguments) BuildLaunchCommand(string template, string command)
    {
        var expanded = template.Replace("{cmd}", command);
        var spaceIdx = expanded.IndexOf(' ');
        if (spaceIdx < 0) return (expanded, "");
        return (expanded[..spaceIdx], expanded[(spaceIdx + 1)..]);
    }

    /// <summary>Gets the process name from /proc/&lt;pid&gt;/comm (Linux).</summary>
    private static string? GetProcessName(int pid)
    {
        try
        {
            var commPath = $"/proc/{pid}/comm";
            if (File.Exists(commPath))
                return File.ReadAllText(commPath).Trim();
        }
        catch { }
        return null;
    }

    /// <summary>Gets the parent PID from /proc/&lt;pid&gt;/status (Linux).</summary>
    private static int GetParentPid(int pid)
    {
        try
        {
            var statusPath = $"/proc/{pid}/status";
            if (!File.Exists(statusPath)) return 0;
            foreach (var line in File.ReadLines(statusPath))
            {
                if (line.StartsWith("PPid:", StringComparison.OrdinalIgnoreCase))
                {
                    var value = line["PPid:".Length..].Trim();
                    if (int.TryParse(value, out var ppid)) return ppid;
                }
            }
        }
        catch { }
        return 0;
    }

    /// <summary>Checks if an executable is available on PATH using 'which'.</summary>
    private static bool IsOnPath(string executable)
    {
        try
        {
            var psi = new ProcessStartInfo("which", executable)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(2000);
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }
}
