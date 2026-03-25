namespace Bat.Context;

internal class DosContext : Context
{
    public DosContext() : this(new DosFileSystem()) { }

    public DosContext(DosFileSystem fs) : base(fs)
    {
        InitializeFromEnvironment();
    }

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
