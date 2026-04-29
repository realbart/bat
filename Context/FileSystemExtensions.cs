namespace Context;

/// <summary>
/// Extension methods for backwards compatibility with (char drive, string[] path) API.
/// These delegate to the BatPath-based IFileSystem methods.
/// </summary>
public static class FileSystemExtensions
{
    public static string GetFullPathDisplayName(this IFileSystem fs, char drive, string[] path)
        => fs.GetFullPathDisplayName(new BatPath(drive, path));

    public static string GetNativePath(this IFileSystem fs, char drive, string[] path)
        => fs.GetNativePath(new BatPath(drive, path));

    public static Task<bool> FileExistsAsync(this IFileSystem fs, char drive, string[] path, CancellationToken cancellationToken = default)
        => fs.FileExistsAsync(new BatPath(drive, path), cancellationToken);

    public static Task<bool> DirectoryExistsAsync(this IFileSystem fs, char drive, string[] path, CancellationToken cancellationToken = default)
        => fs.DirectoryExistsAsync(new BatPath(drive, path), cancellationToken);

    public static Task<bool> IsExecutableAsync(this IFileSystem fs, char drive, string[] path, CancellationToken cancellationToken = default)
        => fs.IsExecutableAsync(new BatPath(drive, path), cancellationToken);

    public static Task CreateDirectoryAsync(this IFileSystem fs, char drive, string[] path, CancellationToken cancellationToken = default)
        => fs.CreateDirectoryAsync(new BatPath(drive, path), cancellationToken);

    public static Task DeleteFileAsync(this IFileSystem fs, char drive, string[] path, CancellationToken cancellationToken = default)
        => fs.DeleteFileAsync(new BatPath(drive, path), cancellationToken);

    public static Task DeleteDirectoryAsync(this IFileSystem fs, char drive, string[] path, bool recursive, CancellationToken cancellationToken = default)
        => fs.DeleteDirectoryAsync(new BatPath(drive, path), recursive, cancellationToken);

    public static IAsyncEnumerable<DosFileEntry> EnumerateEntriesAsync(this IFileSystem fs, char drive, string[] path, string pattern, CancellationToken cancellationToken = default)
        => fs.EnumerateEntriesAsync(new BatPath(drive, path), pattern, cancellationToken);

    public static Task<Stream> OpenReadAsync(this IFileSystem fs, char drive, string[] path, CancellationToken cancellationToken = default)
        => fs.OpenReadAsync(new BatPath(drive, path), cancellationToken);

    public static Task<Stream> OpenWriteAsync(this IFileSystem fs, char drive, string[] path, bool append, CancellationToken cancellationToken = default)
        => fs.OpenWriteAsync(new BatPath(drive, path), append, cancellationToken);

    public static Task<string> ReadAllTextAsync(this IFileSystem fs, char drive, string[] path, CancellationToken cancellationToken = default)
        => fs.ReadAllTextAsync(new BatPath(drive, path), cancellationToken);

    public static Task WriteAllTextAsync(this IFileSystem fs, char drive, string[] path, string content, CancellationToken cancellationToken = default)
        => fs.WriteAllTextAsync(new BatPath(drive, path), content, cancellationToken);

    public static Task CopyFileAsync(this IFileSystem fs, char sourceDrive, string[] sourcePath, char destDrive, string[] destPath, bool overwrite, CancellationToken cancellationToken = default)
        => fs.CopyFileAsync(new BatPath(sourceDrive, sourcePath), new BatPath(destDrive, destPath), overwrite, cancellationToken);

    public static Task MoveFileAsync(this IFileSystem fs, char sourceDrive, string[] sourcePath, char destDrive, string[] destPath, CancellationToken cancellationToken = default)
        => fs.MoveFileAsync(new BatPath(sourceDrive, sourcePath), new BatPath(destDrive, destPath), cancellationToken);

    public static Task RenameFileAsync(this IFileSystem fs, char drive, string[] path, string newName, CancellationToken cancellationToken = default)
        => fs.RenameFileAsync(new BatPath(drive, path), newName, cancellationToken);

    public static Task<FileAttributes> GetAttributesAsync(this IFileSystem fs, char drive, string[] path, CancellationToken cancellationToken = default)
        => fs.GetAttributesAsync(new BatPath(drive, path), cancellationToken);

    public static Task SetAttributesAsync(this IFileSystem fs, char drive, string[] path, FileAttributes attributes, CancellationToken cancellationToken = default)
        => fs.SetAttributesAsync(new BatPath(drive, path), attributes, cancellationToken);

    public static Task<long> GetFileSizeAsync(this IFileSystem fs, char drive, string[] path, CancellationToken cancellationToken = default)
        => fs.GetFileSizeAsync(new BatPath(drive, path), cancellationToken);

    public static Task<DateTime> GetLastWriteTimeAsync(this IFileSystem fs, char drive, string[] path, CancellationToken cancellationToken = default)
        => fs.GetLastWriteTimeAsync(new BatPath(drive, path), cancellationToken);

    public static bool TryGetNativePath(this IFileSystem fs, char drive, string[] path, out string nativePath)
        => fs.TryGetNativePath(new BatPath(drive, path), out nativePath);
}
