using System.Runtime.CompilerServices;
using Context;

namespace Bat.Context.Ux;

public class UxFileSystemAdapter(Dictionary<char, string> mappings, Func<string, string>? getOwner = null) : FileSystem
{
    private static readonly Dictionary<string, string> UnixAssociations = new(StringComparer.OrdinalIgnoreCase)
    {
        [".bat"] = "batfile",
        [".cmd"] = "cmdfile",
        [".com"] = "comfile",
        [".dll"] = "dllfile",
        [".exe"] = "exefile"
    };

    public UxFileSystemAdapter() : this(new() { ['Z'] = "/" }, UnixFileOwner.GetOwner) { }

    public bool HasDrive(char drive) => mappings.ContainsKey(char.ToUpperInvariant(drive));

    public void AddRoot(char drive, string nativePath) => mappings[char.ToUpperInvariant(drive)] = nativePath;

    public IEnumerable<KeyValuePair<char, string>> GetRoots() => mappings;

    public override Task<IReadOnlyDictionary<string, string>> GetFileAssociationsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<string, string>>(UnixAssociations);

    public override char NativeDirectorySeparator => '/';
    public override char NativePathSeparator => ':';

    protected override Task<bool> IsExecutableAsync(char drive, string[] path, CancellationToken cancellationToken = default)
    {
#pragma warning disable CA1416 // File.GetUnixFileMode is supported on Unix
        var native = ResolveCaseInsensitive(GetNativePath(new BatPath(drive, path)));
        if (!File.Exists(native)) return Task.FromResult(false);
        var mode = File.GetUnixFileMode(native);
        return Task.FromResult((mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0);
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

    // ── Async implementations ──────────────────────────────────────────────────

    protected override Task<bool> FileExistsAsync(char drive, string[] path, CancellationToken cancellationToken = default)
    {
        var native = ResolveCaseInsensitive(GetNativePath(new BatPath(drive, path)));
        return Task.FromResult(File.Exists(native));
    }

    protected override Task<bool> DirectoryExistsAsync(char drive, string[] path, CancellationToken cancellationToken = default)
    {
        var native = ResolveCaseInsensitive(GetNativePath(new BatPath(drive, path)));
        return Task.FromResult(Directory.Exists(native));
    }

    protected override Task CreateDirectoryAsync(char drive, string[] path, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(GetNativePath(new BatPath(drive, path)));
        return Task.CompletedTask;
    }

    protected override Task DeleteDirectoryAsync(char drive, string[] path, bool recursive, CancellationToken cancellationToken = default)
    {
        Directory.Delete(ResolveCaseInsensitive(GetNativePath(new BatPath(drive, path))), recursive);
        return Task.CompletedTask;
    }

    protected override Task DeleteFileAsync(char drive, string[] path, CancellationToken cancellationToken = default)
    {
        File.Delete(ResolveCaseInsensitive(GetNativePath(new BatPath(drive, path))));
        return Task.CompletedTask;
    }

    protected override async IAsyncEnumerable<DosFileEntry> EnumerateEntriesAsync(
        char drive, string[] path, string pattern,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var native = ResolveCaseInsensitive(GetNativePath(new BatPath(drive, path)));
        if (!Directory.Exists(native)) yield break;

        EnsureShortNamesForDirectory(native);

        foreach (var entry in Directory.EnumerateFileSystemEntries(native, pattern))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(entry);
            var linfo = new FileInfo(entry);
            var attributes = linfo.Attributes;

            if (linfo.LinkTarget != null)
            {
                attributes |= FileAttributes.ReparsePoint;
            }
            else
            {
                try
                {
                    var linkAttributes = File.GetAttributes(entry);
                    if (linkAttributes.HasFlag(FileAttributes.ReparsePoint))
                        attributes |= FileAttributes.ReparsePoint;
                }
                catch { }
            }

            var isDir = (attributes & FileAttributes.Directory) != 0;
            var isLink = (attributes & FileAttributes.ReparsePoint) != 0;

            if (!isLink && isDir)
            {
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

            if (isDir && !isLink && IsMountPoint(entry))
            {
                attributes |= FileAttributes.ReparsePoint;
                attributes |= FileAttributes.Offline;
            }

            _shortNameCache.TryGetValue(entry, out var shortName);
            yield return new(
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

    protected override Task<Stream> OpenReadAsync(char drive, string[] path, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(File.OpenRead(ResolveCaseInsensitive(GetNativePath(new BatPath(drive, path)))));

    protected override Task<Stream> OpenWriteAsync(char drive, string[] path, bool append, CancellationToken cancellationToken = default)
    {
        var native = GetNativePath(new BatPath(drive, path));
        return Task.FromResult<Stream>(append
            ? new FileStream(native, FileMode.Append, FileAccess.Write)
            : File.OpenWrite(native));
    }

    protected override async Task<string> ReadAllTextAsync(char drive, string[] path, CancellationToken cancellationToken = default) =>
        await File.ReadAllTextAsync(ResolveCaseInsensitive(GetNativePath(new BatPath(drive, path))), cancellationToken);

    protected override async Task WriteAllTextAsync(char drive, string[] path, string content, CancellationToken cancellationToken = default) =>
        await File.WriteAllTextAsync(GetNativePath(new BatPath(drive, path)), content, cancellationToken);

    protected override Task CopyFileAsync(char srcDrive, string[] srcPath, char dstDrive, string[] dstPath, bool overwrite, CancellationToken cancellationToken = default)
    {
        File.Copy(ResolveCaseInsensitive(GetNativePath(new BatPath(srcDrive, srcPath))), GetNativePath(new BatPath(dstDrive, dstPath)), overwrite);
        return Task.CompletedTask;
    }

    protected override Task MoveFileAsync(char srcDrive, string[] srcPath, char dstDrive, string[] dstPath, CancellationToken cancellationToken = default)
    {
        File.Move(ResolveCaseInsensitive(GetNativePath(new BatPath(srcDrive, srcPath))), GetNativePath(new BatPath(dstDrive, dstPath)));
        return Task.CompletedTask;
    }

    protected override Task RenameFileAsync(char drive, string[] path, string newName, CancellationToken cancellationToken = default)
    {
        var src = ResolveCaseInsensitive(GetNativePath(new BatPath(drive, path)));
        var dst = Path.Combine(Path.GetDirectoryName(src)!, newName);
        File.Move(src, dst);
        return Task.CompletedTask;
    }

    protected override Task<FileAttributes> GetAttributesAsync(char drive, string[] path, CancellationToken cancellationToken = default)
    {
        var native = ResolveCaseInsensitive(GetNativePath(new BatPath(drive, path)));
        var linfo = new FileInfo(native);
        var attributes = linfo.Attributes;

        if (linfo.LinkTarget != null)
            attributes |= FileAttributes.ReparsePoint;

        if ((attributes & FileAttributes.Directory) != 0 && (attributes & FileAttributes.ReparsePoint) == 0 && IsMountPoint(native))
        {
            attributes |= FileAttributes.ReparsePoint;
            attributes |= FileAttributes.Offline;
        }
        return Task.FromResult(attributes);
    }

    protected override Task SetAttributesAsync(char drive, string[] path, FileAttributes attributes, CancellationToken cancellationToken = default)
    {
        File.SetAttributes(ResolveCaseInsensitive(GetNativePath(new BatPath(drive, path))), attributes);
        return Task.CompletedTask;
    }

    protected override Task<long> GetFileSizeAsync(char drive, string[] path, CancellationToken cancellationToken = default) =>
        Task.FromResult(new FileInfo(ResolveCaseInsensitive(GetNativePath(new BatPath(drive, path)))).Length);

    protected override Task<DateTime> GetLastWriteTimeAsync(char drive, string[] path, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.GetLastWriteTime(ResolveCaseInsensitive(GetNativePath(new BatPath(drive, path)))));

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
        catch { }

        VolumeInfo info;
        if (device != null)
        {
            var uuid = GetUuidForDevice(device);
            var stableId = uuid ?? device;
            info = new()
            {
                Label = uuid != null ? $"UUID_{uuid[..8].ToUpperInvariant()}" : "UNIX_DISK",
                SerialNumber = GetStableHashCode(stableId)
            };
        }
        else
        {
            info = new()
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
                hash = (hash ^ c) * 16777619;
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
                    return Path.GetFileName(link);
            }
        }
        catch { }
        return null;
    }

    private static string? ReadSymbolicLink(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                return info.LinkTarget;
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
            return 1024 * 1024 * 1024;
        }
    }

    private string GetFileOwner(string fullPath) => getOwner?.Invoke(fullPath) ?? "";

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
        try { return File.GetLastWriteTime(path); }
        catch { return DateTime.MinValue; }
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

