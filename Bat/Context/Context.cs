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
    public List<string> Parameters { get; } = [];
    public bool DelayedExpansion { get; set; } // enables !VAR! expansion
}
