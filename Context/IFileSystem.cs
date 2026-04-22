namespace Context;

public interface IFileSystem
{
    string GetFullPathDisplayName(char drive, string[] path);
    string GetDisplayName(string segment);
    string GetNativePath(char drive, string[] path);

    IReadOnlyDictionary<char, string> GetSubsts();
    void AddSubst(char drive, string nativePath);
    void RemoveSubst(char drive);

    // ── Async members ──────────────────────────────────────────────────────────

    Task<bool> FileExistsAsync(char drive, string[] path, CancellationToken cancellationToken = default);
    Task<bool> DirectoryExistsAsync(char drive, string[] path, CancellationToken cancellationToken = default);
    Task<bool> IsExecutableAsync(char drive, string[] path, CancellationToken cancellationToken = default);
    Task CreateDirectoryAsync(char drive, string[] path, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(char drive, string[] path, CancellationToken cancellationToken = default);
    Task DeleteDirectoryAsync(char drive, string[] path, bool recursive, CancellationToken cancellationToken = default);

    IAsyncEnumerable<DosFileEntry> EnumerateEntriesAsync(char drive, string[] path, string pattern, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(char drive, string[] path, CancellationToken cancellationToken = default);
    Task<Stream> OpenWriteAsync(char drive, string[] path, bool append, CancellationToken cancellationToken = default);
    Task<string> ReadAllTextAsync(char drive, string[] path, CancellationToken cancellationToken = default);
    Task WriteAllTextAsync(char drive, string[] path, string content, CancellationToken cancellationToken = default);

    Task CopyFileAsync(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath, bool overwrite, CancellationToken cancellationToken = default);
    Task MoveFileAsync(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath, CancellationToken cancellationToken = default);
    Task RenameFileAsync(char drive, string[] path, string newName, CancellationToken cancellationToken = default);

    Task<FileAttributes> GetAttributesAsync(char drive, string[] path, CancellationToken cancellationToken = default);
    Task SetAttributesAsync(char drive, string[] path, FileAttributes attributes, CancellationToken cancellationToken = default);
    Task<long> GetFileSizeAsync(char drive, string[] path, CancellationToken cancellationToken = default);
    Task<DateTime> GetLastWriteTimeAsync(char drive, string[] path, CancellationToken cancellationToken = default);

    Task<uint> GetVolumeSerialNumberAsync(char drive, CancellationToken cancellationToken = default);
    Task<string> GetVolumeLabelAsync(char drive, CancellationToken cancellationToken = default);
    Task<long> GetFreeBytesAsync(char drive, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string>> GetFileAssociationsAsync(CancellationToken cancellationToken = default);

    char NativeDirectorySeparator { get; }
    char NativePathSeparator { get; }
    bool TryGetNativePath(char drive, string[] path, out string nativePath);
}
