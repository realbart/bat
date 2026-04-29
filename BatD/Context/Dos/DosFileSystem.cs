using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Context;

namespace Bat.Context.Dos;

public partial class DosFileSystem(Dictionary<char, string> roots) : FileSystem
{
    private readonly Dictionary<char, string> _roots = new(roots);

    public DosFileSystem() : this(new() { ['Z'] = @"C:\" }) { }

    protected override string GetNativePathCore(char drive, string[] path)
    {
        if (!_roots.TryGetValue(drive, out var root))
            root = $@"{drive}:\does-not-exist";
        return path.Length == 0 ? root : Path.Combine([root, .. path]);
    }

    public bool HasDrive(char drive) => _roots.ContainsKey(char.ToUpperInvariant(drive));

    public void AddRoot(char drive, string nativePath) => _roots[char.ToUpperInvariant(drive)] = nativePath;

    public char FirstDrive() => _roots.Keys.First();

    /// <summary>
    /// Returns drive mappings in insertion order for CWD resolution.
    /// </summary>
    public IEnumerable<KeyValuePair<char, string>> GetRoots() => _roots;

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

    protected override bool TryGetNativePathCore(char drive, string[] path, out string nativePath)
    {
        if (!_roots.TryGetValue(drive, out var root))
        {
            nativePath = "";
            return false;
        }
        nativePath = path.Length == 0 ? root : Path.Combine([root, .. path]);
        return true;
    }

    protected override uint GetVolumeSerialNumber(string nativeRoot)
    {
        var hash = GetVolumeInformationW(nativeRoot, null, 0, out var serial, out _, out _, null, 0)
            ? serial : 0;

        return nativeRoot.Length > 3
            ? (uint)HashCode.Combine(hash, nativeRoot[3..])
            : hash;
    }

    protected override string GetVolumeLabel(string nativeRoot)
    {
        var buffer = new char[261];
        return GetVolumeInformationW(nativeRoot, buffer, buffer.Length, out _, out _, out _, null, 0)
            ? new string(buffer).TrimEnd('\0') : "";
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

    // ── Async implementations ──────────────────────────────────────────────────

    protected override Task<bool> FileExistsAsync(char drive, string[] path, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(GetNativePath(new BatPath(drive, path))));

    protected override Task<bool> DirectoryExistsAsync(char drive, string[] path, CancellationToken cancellationToken = default) =>
        Task.FromResult(Directory.Exists(GetNativePath(new BatPath(drive, path))));

    protected override Task CreateDirectoryAsync(char drive, string[] path, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(GetNativePath(new BatPath(drive, path)));
        return Task.CompletedTask;
    }

    protected override Task DeleteDirectoryAsync(char drive, string[] path, bool recursive, CancellationToken cancellationToken = default)
    {
        Directory.Delete(GetNativePath(new BatPath(drive, path)), recursive);
        return Task.CompletedTask;
    }

    protected override async IAsyncEnumerable<DosFileEntry> EnumerateEntriesAsync(
        char drive, string[] path, string pattern,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var searchPath = Path.Combine(GetNativePath(new BatPath(drive, path)), pattern);
        var dirPath = GetNativePath(new BatPath(drive, path));
        var handle = FindFirstFileW(searchPath, out var data);

        if (handle == new IntPtr(-1))
            yield break;

        try
        {
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var isDir = (data.dwFileAttributes & 0x10) != 0;
                var size = ((long)data.nFileSizeHigh << 32) | data.nFileSizeLow;
                var lastWrite = FileTimeToDateTime(data.ftLastWriteTime);
                var fullPath = Path.Combine(dirPath, data.cFileName);
                var owner = (data.cFileName == "." || data.cFileName == "..") ? "" : GetFileOwner(fullPath);

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
        catch
        {
            return "";
        }
    }

    protected override Task DeleteFileAsync(char drive, string[] path, CancellationToken cancellationToken = default)
    {
        File.Delete(GetNativePath(new BatPath(drive, path)));
        return Task.CompletedTask;
    }

    protected override Task CopyFileAsync(char srcDrive, string[] srcPath, char dstDrive, string[] dstPath, bool overwrite, CancellationToken cancellationToken = default)
    {
        File.Copy(GetNativePath(new BatPath(srcDrive, srcPath)), GetNativePath(new BatPath(dstDrive, dstPath)), overwrite);
        return Task.CompletedTask;
    }

    protected override Task MoveFileAsync(char srcDrive, string[] srcPath, char dstDrive, string[] dstPath, CancellationToken cancellationToken = default)
    {
        File.Move(GetNativePath(new BatPath(srcDrive, srcPath)), GetNativePath(new BatPath(dstDrive, dstPath)));
        return Task.CompletedTask;
    }

    protected override Task RenameFileAsync(char drive, string[] path, string newName, CancellationToken cancellationToken = default)
    {
        var src = GetNativePath(new BatPath(drive, path));
        var dst = Path.Combine(Path.GetDirectoryName(src)!, newName);
        File.Move(src, dst);
        return Task.CompletedTask;
    }

    protected override Task<Stream> OpenReadAsync(char drive, string[] path, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(File.OpenRead(GetNativePath(new BatPath(drive, path))));

    protected override Task<Stream> OpenWriteAsync(char drive, string[] path, bool append, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(append
            ? new FileStream(GetNativePath(new BatPath(drive, path)), FileMode.Append, FileAccess.Write)
            : File.OpenWrite(GetNativePath(new BatPath(drive, path))));

    protected override async Task<string> ReadAllTextAsync(char drive, string[] path, CancellationToken cancellationToken = default) =>
        await File.ReadAllTextAsync(GetNativePath(new BatPath(drive, path)), cancellationToken);

    protected override async Task WriteAllTextAsync(char drive, string[] path, string content, CancellationToken cancellationToken = default) =>
        await File.WriteAllTextAsync(GetNativePath(new BatPath(drive, path)), content, cancellationToken);

    protected override Task<FileAttributes> GetAttributesAsync(char drive, string[] path, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.GetAttributes(GetNativePath(new BatPath(drive, path))));

    protected override Task SetAttributesAsync(char drive, string[] path, FileAttributes attributes, CancellationToken cancellationToken = default)
    {
        File.SetAttributes(GetNativePath(new BatPath(drive, path)), attributes);
        return Task.CompletedTask;
    }

    protected override Task<long> GetFileSizeAsync(char drive, string[] path, CancellationToken cancellationToken = default) =>
        Task.FromResult(new FileInfo(GetNativePath(new BatPath(drive, path))).Length);

    protected override Task<DateTime> GetLastWriteTimeAsync(char drive, string[] path, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.GetLastWriteTime(GetNativePath(new BatPath(drive, path))));

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

