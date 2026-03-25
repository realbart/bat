namespace Context;

/// <summary>
/// Represents a file or directory entry with DOS/Windows metadata.
/// All fields are populated in a single syscall via FindFirstFile/FindNextFile.
/// </summary>
public readonly record struct DosFileEntry(
    string Name,
    bool IsDirectory,
    string ShortName,
    long Size,
    DateTime LastWriteTime,
    FileAttributes Attributes);
