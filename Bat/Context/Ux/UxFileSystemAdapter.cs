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

    /// <summary>
    /// Returns drive mappings in insertion order for CWD resolution.
    /// </summary>
    public IEnumerable<KeyValuePair<char, string>> GetRoots() => mappings;

    public override IReadOnlyDictionary<string, string> GetFileAssociations() => UnixAssociations;

    public override char NativeDirectorySeparator => '/';
    public override char NativePathSeparator => ':';

    public override bool IsExecutable(char drive, string[] path)
    {
#pragma warning disable CA1416 // File.GetUnixFileMode is supported on Unix
        var native = ResolveCaseInsensitive(GetNativePath(drive, path));
        if (!File.Exists(native)) return false;
        var mode = File.GetUnixFileMode(native);
        return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
#pragma warning restore CA1416
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
            var linfo = new FileInfo(entry);
            var attributes = linfo.Attributes;

            // On Linux, FileInfo.Attributes might not always have ReparsePoint set for symlinks 
            // depending on the runtime version or filesystem.
            // Check explicitly using LinkTarget or UnixFileMode to see if it's a symlink.
            if (linfo.LinkTarget != null)
            {
                attributes |= FileAttributes.ReparsePoint;
            }
            else
            {
                try
                {
                    // Use File.GetAttributes which is more likely to use the lstat system call correctly.
                    var linkAttributes = File.GetAttributes(entry);
                    if (linkAttributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        attributes |= FileAttributes.ReparsePoint;
                    }
                }
                catch { }
            }

            var isDir = (attributes & FileAttributes.Directory) != 0;
            var isLink = (attributes & FileAttributes.ReparsePoint) != 0;

            // Fallback: check if it's a symbolic link using lstat if possible or check if it's not a regular file/directory.
            if (!isLink)
            {
                // In some cases on Linux, directory symlinks might not have ReparsePoint.
                // We've already tried LinkTarget and File.GetAttributes.
                // If we still don't have it, but it's a directory, let's see if it's actually a symlink.
                if (isDir)
                {
                    // For directories, we can check if it's a symbolic link by using ResolveLinkTarget.
                    // This is more robust than just checking LinkTarget property on some systems.
                    try
                    {
                        var target = linfo.ResolveLinkTarget(false);
                        if (target != null)
                        {
                            attributes |= FileAttributes.ReparsePoint;
                            isLink = true;
                        }
                    }
                    catch { }
                }
            }
            
            // Check if it's a mount point (Junction)
            if (isDir && !isLink && IsMountPoint(entry))
            {
                attributes |= FileAttributes.ReparsePoint;
                attributes |= FileAttributes.Offline; // We use Offline as a marker for Junction/Mount
            }

            _shortNameCache.TryGetValue(entry, out var shortName);
            yield return new DosFileEntry(
                name,
                isDir,
                shortName ?? "",
                isDir ? 0 : linfo.Length,
                linfo.LastWriteTime,
                attributes,
                GetFileOwner(entry));
        }
    }

    private static bool IsMountPoint(string path)
    {
        var fullPath = Path.GetFullPath(path).TrimEnd('/');
        if (fullPath == "") fullPath = "/";
        
        try
        {
            if (File.Exists("/proc/mounts"))
            {
                var lines = File.ReadAllLines("/proc/mounts");
                foreach (var line in lines)
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var mountPoint = parts[1].TrimEnd('/');
                        if (mountPoint == "") mountPoint = "/";
                        if (fullPath == mountPoint) return true;
                    }
                }
            }
        }
        catch { }
        return false;
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

    public override FileAttributes GetAttributes(char drive, string[] path)
    {
        var native = ResolveCaseInsensitive(GetNativePath(drive, path));
        var linfo = new FileInfo(native);
        var attributes = linfo.Attributes;
        
        if (linfo.LinkTarget != null)
        {
            attributes |= FileAttributes.ReparsePoint;
        }

        if ((attributes & FileAttributes.Directory) != 0 && (attributes & FileAttributes.ReparsePoint) == 0 && IsMountPoint(native))
        {
            attributes |= FileAttributes.ReparsePoint;
            attributes |= FileAttributes.Offline;
        }
        return attributes;
    }

    public override void SetAttributes(char drive, string[] path, FileAttributes attributes) =>
        File.SetAttributes(ResolveCaseInsensitive(GetNativePath(drive, path)), attributes);

    public override long GetFileSize(char drive, string[] path) =>
        new FileInfo(ResolveCaseInsensitive(GetNativePath(drive, path))).Length;

    public override DateTime GetLastWriteTime(char drive, string[] path) =>
        File.GetLastWriteTime(ResolveCaseInsensitive(GetNativePath(drive, path)));

    protected override uint GetVolumeSerialNumber(string nativeRoot)
    {
        var info = GetVolumeInfo(nativeRoot);
        return info.SerialNumber;
    }

    protected override string GetVolumeLabel(string nativeRoot)
    {
        var info = GetVolumeInfo(nativeRoot);
        return info.Label;
    }

    private struct VolumeInfo
    {
        public string Label;
        public uint SerialNumber;
    }

    private readonly Dictionary<string, VolumeInfo> _volumeCache = new(StringComparer.Ordinal);

    private VolumeInfo GetVolumeInfo(string nativeRoot)
    {
        // We zoeken de langste match in /proc/mounts die een prefix is van nativeRoot
        var fullPath = Path.GetFullPath(nativeRoot);
        
        if (_volumeCache.TryGetValue(fullPath, out var cached)) return cached;

        string? bestMountPoint = null;
        string? device = null;

        try
        {
            if (File.Exists("/proc/mounts"))
            {
                var lines = File.ReadAllLines("/proc/mounts");
                foreach (var line in lines)
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;
                    var mountPoint = parts[1];
                    // Normaliseer mountPoint om trailing slash problemen te voorkomen, behalve voor root
                    var normalizedMount = mountPoint == "/" ? "/" : mountPoint.TrimEnd('/');
                    var normalizedPath = fullPath == "/" ? "/" : fullPath.TrimEnd('/');

                    if (normalizedPath == normalizedMount || normalizedPath.StartsWith(normalizedMount + "/", StringComparison.Ordinal))
                    {
                        if (bestMountPoint == null || mountPoint.Length > bestMountPoint.Length)
                        {
                            bestMountPoint = mountPoint;
                            device = parts[0];
                        }
                    }
                }
            }
        }
        catch
        {
            // Fallback naar hash van het pad
        }

        VolumeInfo info;
        if (device != null)
        {
            var uuid = GetUuidForDevice(device);
            var stableId = uuid ?? device;
            info = new VolumeInfo
            {
                Label = uuid != null ? $"UUID_{uuid[..8].ToUpperInvariant()}" : "UNIX_DISK",
                SerialNumber = GetStableHashCode(stableId)
            };
        }
        else
        {
            info = new VolumeInfo
            {
                Label = "",
                SerialNumber = GetStableHashCode(fullPath)
            };
        }

        _volumeCache[fullPath] = info;
        return info;
    }

    private static uint GetStableHashCode(string str)
    {
        unchecked
        {
            var hash = 2166136261;
            foreach (var c in str)
            {
                hash = (hash ^ c) * 16777619;
            }
            return hash;
        }
    }

    private static string? GetUuidForDevice(string device)
    {
        try
        {
            const string byUuidDir = "/dev/disk/by-uuid";
            if (!Directory.Exists(byUuidDir)) return null;

            var deviceName = Path.GetFileName(device);
            foreach (var link in Directory.EnumerateFileSystemEntries(byUuidDir))
            {
                var target = ReadSymbolicLink(link);
                if (target != null && Path.GetFileName(target) == deviceName)
                {
                    return Path.GetFileName(link);
                }
            }
        }
        catch
        {
            // Ignore
        }
        return null;
    }

    private static string? ReadSymbolicLink(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return info.LinkTarget;
            }
        }
        catch { }
        return null;
    }

    protected override long GetFreeBytes(string nativeRoot)
    {
        try
        {
            var drive = new DriveInfo(nativeRoot);
            return drive.AvailableFreeSpace;
        }
        catch
        {
            return 1024 * 1024 * 1024; // 1 GB fallback
        }
    }

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
