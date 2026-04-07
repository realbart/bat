namespace Bat.Context.Dos;

internal class DosContext : Context
{
    public DosContext(DosFileSystem fs) : base(fs)
    {
        InitializeFromEnvironment();
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
            && FileSystem.DirectoryExists(hd[0],
                hp.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries)))
            return;

        EnvironmentVariables["HOMEDRIVE"] = $"{fallbackDrive}:";
        EnvironmentVariables["HOMEPATH"] = @"\";
    }

    private static char FirstMappedDrive(DosFileSystem fs) => fs.FirstDrive();

    protected override void InitializeCurrentDirectory()
    {
        var dir = Environment.CurrentDirectory;
        if (dir.Length < 2 || dir[1] != ':') return;

        var nativeDrive = char.ToUpperInvariant(dir[0]);
        CurrentDrive = nativeDrive == 'C' ? 'Z' : nativeDrive;
        var segments = dir.Length > 3 ? dir[3..].Split('\\', StringSplitOptions.RemoveEmptyEntries) : [];
        CurrentFolders[CurrentDrive] = segments;
    }
}
