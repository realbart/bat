using System.Diagnostics;
using Bat.Context;
using Bat.Execution;
using Bat.Nodes;
using BatD.Files;
using Context;

namespace Bat.Commands;

internal enum StartWindowStyle
{
    Normal,
    Minimized,
    Maximized
}

internal enum StartPriority
{
    Normal,
    Low,
    High,
    RealTime,
    AboveNormal,
    BelowNormal
}

internal sealed class StartArguments
{
    public string? Title { get; set; }
    public string? Command { get; set; }
    public string Arguments { get; set; } = "";
    public string? WorkingDirectory { get; set; }
    public bool Background { get; set; }
    public bool Wait { get; set; }
    public bool NewEnvironment { get; set; }
    public StartWindowStyle WindowStyle { get; set; } = StartWindowStyle.Normal;
    public StartPriority Priority { get; set; } = StartPriority.Normal;
}

[BuiltInCommand("start")]
internal class StartCommand : ICommand
{
    private const string HelpText =
        """
        Starts a separate window to run a specified program or command.

        START ["title"] [/D path] [/I] [/MIN] [/MAX] [/NORMAL] [/WAIT] [/B]
              [/LOW | /BELOWNORMAL | /NORMAL | /HIGH | /REALTIME | /ABOVENORMAL]
              [command/program] [parameters]

            "title"     Title to display in the window title bar.
            /D path     Starting directory.
            /B          Start application without creating a new window. The
                        application has ^C handling ignored. Unless the application
                        enables ^C processing, ^Break is the only way to interrupt
                        the application.
            /I          The new environment will be the original environment passed
                        to the cmd.exe and not the current environment.
            /MIN        Start window minimized.
            /MAX        Start window maximized.
            /NORMAL     Start window in normal state.
            /LOW        Start application in the IDLE priority class.
            /BELOWNORMAL Start application in the BELOWNORMAL priority class.
            /ABOVENORMAL Start application in the ABOVENORMAL priority class.
            /HIGH       Start application in the HIGH priority class.
            /REALTIME   Start application in the REALTIME priority class.
            /WAIT       Start application and wait for it to terminate.
        """;

    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        if (arguments.IsHelpRequest)
        {
            await batchContext.Console.Out.WriteLineAsync(HelpText);
            return 0;
        }

        var words = arguments.FullArgument
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        // Re-join quoted strings
        var merged = MergeQuotedWords(words);
        var startArgs = ParseStartArguments(merged);

        var context = batchContext.Context;

        // START with no command or START CMD/BAT → open a new bat window
        if (startArgs.Command == null || IsCmdLaunch(startArgs.Command))
        {
            if (startArgs.Background) return 0; // /B with no command does nothing
            return await LaunchNewWindowAsync(startArgs, context);
        }

        // Resolve the executable
        var executablePath = await ExecutableResolver.ResolveAsync(startArgs.Command, context);
        if (executablePath == null)
        {
            await batchContext.Console.Error.WriteLineAsync($"'{startArgs.Command}' is not recognized as an internal or external command,");
            await batchContext.Console.Error.WriteLineAsync("operable program or batch file.");
            return 1;
        }

        var workingDir = startArgs.WorkingDirectory
            ?? context.FileSystem.GetNativePath(context.CurrentDrive, context.CurrentPath);
        var hostExecutablePath = PathTranslator.TranslateBatPathToHost(executablePath, context.FileSystem);

        var psi = new ProcessStartInfo(hostExecutablePath, startArgs.Arguments)
        {
            WorkingDirectory = workingDir,
            UseShellExecute = !startArgs.Background,
            WindowStyle = startArgs.WindowStyle switch
            {
                StartWindowStyle.Minimized => ProcessWindowStyle.Minimized,
                StartWindowStyle.Maximized => ProcessWindowStyle.Maximized,
                _ => ProcessWindowStyle.Normal
            }
        };

        if (!psi.UseShellExecute)
        {
            foreach (var (key, value) in PathTranslator.TranslateBatEnvironmentToHost(
                (IReadOnlyDictionary<string, string>)context.EnvironmentVariables, context.FileSystem))
                psi.Environment[key] = value;
        }

        var process = Process.Start(psi);
        if (process == null) return 1;

        if (startArgs.Priority != StartPriority.Normal)
        {
            try
            {
                process.PriorityClass = startArgs.Priority switch
                {
                    StartPriority.Low => ProcessPriorityClass.Idle,
                    StartPriority.BelowNormal => ProcessPriorityClass.BelowNormal,
                    StartPriority.AboveNormal => ProcessPriorityClass.AboveNormal,
                    StartPriority.High => ProcessPriorityClass.High,
                    StartPriority.RealTime => ProcessPriorityClass.RealTime,
                    _ => ProcessPriorityClass.Normal
                };
            }
            catch
            {
                // Priority may fail if process already exited or insufficient privileges
            }
        }

        if (startArgs.Wait)
        {
            await process.WaitForExitAsync();
            context.ErrorCode = process.ExitCode;
            return process.ExitCode;
        }

