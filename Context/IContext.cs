namespace Context;

public interface IContext
{
    char CurrentDrive { get; }
    string[] CurrentPath { get; }
    string CurrentPathDisplayName { get; }
    Dictionary<string, string> EnvironmentVariables { get; }
    int ErrorCode { get; set; }
    IFileSystem FileSystem { get; }
}
