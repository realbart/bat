namespace Context;

public interface IFileSystem
{
    string GetFullPathDisplayName(BatPath path);
    string GetDisplayName(string segment);
    string GetNativePath(BatPath path);

    IReadOnlyDictionary<char, string> GetSubsts();
    void AddSubst(char drive, string nativePath);
    void RemoveSubst(char drive);

    // ── Async members ──────────────────────────────────────────────────────────

    Task<bool> FileExistsAsync(BatPath path, CancellationToken cancellationToken = default);
    Task<bool> DirectoryExistsAsync(BatPath path, CancellationToken cancellationToken = default);
    Task<bool> IsExecutableAsync(BatPath path, CancellationToken cancellationToken = default);
    Task CreateDirectoryAsync(BatPath path, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(BatPath path, CancellationToken cancellationToken = default);
    Task DeleteDirectoryAsync(BatPath path, bool recursive, CancellationToken cancellationToken = default);

    IAsyncEnumerable<DosFileEntry> EnumerateEntriesAsync(BatPath path, string pattern, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(BatPath path, CancellationToken cancellationToken = default);
    Task<Stream> OpenWriteAsync(BatPath path, bool append, CancellationToken cancellationToken = default);
    Task<string> ReadAllTextAsync(BatPath path, CancellationToken cancellationToken = default);
    Task WriteAllTextAsync(BatPath path, string content, CancellationToken cancellationToken = default);

    Task CopyFileAsync(BatPath source, BatPath dest, bool overwrite, CancellationToken cancellationToken = default);
    Task MoveFileAsync(BatPath source, BatPath dest, CancellationToken cancellationToken = default);
    Task RenameFileAsync(BatPath path, string newName, CancellationToken cancellationToken = default);

    Task<FileAttributes> GetAttributesAsync(BatPath path, CancellationToken cancellationToken = default);
    Task SetAttributesAsync(BatPath path, FileAttributes attributes, CancellationToken cancellationToken = default);
    Task<long> GetFileSizeAsync(BatPath path, CancellationToken cancellationToken = default);
    Task<DateTime> GetLastWriteTimeAsync(BatPath path, CancellationToken cancellationToken = default);

    Task<uint> GetVolumeSerialNumberAsync(char drive, CancellationToken cancellationToken = default);
    Task<string> GetVolumeLabelAsync(char drive, CancellationToken cancellationToken = default);
    Task<long> GetFreeBytesAsync(char drive, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string>> GetFileAssociationsAsync(CancellationToken cancellationToken = default);

    char NativeDirectorySeparator { get; }
    char NativePathSeparator { get; }

    bool TryGetNativePath(BatPath path, out string nativePath);
}
