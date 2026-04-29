using BatD.Files;

namespace Bat.Context.Ux;

public class UxContextAdapter : Context
{
    public UxContextAdapter(global::Context.IConsole console) : this(new(), console) { }

    public UxContextAdapter(UxFileSystemAdapter fs, global::Context.IConsole console) : base(fs, console)
    {
        InitializeFromEnvironment();
    }

    private UxContextAdapter(UxFileSystemAdapter fs, global::Context.IConsole console, bool skipInit) : base(fs, console)
    {
    }

    public override global::Context.IContext StartNew(global::Context.IConsole? console = null)
    {
        var newContext = new UxContextAdapter((UxFileSystemAdapter)FileSystem, console ?? Console, skipInit: true)
        {
            CurrentDrive = this.CurrentDrive,
            ErrorCode = this.ErrorCode,
            EchoEnabled = this.EchoEnabled,
            DelayedExpansion = this.DelayedExpansion,
            ExtensionsEnabled = this.ExtensionsEnabled,
            PromptFormat = this.PromptFormat,
            HistorySize = this.HistorySize,
            CurrentBatch = this.CurrentBatch
        };

        return StartNewCore(newContext);
    }

    protected override void PostProcessEnvironmentVariables()
    {
        // Translate environment variables whose values are Unix absolute paths.
        foreach (var key in EnvironmentVariables.Keys.ToList())
        {
            if (key.Equals("PATH", StringComparison.OrdinalIgnoreCase)) continue;

            var value = EnvironmentVariables[key];
            if (!value.StartsWith('/')) continue;

            var translated = BatD.Files.PathTranslator.TranslateHostPathToBat(value, FileSystem);
            if (string.IsNullOrEmpty(translated))
                EnvironmentVariables.Remove(key);
            else
                EnvironmentVariables[key] = translated;
        }

        SynthesizeCmdVariables();
    }

    private void SynthesizeCmdVariables()
    {
        var user = Environment.UserName;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var homeTranslated = BatD.Files.PathTranslator.TranslateHostPathToBat(home, FileSystem);
        var hostname = Environment.MachineName;
        var temp = Environment.GetEnvironmentVariable("TMPDIR") ?? "/tmp";
        var tempTranslated = BatD.Files.PathTranslator.TranslateHostPathToBat(temp, FileSystem);

        // Split translated home into HOMEDRIVE + HOMEPATH (e.g. Z:\Users\kempsb → Z: + \Users\kempsb)
        var homeDrive = "";
        var homePath = "\\";
        if (!string.IsNullOrEmpty(homeTranslated) && homeTranslated.Length >= 2 && homeTranslated[1] == ':')
        {
            homeDrive = homeTranslated[..2];
            homePath = homeTranslated.Length > 2 ? homeTranslated[2..] : "\\";
        }

        SetIfMissing("COMPUTERNAME", hostname);
        SetIfMissing("HOMEDRIVE", homeDrive);
        SetIfMissing("HOMEPATH", homePath);
        SetIfMissing("USERPROFILE", homeTranslated);
        SetIfMissing("USERNAME", user);
        SetIfMissing("OS", "Windows_NT");
        SetIfMissing("ComSpec", $"{homeDrive}\\WINDOWS\\system32\\cmd.exe");
        SetIfMissing("SystemRoot", $"{homeDrive}\\WINDOWS");
        SetIfMissing("SystemDrive", homeDrive);
        SetIfMissing("TEMP", tempTranslated);
        SetIfMissing("TMP", tempTranslated);
        SetIfMissing("ALLUSERSPROFILE", $"{homeDrive}\\ProgramData");
        SetIfMissing("ProgramData", $"{homeDrive}\\ProgramData");
        SetIfMissing("ProgramFiles", $"{homeDrive}\\Program Files");
        SetIfMissing("ProgramFiles(x86)", $"{homeDrive}\\Program Files (x86)");
        SetIfMissing("ProgramW6432", $"{homeDrive}\\Program Files");
        SetIfMissing("CommonProgramFiles", $"{homeDrive}\\Program Files\\Common Files");
        SetIfMissing("CommonProgramFiles(x86)", $"{homeDrive}\\Program Files (x86)\\Common Files");
        SetIfMissing("CommonProgramW6432", $"{homeDrive}\\Program Files\\Common Files");
        SetIfMissing("PUBLIC", $"{homeDrive}\\Users\\Public");
        SetIfMissing("APPDATA", $"{homeTranslated}\\AppData\\Roaming");
        SetIfMissing("LOCALAPPDATA", $"{homeTranslated}\\AppData\\Local");
        SetIfMissing("DriverData", $"{homeDrive}\\Windows\\System32\\Drivers\\DriverData");
        SetIfMissing("SESSIONNAME", "Console");
    }

    private void SetIfMissing(string key, string value)
    {
        if (!string.IsNullOrEmpty(value) && !EnvironmentVariables.ContainsKey(key))
            EnvironmentVariables[key] = value;
    }

    protected override void InitializeCurrentDirectory()
    {
        var cwd = Environment.CurrentDirectory;
        if (FileSystem is not UxFileSystemAdapter ux) return;

        // Find first mapping (in insertion order) whose root is a prefix of the CWD
        foreach (var (drive, root) in ux.GetRoots())
        {
            var rootNorm = root.TrimEnd('/');
            if (cwd == rootNorm || cwd.StartsWith(rootNorm + "/", StringComparison.Ordinal))
            {
                CurrentDrive = drive;
                var remainder = cwd.Length > rootNorm.Length ? cwd[(rootNorm.Length + 1)..] : "";
                CurrentFolders[drive] = remainder.Length > 0
                    ? remainder.Split('/', StringSplitOptions.RemoveEmptyEntries)
                    : [];
                return;
            }
        }

        // No mapping resolves — use first mapped drive at root
        CurrentDrive = ux.GetRoots().First().Key;
        CurrentFolders[CurrentDrive] = [];
    }
}
