namespace Context;

public interface IFileSystem
{
    string GetFullPathDisplayName(char drive, string[] path);
    string GetDisplayName(string segment);
    string GetNativePath(char drive, string[] path);

    bool FileExists(char drive, string[] path);
    bool DirectoryExists(char drive, string[] path);
    bool IsExecutable(char drive, string[] path);
    void CreateDirectory(char drive, string[] path);
    void DeleteFile(char drive, string[] path);
    void DeleteDirectory(char drive, string[] path, bool recursive);

    IEnumerable<DosFileEntry> EnumerateEntries(char drive, string[] path, string pattern);

    Stream OpenRead(char drive, string[] path);
    Stream OpenWrite(char drive, string[] path, bool append);
    string ReadAllText(char drive, string[] path);
    void WriteAllText(char drive, string[] path, string content);

    void CopyFile(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath, bool overwrite);
    void MoveFile(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath);
    void RenameFile(char drive, string[] path, string newName);

    FileAttributes GetAttributes(char drive, string[] path);
    void SetAttributes(char drive, string[] path, FileAttributes attributes);
    long GetFileSize(char drive, string[] path);
    DateTime GetLastWriteTime(char drive, string[] path);

    uint GetVolumeSerialNumber(char drive);
    string GetVolumeLabel(char drive);
    long GetFreeBytes(char drive);

    IReadOnlyDictionary<string, string> GetFileAssociations();

    IReadOnlyDictionary<char, string> GetSubsts();
    void AddSubst(char drive, string nativePath);
    void RemoveSubst(char drive);
}
