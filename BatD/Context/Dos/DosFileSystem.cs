// todo: only compile in windos builds
#if WINDOWS
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using BatD.Context;
using global::Context;

namespace BatD.Context.Dos;

public partial class DosFileSystem(Dictionary<char, string> roots) : FileSystem
{
    private readonly Dictionary<char, string> _roots = new(roots);

    // todo:
    // determine the host drive(s) dynamicly
    // - Always map the root of the drive %COMSPEC% is in
    // - Always map the root of the current working directory
    // - Always map %HOMEDRIVE%
    // - Always map the folder the batd executable is in
    public DosFileSystem() : this(new() { ['Z'] = @"C:\" }) { }

    public bool HasDrive(char drive) => _roots.ContainsKey(char.ToUpperInvariant(drive));
    public void AddRoot(char drive, string nativePath) => _roots[char.ToUpperInvariant(drive)] = nativePath;
    public char FirstDrive() => _roots.Keys.First();
    public IEnumerable<KeyValuePair<char, string>> GetRoots() => _roots;

    private string ResolveNativePath(BatPath path)
    {
        var drive = char.ToUpperInvariant(path.Drive);
        var segments = path.Segments;
        var depth = 0;
        while (depth++ < 16 && Substs.TryGetValue(drive, out var subst))
            (drive, segments) = (subst.Drive, [.. subst.Segments, .. segments]);

        if (!_roots.TryGetValue(drive, out var root))
            root = $@"{drive}:\does-not-exist";
        return segments.Length == 0 ? root : Path.Combine([root, .. segments]);
    }

    public override Task<HostPath> GetNativePathAsync(BatPath path, CancellationToken cancellationToken = default) =>
        Task.FromResult(new HostPath(ResolveNativePath(path)));

    public override Task<BatPath> FromNativePathAsync(HostPath hostPath, CancellationToken cancellationToken = default)
    {
        var p = hostPath.Path;
        if (string.IsNullOrEmpty(p) || p.Length < 2 || p[1] != ':')
            throw new ArgumentException($"Invalid Windows path: {p}");
        var drive = char.ToUpperInvariant(p[0]);
        var rest = p.Length > 3 && p[2] == '\\' ? p[3..] : "";
        var segments = string.IsNullOrEmpty(rest)
            ? Array.Empty<string>()
            : rest.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return Task.FromResult(new BatPath(drive, segments));
    }

    public override Task<(bool Success, HostPath Path)> TryGetNativePathAsync(BatPath path, CancellationToken cancellationToken = default)
    {
        var drive = char.ToUpperInvariant(path.Drive);
        var depth = 0;
        while (depth++ < 16 && Substs.TryGetValue(drive, out var subst))
            drive = char.ToUpperInvariant(subst.Drive);

        if (!_roots.ContainsKey(drive))
            return Task.FromResult((false, default(HostPath)));

        return Task.FromResult((true, new HostPath(ResolveNativePath(path))));
    }

    public override Task<bool> FileExistsAsync(BatPath path, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(ResolveNativePath(path)));

    public override Task<bool> DirectoryExistsAsync(BatPath path, CancellationToken cancellationToken = default) =>
        Task.FromResult(Directory.Exists(ResolveNativePath(path)));

    public override Task CreateDirectoryAsync(BatPath path, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ResolveNativePath(path));
        return Task.CompletedTask;
    }

    public override Task DeleteFileAsync(BatPath path, CancellationToken cancellationToken = default)
    {
        File.Delete(ResolveNativePath(path));
        return Task.CompletedTask;
    }

    public override Task DeleteDirectoryAsync(BatPath path, bool recursive, CancellationToken cancellationToken = default)
    {
        Directory.Delete(ResolveNativePath(path), recursive);
        return Task.CompletedTask;
    }

    public override async IAsyncEnumerable<DosFileEntry> EnumerateEntriesAsync(
        BatPath path, string pattern, bool includeDotEntries = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var dirPath = ResolveNativePath(path);
        var searchPath = Path.Combine(dirPath, pattern);
        var handle = FindFirstFileW(searchPath, out var data);

        if (handle == new IntPtr(-1))
            yield break;

        try
        {
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!includeDotEntries && data.cFileName is "." or "..") continue;
                var isDir = (data.dwFileAttributes & 0x10) != 0;
                var size = ((long)data.nFileSizeHigh << 32) | data.nFileSizeLow;
                var lastWrite = FileTimeToDateTime(data.ftLastWriteTime);
                var fullPath = Path.Combine(dirPath, data.cFileName);
                var owner = (data.cFileName is "." or "..") ? "" : GetFileOwner(fullPath);

                yield return new(
                    data.cFileName,
                    isDir,
                    data.cAlternateFileName ?? "",
                    size,
                    lastWrite,
                    (FileAttributes)data.dwFileAttributes,
                    owner);
            }
            while (FindNextFileW(handle, out data));
        }
        finally
        {
            FindClose(handle);
        }
    }

    public override Task<Stream> OpenReadAsync(BatPath path, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(File.OpenRead(ResolveNativePath(path)));

    public override Task<Stream> OpenWriteAsync(BatPath path, bool append, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(append
            ? new FileStream(ResolveNativePath(path), FileMode.Append, FileAccess.Write)
            : new FileStream(ResolveNativePath(path), FileMode.Create, FileAccess.Write));

    public override async Task<string> ReadAllTextAsync(BatPath path, CancellationToken cancellationToken = default) =>
        await File.ReadAllTextAsync(ResolveNativePath(path), cancellationToken);

    public override async Task WriteAllTextAsync(BatPath path, string content, CancellationToken cancellationToken = default) =>
        await File.WriteAllTextAsync(ResolveNativePath(path), content, cancellationToken);

    public override Task CopyFileAsync(BatPath source, BatPath dest, bool overwrite, CancellationToken cancellationToken = default)
    {
        File.Copy(ResolveNativePath(source), ResolveNativePath(dest), overwrite);
        return Task.CompletedTask;
    }

    public override Task MoveFileAsync(BatPath source, BatPath dest, CancellationToken cancellationToken = default)
    {
        File.Move(ResolveNativePath(source), ResolveNativePath(dest));
        return Task.CompletedTask;
    }

    public override Task RenameFileAsync(BatPath path, string newName, CancellationToken cancellationToken = default)
    {
        var src = ResolveNativePath(path);
        var dst = Path.Combine(Path.GetDirectoryName(src)!, newName);
        File.Move(src, dst);
        return Task.CompletedTask;
    }

    public override Task<FileAttributes> GetAttributesAsync(BatPath path, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.GetAttributes(ResolveNativePath(path)));

    public override Task SetAttributesAsync(BatPath path, FileAttributes attributes, CancellationToken cancellationToken = default)
    {
        File.SetAttributes(ResolveNativePath(path), attributes);
        return Task.CompletedTask;
    }

    public override Task<long> GetFileSizeAsync(BatPath path, CancellationToken cancellationToken = default) =>
        Task.FromResult(new FileInfo(ResolveNativePath(path)).Length);

    public override Task<DateTime> GetLastWriteTimeAsync(BatPath path, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.GetLastWriteTime(ResolveNativePath(path)));

    public override Task<uint> GetVolumeSerialNumberAsync(char drive, CancellationToken cancellationToken = default)
    {
        var root = ResolveNativePath(new BatPath(char.ToUpperInvariant(drive), []));
        var volumeName = new char[256];
        GetVolumeInformationW(root, volumeName, volumeName.Length,
            out var serial, out _, out _, null, 0);
        return Task.FromResult(serial);
    }

    public override Task<string> GetVolumeLabelAsync(char drive, CancellationToken cancellationToken = default)
    {
        var root = ResolveNativePath(new BatPath(char.ToUpperInvariant(drive), []));
        var volumeName = new char[256];
        GetVolumeInformationW(root, volumeName, volumeName.Length,
            out _, out _, out _, null, 0);
        return Task.FromResult(new string(volumeName).TrimEnd('\0'));
    }

    public override Task<long> GetFreeBytesAsync(char drive, CancellationToken cancellationToken = default)
    {
        var root = ResolveNativePath(new BatPath(char.ToUpperInvariant(drive), []));
        var driveInfo = new DriveInfo(root);
        return Task.FromResult(driveInfo.AvailableFreeSpace);
    }

    public override Task<IReadOnlyDictionary<string, string>> GetFileAssociationsAsync(CancellationToken cancellationToken = default)
    {
        var assoc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var classesRoot = Microsoft.Win32.Registry.ClassesRoot;
            foreach (var keyName in classesRoot.GetSubKeyNames())
            {
                if (!keyName.StartsWith('.')) continue;
                using var extKey = classesRoot.OpenSubKey(keyName);
                var type = extKey?.GetValue("")?.ToString();
                if (!string.IsNullOrEmpty(type))
                    assoc[keyName] = type;
            }
        }
        catch { }
        return Task.FromResult<IReadOnlyDictionary<string, string>>(assoc);
    }

    // ── Private helpers ─────────────────────────────────────────────────────────

    private static DateTime FileTimeToDateTime(System.Runtime.InteropServices.ComTypes.FILETIME ft)
    {
        var fileTime = ((long)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
        if (fileTime == 0) return DateTime.MinValue;
        return DateTime.FromFileTime(fileTime);
    }

    private static string GetFileOwner(string fullPath)
    {
        try
        {
            var fileInfo = new FileInfo(fullPath);
            var fileSecurity = fileInfo.GetAccessControl();
            var owner = fileSecurity.GetOwner(typeof(NTAccount));
            return owner?.Value ?? "";
        }
        catch { return ""; }
    }

    // ── P/Invoke ────────────────────────────────────────────────────────────────

    [LibraryImport("kernel32.dll", EntryPoint = "GetVolumeInformationW", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetVolumeInformationW(
        string lpRootPathName,
        char[]? lpVolumeNameBuffer,
        int nVolumeNameSize,
        out uint lpVolumeSerialNumber,
        out uint lpMaximumComponentLength,
        out uint lpFileSystemFlags,
        char[]? lpFileSystemNameBuffer,
        int nFileSystemNameSize);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WIN32_FIND_DATA
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindFirstFileW(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool FindNextFileW(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FindClose(IntPtr hFindFile);
}
#endif
