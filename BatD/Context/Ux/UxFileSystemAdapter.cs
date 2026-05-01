// todo: only compile in ux builds

using System.Runtime.CompilerServices;
using Bat.Context;
using BatD.Context;
using global::Context;

namespace BatD.Context.Ux;

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

    public override char NativeDirectorySeparator => '/';
    public override char NativePathSeparator => ':';

    // todo: cache resolved (partial) paths for performance, with invalidation on file system changes (FileSystemWatcher)
    // todo: actually look if the path segment exitst, to fix he casing.
    // only used the BatPart segment casing is the partial path does nog exit.
    private string ResolveNativePath(BatPath path)
    {
        var drive = char.ToUpperInvariant(path.Drive);
        var segments = path.Segments;
        var depth = 0;
        while (depth++ < 16 && Substs.TryGetValue(drive, out var subst))
            (drive, segments) = (subst.Drive, [.. subst.Segments, .. segments]);

        mappings.TryGetValue(drive, out var root);
        root ??= $"/{char.ToLowerInvariant(drive)}:";
        if (segments.Length == 0) return root;
        return root.TrimEnd('/') + '/' + string.Join('/', segments);
    }

    public override Task<HostPath> GetNativePathAsync(BatPath path, CancellationToken cancellationToken = default) =>
        Task.FromResult(new HostPath(ResolveNativePath(path)));

    public override Task<BatPath> FromNativePathAsync(HostPath hostPath, CancellationToken cancellationToken = default)
    {
        var p = hostPath.Path;
        if (string.IsNullOrEmpty(p))
            throw new ArgumentException("Empty native path");

        if (p.StartsWith('/'))
        {
            var parts = p[1..].Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && parts[0].Length == 2 && parts[0][1] == ':' && char.IsAsciiLetter(parts[0][0]))
            {
                var drive = char.ToUpperInvariant(parts[0][0]);
                var segments = parts.Length > 1 ? parts[1..] : Array.Empty<string>();
                return Task.FromResult(new BatPath(drive, segments));
            }
            return Task.FromResult(new BatPath('Z', parts));
        }

        throw new ArgumentException($"Cannot convert Unix path to BatPath: {p}");
    }

    public override Task<(bool Success, HostPath Path)> TryGetNativePathAsync(BatPath path, CancellationToken cancellationToken = default)
    {
        var drive = char.ToUpperInvariant(path.Drive);
        if (!mappings.ContainsKey(drive) && !Substs.ContainsKey(drive))
            return Task.FromResult((false, default(HostPath)));
        return Task.FromResult((true, new HostPath(ResolveNativePath(path))));
    }

    public override Task<bool> IsExecutableAsync(BatPath path, CancellationToken cancellationToken = default)
    {
#pragma warning disable CA1416
        var native = ResolveCaseInsensitive(ResolveNativePath(path));
        if (!File.Exists(native)) return Task.FromResult(false);
        var mode = File.GetUnixFileMode(native);
        return Task.FromResult((mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0);
#pragma warning restore CA1416
    }

    public override Task<bool> FileExistsAsync(BatPath path, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(ResolveCaseInsensitive(ResolveNativePath(path))));

    public override Task<bool> DirectoryExistsAsync(BatPath path, CancellationToken cancellationToken = default) =>
        Task.FromResult(Directory.Exists(ResolveCaseInsensitive(ResolveNativePath(path))));

    public override Task CreateDirectoryAsync(BatPath path, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ResolveNativePath(path));
        return Task.CompletedTask;
    }

    public override Task DeleteDirectoryAsync(BatPath path, bool recursive, CancellationToken cancellationToken = default)
    {
        Directory.Delete(ResolveCaseInsensitive(ResolveNativePath(path)), recursive);
        return Task.CompletedTask;
    }

    public override Task DeleteFileAsync(BatPath path, CancellationToken cancellationToken = default)
    {
        File.Delete(ResolveCaseInsensitive(ResolveNativePath(path)));
        return Task.CompletedTask;
    }

    public override async IAsyncEnumerable<DosFileEntry> EnumerateEntriesAsync(
        BatPath path, string pattern, bool includeDotEntries = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var native = ResolveCaseInsensitive(ResolveNativePath(path));
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

    public override Task<Stream> OpenReadAsync(BatPath path, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(File.OpenRead(ResolveCaseInsensitive(ResolveNativePath(path))));

    public override Task<Stream> OpenWriteAsync(BatPath path, bool append, CancellationToken cancellationToken = default)
    {
        var native = ResolveNativePath(path);
        return Task.FromResult<Stream>(append
            ? new FileStream(native, FileMode.Append, FileAccess.Write)
            : new FileStream(native, FileMode.Create, FileAccess.Write));
    }

    public override async Task<string> ReadAllTextAsync(BatPath path, CancellationToken cancellationToken = default) =>
        await File.ReadAllTextAsync(ResolveCaseInsensitive(ResolveNativePath(path)), cancellationToken);

    public override async Task WriteAllTextAsync(BatPath path, string content, CancellationToken cancellationToken = default) =>
        await File.WriteAllTextAsync(ResolveNativePath(path), content, cancellationToken);

    public override Task CopyFileAsync(BatPath source, BatPath dest, bool overwrite, CancellationToken cancellationToken = default)
    {
        File.Copy(ResolveCaseInsensitive(ResolveNativePath(source)), ResolveNativePath(dest), overwrite);
        return Task.CompletedTask;
    }

    public override Task MoveFileAsync(BatPath source, BatPath dest, CancellationToken cancellationToken = default)
    {
        File.Move(ResolveCaseInsensitive(ResolveNativePath(source)), ResolveNativePath(dest));
        return Task.CompletedTask;
    }

    public override Task RenameFileAsync(BatPath path, string newName, CancellationToken cancellationToken = default)
    {
        var src = ResolveCaseInsensitive(ResolveNativePath(path));
        var dst = Path.Combine(Path.GetDirectoryName(src)!, newName);
        File.Move(src, dst);
        return Task.CompletedTask;
    }

    public override Task<FileAttributes> GetAttributesAsync(BatPath path, CancellationToken cancellationToken = default)
    {
        var native = ResolveCaseInsensitive(ResolveNativePath(path));
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

    public override Task SetAttributesAsync(BatPath path, FileAttributes attributes, CancellationToken cancellationToken = default)
    {
        File.SetAttributes(ResolveCaseInsensitive(ResolveNativePath(path)), attributes);
        return Task.CompletedTask;
    }

    public override Task<long> GetFileSizeAsync(BatPath path, CancellationToken cancellationToken = default) =>
        Task.FromResult(new FileInfo(ResolveCaseInsensitive(ResolveNativePath(path))).Length);

    public override Task<DateTime> GetLastWriteTimeAsync(BatPath path, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.GetLastWriteTime(ResolveCaseInsensitive(ResolveNativePath(path))));

    public override Task<uint> GetVolumeSerialNumberAsync(char drive, CancellationToken cancellationToken = default) =>
        Task.FromResult(GetVolumeInfo(ResolveNativePath(new BatPath(char.ToUpperInvariant(drive), []))).SerialNumber);

    public override Task<string> GetVolumeLabelAsync(char drive, CancellationToken cancellationToken = default) =>
        Task.FromResult(GetVolumeInfo(ResolveNativePath(new BatPath(char.ToUpperInvariant(drive), []))).Label);

    public override Task<long> GetFreeBytesAsync(char drive, CancellationToken cancellationToken = default)
    {
        try
        {
            var driveInfo = new DriveInfo(ResolveNativePath(new BatPath(char.ToUpperInvariant(drive), [])));
            return Task.FromResult(driveInfo.AvailableFreeSpace);
        }
        catch { return Task.FromResult<long>(1024 * 1024 * 1024); }
    }

    public override Task<IReadOnlyDictionary<string, string>> GetFileAssociationsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(UnixAssociations);

    // ── Private helpers ─────────────────────────────────────────────────────────

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
            info = new() { Label = "", SerialNumber = GetStableHashCode(fullPath) };
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
