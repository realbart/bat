using System.Runtime.InteropServices;
using Context;

namespace Bat.Context;

internal partial class DosFileSystem : FileSystem
{
    private readonly Dictionary<char, string> _roots;

    public DosFileSystem(Dictionary<char, string> roots)
    {
        _roots = new Dictionary<char, string>(roots);
    }

    public DosFileSystem() : this(new Dictionary<char, string> { ['Z'] = @"C:\" }) { }

    public override string GetNativePath(char drive, string[] path)
    {
        var upper = char.ToUpperInvariant(drive);
        if (!_roots.TryGetValue(upper, out var root))
            throw new DriveNotFoundException($"Drive {upper}: is not mapped.");
        return path.Length == 0 ? root : Path.Combine([root, .. path]);
    }

    public bool HasDrive(char drive) => _roots.ContainsKey(char.ToUpperInvariant(drive));

    protected override uint GetVolumeSerialNumber(string nativeRoot)
    {
        var hash = GetVolumeInformationW(nativeRoot, null, 0, out uint serial, out _, out _, null, 0)
            ? serial : 0;

        return nativeRoot.Length > 3
            ? (uint)HashCode.Combine(hash, nativeRoot[3..])
            : hash;
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

    public override bool FileExists(char drive, string[] path) =>
        File.Exists(GetNativePath(drive, path));

    public override bool DirectoryExists(char drive, string[] path) =>
        Directory.Exists(GetNativePath(drive, path));

    public override void CreateDirectory(char drive, string[] path) =>
        Directory.CreateDirectory(GetNativePath(drive, path));

    public override void DeleteDirectory(char drive, string[] path, bool recursive) =>
        Directory.Delete(GetNativePath(drive, path), recursive);

    public override IEnumerable<DosFileEntry> EnumerateEntries(
        char drive, string[] path, string pattern)
    {
        if (!OperatingSystem.IsWindows())
        {
            var native = GetNativePath(drive, path);
            if (!Directory.Exists(native)) yield break;
            foreach (var entry in Directory.EnumerateFileSystemEntries(native, pattern))
            {
                var name = Path.GetFileName(entry);
                bool isDir = Directory.Exists(entry);
                var info = new FileInfo(entry);
                yield return new DosFileEntry(
                    name,
                    isDir,
                    "",
                    isDir ? 0 : info.Length,
                    File.GetLastWriteTime(entry),
                    File.GetAttributes(entry));
            }
            yield break;
        }

        string searchPath = Path.Combine(GetNativePath(drive, path), pattern);
        IntPtr handle = FindFirstFileW(searchPath, out var data);

        if (handle == new IntPtr(-1))
            yield break;

        try
        {
            do
            {
                if (data.cFileName is "." or "..")
                    continue;

                bool isDir = (data.dwFileAttributes & 0x10) != 0;
                long size = ((long)data.nFileSizeHigh << 32) | data.nFileSizeLow;
                DateTime lastWrite = FileTimeToDateTime(data.ftLastWriteTime);

                yield return new DosFileEntry(
                    data.cFileName,
                    isDir,
                    data.cAlternateFileName ?? "",
                    size,
                    lastWrite,
                    (FileAttributes)data.dwFileAttributes);
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
        long fileTime = ((long)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
        if (fileTime == 0) return DateTime.MinValue;
        return DateTime.FromFileTimeUtc(fileTime).ToLocalTime();
    }

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

    public override FileAttributes GetAttributes(char drive, string[] path) =>
        File.GetAttributes(GetNativePath(drive, path));

    public override void SetAttributes(char drive, string[] path, FileAttributes attributes) =>
        File.SetAttributes(GetNativePath(drive, path), attributes);

    public override long GetFileSize(char drive, string[] path) =>
        new FileInfo(GetNativePath(drive, path)).Length;

    public override DateTime GetLastWriteTime(char drive, string[] path) =>
        File.GetLastWriteTime(GetNativePath(drive, path));

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
