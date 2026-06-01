namespace Context;

/// <summary>
/// Synchronous extension members for IFileSystem that delegate to async members.
/// These exist for backwards compatibility with existing test code.
/// </summary>
public static class FileSystemExtensions
{
    public static bool FileExists(this IFileSystem fs, char drive, string[] path)
        => fs.FileExistsAsync(new BatPath(drive, path)).GetAwaiter().GetResult();

    public static bool DirectoryExists(this IFileSystem fs, char drive, string[] path)
        => fs.DirectoryExistsAsync(new BatPath(drive, path)).GetAwaiter().GetResult();

    public static bool IsExecutable(this IFileSystem fs, char drive, string[] path)
        => fs.IsExecutableAsync(new BatPath(drive, path)).GetAwaiter().GetResult();

    public static void CreateDirectory(this IFileSystem fs, char drive, string[] path)
        => fs.CreateDirectoryAsync(new BatPath(drive, path)).GetAwaiter().GetResult();

    public static void DeleteFile(this IFileSystem fs, char drive, string[] path)
        => fs.DeleteFileAsync(new BatPath(drive, path)).GetAwaiter().GetResult();

    public static void DeleteDirectory(this IFileSystem fs, char drive, string[] path, bool recursive)
        => fs.DeleteDirectoryAsync(new BatPath(drive, path), recursive).GetAwaiter().GetResult();

    public static IEnumerable<DosFileEntry> EnumerateEntries(this IFileSystem fs, char drive, string[] path, string pattern)
        => fs.EnumerateEntriesAsync(new BatPath(drive, path), pattern).ToBlockingEnumerable();

    public static Stream OpenRead(this IFileSystem fs, char drive, string[] path)
        => fs.OpenReadAsync(new BatPath(drive, path)).GetAwaiter().GetResult();

    public static Stream OpenWrite(this IFileSystem fs, char drive, string[] path, bool append)
        => fs.OpenWriteAsync(new BatPath(drive, path), append).GetAwaiter().GetResult();

    public static string ReadAllText(this IFileSystem fs, char drive, string[] path)
        => fs.ReadAllTextAsync(new BatPath(drive, path)).GetAwaiter().GetResult();

    public static void WriteAllText(this IFileSystem fs, char drive, string[] path, string content)
        => fs.WriteAllTextAsync(new BatPath(drive, path), content).GetAwaiter().GetResult();

    public static void CopyFile(this IFileSystem fs, char sourceDrive, string[] sourcePath, char destDrive, string[] destPath, bool overwrite)
        => fs.CopyFileAsync(new BatPath(sourceDrive, sourcePath), new BatPath(destDrive, destPath), overwrite).GetAwaiter().GetResult();

    public static void MoveFile(this IFileSystem fs, char sourceDrive, string[] sourcePath, char destDrive, string[] destPath)
        => fs.MoveFileAsync(new BatPath(sourceDrive, sourcePath), new BatPath(destDrive, destPath)).GetAwaiter().GetResult();

    public static void RenameFile(this IFileSystem fs, char drive, string[] path, string newName)
        => fs.RenameFileAsync(new BatPath(drive, path), newName).GetAwaiter().GetResult();

    public static FileAttributes GetAttributes(this IFileSystem fs, char drive, string[] path)
        => fs.GetAttributesAsync(new BatPath(drive, path)).GetAwaiter().GetResult();

    public static void SetAttributes(this IFileSystem fs, char drive, string[] path, FileAttributes attributes)
        => fs.SetAttributesAsync(new BatPath(drive, path), attributes).GetAwaiter().GetResult();

    public static long GetFileSize(this IFileSystem fs, char drive, string[] path)
        => fs.GetFileSizeAsync(new BatPath(drive, path)).GetAwaiter().GetResult();

    public static DateTime GetLastWriteTime(this IFileSystem fs, char drive, string[] path)
        => fs.GetLastWriteTimeAsync(new BatPath(drive, path)).GetAwaiter().GetResult();

    public static uint GetVolumeSerialNumber(this IFileSystem fs, char drive)
        => fs.GetVolumeSerialNumberAsync(drive).GetAwaiter().GetResult();

    public static string GetVolumeLabel(this IFileSystem fs, char drive)
        => fs.GetVolumeLabelAsync(drive).GetAwaiter().GetResult();

    public static long GetFreeBytes(this IFileSystem fs, char drive)
        => fs.GetFreeBytesAsync(drive).GetAwaiter().GetResult();

    public static IReadOnlyDictionary<string, string> GetFileAssociations(this IFileSystem fs)
        => fs.GetFileAssociationsAsync().GetAwaiter().GetResult();
}