        return 0;
    }

    internal static StartArguments ParseStartArguments(string[] words)
    {
        var result = new StartArguments();
        var i = 0;

        // First quoted arg is the title
        if (i < words.Length && words[i].StartsWith('"') && words[i].EndsWith('"'))
        {
            result.Title = words[i][1..^1];
            i++;
        }

        // Parse flags
        while (i < words.Length)
        {
            var word = words[i].ToUpperInvariant();
            switch (word)
            {
                case "/B":
                    result.Background = true;
                    i++;
                    break;
                case "/WAIT":
                    result.Wait = true;
                    i++;
                    break;
                case "/MIN":
                    result.WindowStyle = StartWindowStyle.Minimized;
                    i++;
                    break;
                case "/MAX":
                    result.WindowStyle = StartWindowStyle.Maximized;
                    i++;
                    break;
                case "/NORMAL":
                    result.Priority = StartPriority.Normal;
                    result.WindowStyle = StartWindowStyle.Normal;
                    i++;
                    break;
                case "/I":
                    result.NewEnvironment = true;
                    i++;
                    break;
                case "/D":
                    i++;
                    if (i < words.Length)
                    {
                        result.WorkingDirectory = words[i];
                        i++;
                    }
                    break;
                case "/LOW":
                    result.Priority = StartPriority.Low;
                    i++;
                    break;
                case "/HIGH":
                    result.Priority = StartPriority.High;
                    i++;
                    break;
                case "/REALTIME":
                    result.Priority = StartPriority.RealTime;
                    i++;
                    break;
                case "/ABOVENORMAL":
                    result.Priority = StartPriority.AboveNormal;
                    i++;
                    break;
                case "/BELOWNORMAL":
                    result.Priority = StartPriority.BelowNormal;
                    i++;
                    break;
                default:
                    goto doneFlags;
            }
        }
        doneFlags:

        // Remaining: command + arguments
        if (i < words.Length)
        {
            result.Command = words[i];
            i++;
            if (i < words.Length)
                result.Arguments = string.Join(" ", words[i..]);
        }

        return result;
    }

    /// <summary>
    /// Determines whether the command is a CMD/BAT launch (should open a new shell window).
    /// </summary>
    internal static bool IsCmdLaunch(string command)
    {
        var name = Path.GetFileNameWithoutExtension(command).ToUpperInvariant();
        return name is "CMD" or "BAT";
    }

    /// <summary>
    /// Spawns a new bat process in a new window (naïve — no daemon, no terminal detection).
    /// </summary>
    private static async Task<int> LaunchNewWindowAsync(StartArguments startArgs, IContext context)
    {
        var batExePath = ResolveBatPath();
        var arguments = startArgs.Arguments;

        // If START CMD /C echo hi → pass /C echo hi as arguments to the new bat
        if (startArgs.Command != null && IsCmdLaunch(startArgs.Command) && !string.IsNullOrEmpty(arguments))
        {
            // arguments already contains everything after the command
        }
        else
        {
            arguments = "";
        }

        var workingDir = startArgs.WorkingDirectory
            ?? context.FileSystem.GetNativePath(context.CurrentDrive, context.CurrentPath);

        var fullCommand = string.IsNullOrEmpty(arguments) ? batExePath : $"{batExePath} {arguments}";

        ProcessStartInfo psi;

        if (OperatingSystem.IsWindows())
        {
            psi = new ProcessStartInfo(batExePath, arguments)
            {
                WorkingDirectory = workingDir,
                UseShellExecute = true,
                WindowStyle = startArgs.WindowStyle switch
                {
                    StartWindowStyle.Minimized => ProcessWindowStyle.Minimized,
                    StartWindowStyle.Maximized => ProcessWindowStyle.Maximized,
                    _ => ProcessWindowStyle.Normal
                }
            };
        }
        else
        {
            // Linux: detect terminal emulator and spawn in a new window
            var template = TerminalDetector.Detect();
            if (template == null)
            {
                // No terminal found — fall back to background spawn
                psi = new ProcessStartInfo(batExePath, arguments)
                {
                    WorkingDirectory = workingDir,
                    UseShellExecute = false
                };
            }
            else
            {
                var (exe, args) = TerminalDetector.BuildLaunchCommand(template, fullCommand);
                psi = new ProcessStartInfo(exe, args)
                {
                    WorkingDirectory = workingDir,
                    UseShellExecute = false
                };
            }
        }

        var process = Process.Start(psi);
        if (process == null) return 1;

        if (startArgs.Wait)
        {
            await process.WaitForExitAsync();
            context.ErrorCode = process.ExitCode;
            return process.ExitCode;
        }

        return 0;
    }

    private static string[] MergeQuotedWords(List<string> words)
    {
        var result = new List<string>();
        var i = 0;
        while (i < words.Count)
        {
            if (words[i].StartsWith('"') && !words[i].EndsWith('"'))
            {
                var buf = words[i];
                i++;
                while (i < words.Count)
                {
                    buf += " " + words[i];
                    if (words[i].EndsWith('"'))
                    {
                        i++;
                        break;
                    }
                    i++;
                }
                result.Add(buf);
            }
            else
            {
                result.Add(words[i]);
                i++;
            }
        }
        return result.ToArray();
    }

    /// <summary>
    /// Resolves the path to bat.exe (the terminal proxy), which lives
    /// in the same directory as the current process (batd).
    /// </summary>
    private static string ResolveBatPath()
    {
        var dir = AppContext.BaseDirectory;
        var name = OperatingSystem.IsWindows() ? "bat.exe" : "bat";
        var path = Path.Combine(dir, name);
        return File.Exists(path) ? path : name; // fallback to PATH lookup
    }
}
