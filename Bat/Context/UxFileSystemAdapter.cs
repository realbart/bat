using Context;

namespace Bat.Context;

internal class UxFileSystemAdapter(Dictionary<char, string> mappings) : FileSystem
{
    public UxFileSystemAdapter() : this(new Dictionary<char, string> { ['Z'] = "/" }) { }

    public bool HasDrive(char drive) => mappings.ContainsKey(char.ToUpperInvariant(drive));

    public override char NativeDirectorySeparator => '/';
    public override char NativePathSeparator => ':';

    public override bool IsExecutable(char drive, string[] path)
    {
        var native = ResolveCaseInsensitive(GetNativePath(drive, path));
        if (!File.Exists(native)) return false;
        var mode = File.GetUnixFileMode(native);
        return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
    }
    public override bool TryGetNativePath(char drive, string[] path, out string nativePath)    {
        var upper = char.ToUpperInvariant(drive);
        if (!mappings.TryGetValue(upper, out var root))
        {
            nativePath = "";
            return false;
        }
        nativePath = path.Length == 0 ? root : root.TrimEnd('/') + '/' + string.Join('/', path);
        return true;
    }

    public override string GetNativePath(char drive, string[] path)
    {
        var upper = char.ToUpperInvariant(drive);
        mappings.TryGetValue(upper, out var root);
        root ??= $"/{char.ToLowerInvariant(upper)}:";
        if (path.Length == 0) return root;
        return root.TrimEnd('/') + '/' + string.Join('/', path);
    }

    private string ResolveCaseInsensitive(string nativePath)
    {
        if (File.Exists(nativePath) || Directory.Exists(nativePath)) return nativePath;

        var parts = nativePath.Split('/');
        var resolved = "";
        foreach (var part in parts)
        {
            if (part.Length == 0) { resolved += "/"; continue; }
            var candidate = Path.Combine(resolved, part);
            if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                resolved = candidate;
                continue;
            }
            var match = FindCaseInsensitive(resolved, part);
            resolved = match ?? candidate;
        }
        return resolved;
    }

    private static string? FindCaseInsensitive(string directory, string name)
    {
        if (!Directory.Exists(directory)) return null;
        return Directory.EnumerateFileSystemEntries(directory)
            .FirstOrDefault(e => string.Equals(Path.GetFileName(e), name, StringComparison.OrdinalIgnoreCase));
    }

    public override bool FileExists(char drive, string[] path)
    {
        var native = ResolveCaseInsensitive(GetNativePath(drive, path));
        return File.Exists(native);
    }

    public override bool DirectoryExists(char drive, string[] path)
    {
        var native = ResolveCaseInsensitive(GetNativePath(drive, path));
        return Directory.Exists(native);
    }

    public override void CreateDirectory(char drive, string[] path) =>
        Directory.CreateDirectory(GetNativePath(drive, path));

    public override void DeleteDirectory(char drive, string[] path, bool recursive) =>
        Directory.Delete(ResolveCaseInsensitive(GetNativePath(drive, path)), recursive);

    public override void DeleteFile(char drive, string[] path) =>
        File.Delete(ResolveCaseInsensitive(GetNativePath(drive, path)));

    public override IEnumerable<DosFileEntry> EnumerateEntries(char drive, string[] path, string pattern)
    {
        var native = ResolveCaseInsensitive(GetNativePath(drive, path));
        if (!Directory.Exists(native)) yield break;

        foreach (var entry in Directory.EnumerateFileSystemEntries(native, pattern))
        {
            var name = Path.GetFileName(entry);
            var isDir = Directory.Exists(entry);
            var info = new FileInfo(entry);
            yield return new DosFileEntry(
                name,
                isDir,
                "",
                isDir ? 0 : info.Length,
                File.GetLastWriteTime(entry),
                File.GetAttributes(entry),
                "");
        }
    }

    public override Stream OpenRead(char drive, string[] path) =>
        File.OpenRead(ResolveCaseInsensitive(GetNativePath(drive, path)));

    public override Stream OpenWrite(char drive, string[] path, bool append)
    {
        var native = GetNativePath(drive, path);
        return append
            ? new FileStream(native, FileMode.Append, FileAccess.Write)
            : File.OpenWrite(native);
    }

    public override string ReadAllText(char drive, string[] path) =>
        File.ReadAllText(ResolveCaseInsensitive(GetNativePath(drive, path)));

    public override void WriteAllText(char drive, string[] path, string content) =>
        File.WriteAllText(GetNativePath(drive, path), content);

    public override void CopyFile(char srcDrive, string[] srcPath, char dstDrive, string[] dstPath, bool overwrite) =>
        File.Copy(ResolveCaseInsensitive(GetNativePath(srcDrive, srcPath)), GetNativePath(dstDrive, dstPath), overwrite);

    public override void MoveFile(char srcDrive, string[] srcPath, char dstDrive, string[] dstPath) =>
        File.Move(ResolveCaseInsensitive(GetNativePath(srcDrive, srcPath)), GetNativePath(dstDrive, dstPath));

    public override void RenameFile(char drive, string[] path, string newName)
    {
        var src = ResolveCaseInsensitive(GetNativePath(drive, path));
        var dst = Path.Combine(Path.GetDirectoryName(src)!, newName);
        File.Move(src, dst);
    }

    public override FileAttributes GetAttributes(char drive, string[] path) =>
        File.GetAttributes(ResolveCaseInsensitive(GetNativePath(drive, path)));

    public override void SetAttributes(char drive, string[] path, FileAttributes attributes) =>
        File.SetAttributes(ResolveCaseInsensitive(GetNativePath(drive, path)), attributes);

    public override long GetFileSize(char drive, string[] path) =>
        new FileInfo(ResolveCaseInsensitive(GetNativePath(drive, path))).Length;

    public override DateTime GetLastWriteTime(char drive, string[] path) =>
        File.GetLastWriteTime(ResolveCaseInsensitive(GetNativePath(drive, path)));

    protected override uint GetVolumeSerialNumber(string nativeRoot) =>
        (uint)nativeRoot.GetHashCode(StringComparison.Ordinal);
}
