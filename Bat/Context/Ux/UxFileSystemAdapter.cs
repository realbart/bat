using Context;

namespace Bat.Context.Ux;

internal class UxFileSystemAdapter(Dictionary<char, string> mappings, Func<string, string>? getOwner = null) : FileSystem
{
    private static readonly Dictionary<string, string> UnixAssociations = new(StringComparer.OrdinalIgnoreCase)
    {
        [".bat"] = "batfile",
        [".cmd"] = "cmdfile",
        [".com"] = "comfile",
        [".dll"] = "dllfile",
        [".exe"] = "exefile"
    };

    public UxFileSystemAdapter() : this(new Dictionary<char, string> { ['Z'] = "/" }, UnixFileOwner.GetOwner) { }

    public bool HasDrive(char drive) => mappings.ContainsKey(char.ToUpperInvariant(drive));

    public override IReadOnlyDictionary<string, string> GetFileAssociations() => UnixAssociations;

    public override char NativeDirectorySeparator => '/';
    public override char NativePathSeparator => ':';

    public override bool IsExecutable(char drive, string[] path)
    {
        var native = ResolveCaseInsensitive(GetNativePath(drive, path));
        if (!File.Exists(native)) return false;
        var mode = File.GetUnixFileMode(native);
        return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
    }

    protected override bool TryGetNativePathCore(char drive, string[] path, out string nativePath)
    {
        if (!mappings.TryGetValue(drive, out var root))
        {
            nativePath = "";
            return false;
        }
        nativePath = path.Length == 0 ? root : root.TrimEnd('/') + '/' + string.Join('/', path);
        return true;
    }

    protected override string GetNativePathCore(char drive, string[] path)
    {
        mappings.TryGetValue(drive, out var root);
        root ??= $"/{char.ToLowerInvariant(drive)}:";
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

        EnsureShortNamesForDirectory(native);

        foreach (var entry in Directory.EnumerateFileSystemEntries(native, pattern))
        {
            var name = Path.GetFileName(entry);
            var isDir = Directory.Exists(entry);
            var info = new FileInfo(entry);
            _shortNameCache.TryGetValue(entry, out var shortName);
            yield return new DosFileEntry(
                name,
                isDir,
                shortName ?? "",
                isDir ? 0 : info.Length,
                File.GetLastWriteTime(entry),
                File.GetAttributes(entry),
                GetFileOwner(entry));
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

    // ── Owner ────────────────────────────────────────────────────────────────

    private string GetFileOwner(string fullPath) => getOwner?.Invoke(fullPath) ?? "";

    // ── 8.3 short-name generation ─────────────────────────────────────────────
    //
    // Format:  {stem[0..2].ToUpper()}{((short)fullname.GetHashCode()):X4}~{n}.{ext[0..3].ToUpper()}
    // Collision (~2, ~3, …): files sorted by creation date within the same directory.
    // Results are cached for the lifetime of the adapter so names stay stable.

    private readonly Dictionary<string, string> _shortNameCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _processedDirs = new(StringComparer.OrdinalIgnoreCase);

    private void EnsureShortNamesForDirectory(string nativeDir)
    {
        if (!_processedDirs.Add(nativeDir)) return;

        var candidates = Directory.EnumerateFileSystemEntries(nativeDir)
            .Where(e => NeedsShortName(Path.GetFileName(e)))
            .Select(e => (Path: e, Timestamp: GetTimestampSafe(e)))
            .OrderBy(x => x.Timestamp)
            .Select(x => x.Path)
            .ToList();

        var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in candidates)
        {
            var name = Path.GetFileName(entry);
            var base8 = ShortNameBase(name);
            counters.TryGetValue(base8, out var n);
            n++;
            counters[base8] = n;
            var ext = ShortNameExt(name);
            _shortNameCache[entry] = ext.Length > 0 ? $"{base8}~{n}.{ext}" : $"{base8}~{n}";
        }
    }

    private static DateTime GetTimestampSafe(string path)
    {
        try
        {
            // On Unix, GetLastWriteTime is more reliable than GetCreationTime
            return File.GetLastWriteTime(path);
        }
        catch (UnauthorizedAccessException)
        {
            return DateTime.MinValue;
        }
        catch (IOException)
        {
            return DateTime.MinValue;
        }
    }

    private static bool NeedsShortName(string name)
    {
        var dot = name.LastIndexOf('.');
        var stem = dot < 0 ? name : name[..dot];
        var ext = dot < 0 ? "" : name[(dot + 1)..];
        return stem.Length > 8 || ext.Length > 3;
    }

    private static string ShortNameBase(string name)
    {
        var dot = name.LastIndexOf('.');
        var stem = (dot < 0 ? name : name[..dot]).ToUpperInvariant();
        var prefix = stem.Length >= 2 ? stem[..2] : stem;
        return prefix + ((short)name.GetHashCode()).ToString("X4");
    }

    private static string ShortNameExt(string name)
    {
        var dot = name.LastIndexOf('.');
        if (dot < 0) return "";
        var ext = name[(dot + 1)..].ToUpperInvariant();
        return ext.Length > 3 ? ext[..3] : ext;
    }
}
