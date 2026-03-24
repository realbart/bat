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
        var dir = System.Environment.CurrentDirectory;
        if (dir.Length >= 2 && dir[1] == ':')
        {
            // C:\ is mapped to Z: — reflect that in the context's current drive
            var nativeDrive = char.ToUpperInvariant(dir[0]);
            CurrentDrive = nativeDrive == 'C' ? 'Z' : nativeDrive;
            var segments = dir.Length > 3
                ? dir[3..].Split('\\', System.StringSplitOptions.RemoveEmptyEntries)
                : [];
            CurrentFolders[CurrentDrive] = segments;
        }
    }
}
