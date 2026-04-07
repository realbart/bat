namespace Context;

public interface IContext
{
    char CurrentDrive { get; }
    string[] CurrentPath { get; }
    string CurrentPathDisplayName { get; }
    Dictionary<string, string> EnvironmentVariables { get; }
    int ErrorCode { get; set; }
    IFileSystem FileSystem { get; }

    // Batch execution state (null only at startup, set to REPL singleton or batch context)
    object? CurrentBatch { get; set; }

    // CMD state
    bool EchoEnabled { get; set; }
    bool DelayedExpansion { get; set; }  // CMD /V:ON
    bool ExtensionsEnabled { get; set; }
    string PromptFormat { get; set; }  // %PROMPT% env var

    // Directory stack for PUSHD/POPD
    Stack<(char Drive, string[] Path)> DirectoryStack { get; }

    void SetPath(char drive, string[] path);
    void SetCurrentDrive(char drive);
    string[] GetPathForDrive(char drive);
    (bool Found, string NativePath) TryGetCurrentFolder();
    IReadOnlyDictionary<char, string[]> GetAllDrivePaths();
    void RestoreAllDrivePaths(Dictionary<char, string[]> paths);
}
