using Context;

namespace Bat.Context;

internal abstract class Context(IFileSystem fileSystem) : IContext
{
    public int ErrorCode { get; set; } = 0;
    public Dictionary<string, string> EnvironmentVariables { get; } = [];
    private readonly Dictionary<char, string[]> CurrentFolders = [];
    public char CurrentDrive { get; } = 'C'; // todo: configuable in the command line arguments
    public string[] CurrentPath => CurrentFolders.TryGetValue(CurrentDrive, out var path) ? path : [];
    public string CurrentPathDisplayName => fileSystem.GetFullPathDisplayName(CurrentDrive, CurrentPath);
    public IFileSystem FileSystem => fileSystem;

    // Batch execution state (null only at startup)
    public object? CurrentBatch { get; set; }

    // CMD state
    public bool EchoEnabled { get; set; } = true;
    public bool DelayedExpansion { get; set; } = false;
    public bool ExtensionsEnabled { get; set; } = true;
    public string PromptFormat { get; set; } = "$P$G";  // Default: C:\path>

    // Directory stack for PUSHD/POPD
    public Stack<(char Drive, string[] Path)> DirectoryStack { get; } = new();
}
