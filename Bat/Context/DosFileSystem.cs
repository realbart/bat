using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Context;

namespace Bat.Context;

internal partial class DosFileSystem(Dictionary<char, string> roots) : FileSystem
{
    private readonly Dictionary<char, string> _roots = new Dictionary<char, string>(roots);

    public DosFileSystem() : this(new Dictionary<char, string> { ['Z'] = @"C:\" }) { }

    protected override string GetNativePathCore(char drive, string[] path)
    {
        if (!_roots.TryGetValue(drive, out var root))
            root = $@"{drive}:\does-not-exist";
        return path.Length == 0 ? root : Path.Combine([root, .. path]);
    }

    public bool HasDrive(char drive) => _roots.ContainsKey(char.ToUpperInvariant(drive));

    public char FirstDrive() => _roots.Keys.First();

    public override IReadOnlyDictionary<string, string> GetFileAssociations()
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
        return assoc;
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
        var searchPath = Path.Combine(GetNativePath(drive, path), pattern);
        var dirPath = GetNativePath(drive, path);
        var handle = FindFirstFileW(searchPath, out var data);

        if (handle == new IntPtr(-1))
            yield break;

        try
        {
            do
            {
                if (data.cFileName != "..")
                {
                    var isDir = (data.dwFileAttributes & 0x10) != 0;
                    var size = ((long)data.nFileSizeHigh << 32) | data.nFileSizeLow;
                    var lastWrite = FileTimeToDateTime(data.ftLastWriteTime);
                    var fullPath = Path.Combine(dirPath, data.cFileName);
                    var owner = GetFileOwner(fullPath);

                    yield return new DosFileEntry(
                        data.cFileName,
                        isDir,
                        data.cAlternateFileName ?? "",
                        size,
                        lastWrite,
                        (FileAttributes)data.dwFileAttributes,
                        owner);
                }
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
