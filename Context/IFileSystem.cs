namespace Context;

public interface IFileSystem
{
    public string GetFullPathDisplayName(char drive, string[] path);
    public string GetNativePath(char drive, string[] path);

    public string GetDisplayName(string segment);
}
