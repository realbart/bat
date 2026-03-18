namespace Context;

public interface IContext
{
    List<string> Parameters { get; }
    bool DelayedExpansion { get; set; }
    char CurrentDrive { get; }
    string[] CurrentPath { get; }
    string CurrentPathDisplayName { get; }
    Dictionary<string, string> EnvironmentVariables { get; }
    int ErrorCode { get; set; }
    IFileSystem FileSystem { get; }
}
