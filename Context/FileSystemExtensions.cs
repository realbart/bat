namespace Context;

/// <summary>
/// Synchronous extension methods for IFileSystem that delegate to async members.
/// These exist for backwards compatibility with existing code.
/// </summary>
public static class FileSystemExtensions
{
    public static bool FileExists(this IFileSystem fs, char drive, string[] path)
        => fs.FileExistsAsync(drive, path).GetAwaiter().GetResult();

    public static bool DirectoryExists(this IFileSystem fs, char drive, string[] path)
        => fs.DirectoryExistsAsync(drive, path).GetAwaiter().GetResult();

    public static bool IsExecutable(this IFileSystem fs, char drive, string[] path)
        => fs.IsExecutableAsync(drive, path).GetAwaiter().GetResult();

    public static void CreateDirectory(this IFileSystem fs, char drive, string[] path)
        => fs.CreateDirectoryAsync(drive, path).GetAwaiter().GetResult();

    public static void DeleteFile(this IFileSystem fs, char drive, string[] path)
        => fs.DeleteFileAsync(drive, path).GetAwaiter().GetResult();

    public static void DeleteDirectory(this IFileSystem fs, char drive, string[] path, bool recursive)
        => fs.DeleteDirectoryAsync(drive, path, recursive).GetAwaiter().GetResult();

    public static IEnumerable<DosFileEntry> EnumerateEntries(this IFileSystem fs, char drive, string[] path, string pattern)
        => fs.EnumerateEntriesAsync(drive, path, pattern).ToBlockingEnumerable();

    public static Stream OpenRead(this IFileSystem fs, char drive, string[] path)
        => fs.OpenReadAsync(drive, path).GetAwaiter().GetResult();

    public static Stream OpenWrite(this IFileSystem fs, char drive, string[] path, bool append)
        => fs.OpenWriteAsync(drive, path, append).GetAwaiter().GetResult();

    public static string ReadAllText(this IFileSystem fs, char drive, string[] path)
        => fs.ReadAllTextAsync(drive, path).GetAwaiter().GetResult();

    public static void WriteAllText(this IFileSystem fs, char drive, string[] path, string content)
        => fs.WriteAllTextAsync(drive, path, content).GetAwaiter().GetResult();

    public static void CopyFile(this IFileSystem fs, char sourceDrive, string[] sourcePath, char destDrive, string[] destPath, bool overwrite)
        => fs.CopyFileAsync(sourceDrive, sourcePath, destDrive, destPath, overwrite).GetAwaiter().GetResult();

    public static void MoveFile(this IFileSystem fs, char sourceDrive, string[] sourcePath, char destDrive, string[] destPath)
        => fs.MoveFileAsync(sourceDrive, sourcePath, destDrive, destPath).GetAwaiter().GetResult();

    public static void RenameFile(this IFileSystem fs, char drive, string[] path, string newName)
        => fs.RenameFileAsync(drive, path, newName).GetAwaiter().GetResult();

    public static FileAttributes GetAttributes(this IFileSystem fs, char drive, string[] path)
        => fs.GetAttributesAsync(drive, path).GetAwaiter().GetResult();

    public static void SetAttributes(this IFileSystem fs, char drive, string[] path, FileAttributes attributes)
        => fs.SetAttributesAsync(drive, path, attributes).GetAwaiter().GetResult();

    public static long GetFileSize(this IFileSystem fs, char drive, string[] path)
        => fs.GetFileSizeAsync(drive, path).GetAwaiter().GetResult();

    public static DateTime GetLastWriteTime(this IFileSystem fs, char drive, string[] path)
        => fs.GetLastWriteTimeAsync(drive, path).GetAwaiter().GetResult();

    public static uint GetVolumeSerialNumber(this IFileSystem fs, char drive)
        => fs.GetVolumeSerialNumberAsync(drive).GetAwaiter().GetResult();

    public static string GetVolumeLabel(this IFileSystem fs, char drive)
        => fs.GetVolumeLabelAsync(drive).GetAwaiter().GetResult();

    public static long GetFreeBytes(this IFileSystem fs, char drive)
        => fs.GetFreeBytesAsync(drive).GetAwaiter().GetResult();

    public static IReadOnlyDictionary<string, string> GetFileAssociations(this IFileSystem fs)
        => fs.GetFileAssociationsAsync().GetAwaiter().GetResult();
}

