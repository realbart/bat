namespace Bat.Context.Dos;

public class DosContext : Context
{
    public DosContext(DosFileSystem fs, global::Context.IConsole console) : base(fs, console)
    {
        InitializeFromEnvironment();
    }

    private DosContext(DosFileSystem fs, global::Context.IConsole console, DosContext inner) : base(fs, console, inner)
    {
    }

    public override global::Context.IContext StartNew(global::Context.IConsole? console = null)
    {
        var newContext = new DosContext((DosFileSystem)FileSystem, console ?? Console, this)
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
        var fs = (DosFileSystem)FileSystem;
        var fallbackDrive = FirstMappedDrive(fs);

        foreach (var key in EnvironmentVariables.Keys.ToList())
        {
            if (key.Equals("PATH", StringComparison.OrdinalIgnoreCase)) continue;

            var value = EnvironmentVariables[key];
            var bareDrive = value.Length == 2 && char.IsLetter(value[0]) && value[1] == ':';
            var absolutePath = value.Length >= 3 && char.IsLetter(value[0]) && value[1] == ':' && value[2] == '\\';

            if (!bareDrive && !absolutePath) continue;

            var toTranslate = bareDrive ? value + @"\" : value;
            var translated = PathTranslator.TranslateHostPathToBat(toTranslate, FileSystem);
            if (string.IsNullOrEmpty(translated))
                EnvironmentVariables.Remove(key);
            else
                EnvironmentVariables[key] = bareDrive ? translated.TrimEnd('\\') : translated;
        }

        EnsureSystemRoot(fallbackDrive);
        EnsureHome(fallbackDrive);
    }

    private void EnsureSystemRoot(char fallbackDrive)
    {
        if (!EnvironmentVariables.TryGetValue("SystemRoot", out var sysRoot)
            || !sysRoot.Contains(':'))
        {
            sysRoot = $@"{fallbackDrive}:\Windows";
            EnvironmentVariables["SystemRoot"] = sysRoot;
        }

        EnvironmentVariables["SystemDrive"] = sysRoot[..2];
    }

    private void EnsureHome(char fallbackDrive)
    {
        if (EnvironmentVariables.TryGetValue("HOMEDRIVE", out var hd)
            && EnvironmentVariables.TryGetValue("HOMEPATH", out var hp)
            && hd.Length == 2 && hp.Length > 0
            && FileSystem.DirectoryExistsAsync(hd[0],
                hp.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries)).GetAwaiter().GetResult())
            return;

        EnvironmentVariables["HOMEDRIVE"] = $"{fallbackDrive}:";
        EnvironmentVariables["HOMEPATH"] = @"\";
    }

    private static char FirstMappedDrive(DosFileSystem fs) => fs.FirstDrive();

    protected override void InitializeCurrentDirectory()
    {
        var cwd = Environment.CurrentDirectory;
        if (cwd.Length < 2 || cwd[1] != ':') return;

        var fs = (DosFileSystem)FileSystem;

        // Find first mapping whose native root is a prefix of the CWD
        foreach (var (drive, root) in fs.GetRoots())
        {
            var rootNorm = root.TrimEnd('\\');
            if (cwd.StartsWith(rootNorm + "\\", StringComparison.OrdinalIgnoreCase)
                || cwd.Equals(rootNorm, StringComparison.OrdinalIgnoreCase))
            {
                CurrentDrive = drive;
                var remainder = cwd.Length > rootNorm.Length + 1 ? cwd[(rootNorm.Length + 1)..] : "";
                CurrentFolders[drive] = remainder.Length > 0
                    ? remainder.Split('\\', StringSplitOptions.RemoveEmptyEntries)
                    : [];
                return;
            }
        }

        // No mapping resolves — use first mapped drive at root
        CurrentDrive = fs.FirstDrive();
        CurrentFolders[CurrentDrive] = [];
    }
}
