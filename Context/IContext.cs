namespace Context;

public interface IContext
{
    IConsole Console { get; }
    char CurrentDrive { get; }
    string[] CurrentPath { get; }
    string CurrentPathDisplayName { get; }
    Dictionary<string, string> EnvironmentVariables { get; }
    Dictionary<string, string> Macros { get; }
    List<string> CommandHistory { get; }
    int HistorySize { get; set; }
    int ErrorCode { get; set; }
    IFileSystem FileSystem { get; }

    object? CurrentBatch { get; set; }

    bool EchoEnabled { get; set; }
    bool DelayedExpansion { get; set; }
    bool ExtensionsEnabled { get; set; }
    string PromptFormat { get; set; }
    System.Globalization.CultureInfo FileCulture { get; }

    Stack<(char Drive, string[] Path)> DirectoryStack { get; }

    void SetPath(char drive, string[] path);
    void SetCurrentDrive(char drive);
    string[] GetPathForDrive(char drive);
    (bool Found, string NativePath) TryGetCurrentFolder();
    IReadOnlyDictionary<char, string[]> GetAllDrivePaths();
    void RestoreAllDrivePaths(Dictionary<char, string[]> paths);

    /// <summary>
    /// Creates a new execution context for a command.
    /// Performs deep copy of state with optional console override.
    /// </summary>
    IContext StartNew(IConsole? console = null);
}
