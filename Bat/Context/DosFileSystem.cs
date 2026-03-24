namespace Bat.Context;

/// <summary>
/// Windows filesystem implementation. Maintains a dictionary that maps
/// virtual drive letters to native root paths.
/// On startup C: is mapped to Z:, making it visible that Bat uses virtual drives.
/// </summary>
internal class DosFileSystem : FileSystem
{
    private readonly Dictionary<char, string> _roots;

    /// <summary>Creates a DosFileSystem with an explicit drive-root mapping (used in tests).</summary>
    public DosFileSystem(Dictionary<char, string> roots)
    {
        _roots = new Dictionary<char, string>(roots);
    }

    /// <summary>
    /// Creates a DosFileSystem with the default Windows mapping:
    /// virtual Z: → native C:\ (root of the system drive).
    /// No other drives are mapped — additional drives must be added explicitly
    /// via /M command-line arguments or SUBST commands.
    /// On Linux the equivalent would be virtual Z: → native /.
    /// </summary>
    public DosFileSystem() : this(new Dictionary<char, string> { ['Z'] = @"C:\" }) { }

    // ── path resolution ──────────────────────────────────────────────────

    public override string GetNativePath(char drive, string[] path)
    {
        var upper = char.ToUpperInvariant(drive);
        if (!_roots.TryGetValue(upper, out var root))
            throw new DriveNotFoundException($"Drive {upper}: is not mapped.");
        return path.Length == 0 ? root : Path.Combine([root, .. path]);
    }

    public bool HasDrive(char drive) => _roots.ContainsKey(char.ToUpperInvariant(drive));

    // ── existence ────────────────────────────────────────────────────────

    public override bool FileExists(char drive, string[] path) =>
        File.Exists(GetNativePath(drive, path));

    public override bool DirectoryExists(char drive, string[] path) =>
        Directory.Exists(GetNativePath(drive, path));

    // ── directory operations ─────────────────────────────────────────────

    public override void CreateDirectory(char drive, string[] path) =>
        Directory.CreateDirectory(GetNativePath(drive, path));

    public override void DeleteDirectory(char drive, string[] path, bool recursive) =>
        Directory.Delete(GetNativePath(drive, path), recursive);

    public override IEnumerable<(string Name, bool IsDirectory)> EnumerateEntries(
        char drive, string[] path, string pattern)
    {
        var native = GetNativePath(drive, path);
        if (!Directory.Exists(native)) yield break;
        foreach (var entry in Directory.EnumerateFileSystemEntries(native, pattern))
            yield return (Path.GetFileName(entry), Directory.Exists(entry));
    }

    // ── file operations ──────────────────────────────────────────────────

    public override void DeleteFile(char drive, string[] path) =>
        File.Delete(GetNativePath(drive, path));

    public override void CopyFile(
        char srcDrive, string[] srcPath, char dstDrive, string[] dstPath, bool overwrite) =>
        File.Copy(GetNativePath(srcDrive, srcPath), GetNativePath(dstDrive, dstPath), overwrite);

    public override void MoveFile(
        char srcDrive, string[] srcPath, char dstDrive, string[] dstPath) =>
        File.Move(GetNativePath(srcDrive, srcPath), GetNativePath(dstDrive, dstPath));

    public override void RenameFile(char drive, string[] path, string newName)
    {
        var src = GetNativePath(drive, path);
        var dst = Path.Combine(Path.GetDirectoryName(src)!, newName);
        File.Move(src, dst);
    }

    // ── file I/O ─────────────────────────────────────────────────────────

    public override Stream OpenRead(char drive, string[] path) =>
        File.OpenRead(GetNativePath(drive, path));

    public override Stream OpenWrite(char drive, string[] path, bool append) =>
        append
            ? new FileStream(GetNativePath(drive, path), FileMode.Append, FileAccess.Write)
            : File.OpenWrite(GetNativePath(drive, path));

    public override string ReadAllText(char drive, string[] path) =>
        File.ReadAllText(GetNativePath(drive, path));

    public override void WriteAllText(char drive, string[] path, string content) =>
        File.WriteAllText(GetNativePath(drive, path), content);

    // ── metadata ─────────────────────────────────────────────────────────

    public override FileAttributes GetAttributes(char drive, string[] path) =>
        File.GetAttributes(GetNativePath(drive, path));

    public override void SetAttributes(char drive, string[] path, FileAttributes attributes) =>
        File.SetAttributes(GetNativePath(drive, path), attributes);

    public override long GetFileSize(char drive, string[] path) =>
        new FileInfo(GetNativePath(drive, path)).Length;

    public override DateTime GetLastWriteTime(char drive, string[] path) =>
        File.GetLastWriteTime(GetNativePath(drive, path));
}

