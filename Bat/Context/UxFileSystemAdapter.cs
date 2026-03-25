using Context;

namespace Bat.Context;

internal class UxFileSystemAdapter : FileSystem
{
    // All members throw NotImplementedException until STEP 13 (UxFileSystem)
    public override string GetNativePath(char drive, string[] path) => throw new NotImplementedException();
    public override bool FileExists(char drive, string[] path) => throw new NotImplementedException();
    public override bool DirectoryExists(char drive, string[] path) => throw new NotImplementedException();
    public override void CreateDirectory(char drive, string[] path) => throw new NotImplementedException();
    public override void DeleteFile(char drive, string[] path) => throw new NotImplementedException();
    public override void DeleteDirectory(char drive, string[] path, bool recursive) => throw new NotImplementedException();
    public override IEnumerable<DosFileEntry> EnumerateEntries(char drive, string[] path, string pattern) => throw new NotImplementedException();
    public override Stream OpenRead(char drive, string[] path) => throw new NotImplementedException();
    public override Stream OpenWrite(char drive, string[] path, bool append) => throw new NotImplementedException();
    public override string ReadAllText(char drive, string[] path) => throw new NotImplementedException();
    public override void WriteAllText(char drive, string[] path, string content) => throw new NotImplementedException();
    public override void CopyFile(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath, bool overwrite) => throw new NotImplementedException();
    public override void MoveFile(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath) => throw new NotImplementedException();
    public override void RenameFile(char drive, string[] path, string newName) => throw new NotImplementedException();
    public override FileAttributes GetAttributes(char drive, string[] path) => throw new NotImplementedException();
    public override void SetAttributes(char drive, string[] path, FileAttributes attributes) => throw new NotImplementedException();
    public override long GetFileSize(char drive, string[] path) => throw new NotImplementedException();
    public override DateTime GetLastWriteTime(char drive, string[] path) => throw new NotImplementedException();
    protected override uint GetVolumeSerialNumber(string nativeRoot) => throw new NotImplementedException();
}
