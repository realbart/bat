namespace Bat.Context;

internal class UxContextAdapter : Context
{
    public UxContextAdapter() : this(new UxFileSystemAdapter()) { }

    public UxContextAdapter(UxFileSystemAdapter fs) : base(fs)
    {
        InitializeFromEnvironment();
    }

    protected override void PostProcessEnvironmentVariables()
    {
        // Translate environment variables whose values are Unix absolute paths (start with '/').
        // Multi-path variables (e.g. XDG_DATA_DIRS=/usr/share:/usr/local/share) are handled
        // by TranslateHostPathToBat which splits on NativePathSeparator (':') automatically.
        // Variables whose paths are not under any mapped drive are removed.
        // PATH is already translated by the base class.
        foreach (var key in EnvironmentVariables.Keys.ToList())
        {
            if (key.Equals("PATH", StringComparison.OrdinalIgnoreCase)) continue;

            var value = EnvironmentVariables[key];
            if (!value.StartsWith('/')) continue;

            var translated = PathTranslator.TranslateHostPathToBat(value, FileSystem);
            if (string.IsNullOrEmpty(translated))
                EnvironmentVariables.Remove(key);
            else
                EnvironmentVariables[key] = translated;
        }
    }

    protected override void InitializeCurrentDirectory()
    {
        var cwd = Environment.CurrentDirectory;
        if (FileSystem is not UxFileSystemAdapter ux) return;

        // Find the mapped drive whose root is a prefix of the current working directory
        for (var drive = 'A'; drive <= 'Z'; drive++)
        {
            if (!ux.TryGetNativePath(drive, [], out var root)) continue;

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

        // Fallback: first available drive, full path as segments
        for (var drive = 'A'; drive <= 'Z'; drive++)
        {
            if (!ux.HasDrive(drive)) continue;
            CurrentDrive = drive;
            CurrentFolders[drive] = cwd.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            return;
        }
    }
}
