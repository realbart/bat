using Context;

namespace Bat.Context;

internal abstract class Context(IFileSystem fileSystem) : IContext
{
    public int ErrorCode { get; set; } = 0;
    public Dictionary<string, string> EnvironmentVariables { get; } = new(StringComparer.OrdinalIgnoreCase);
    protected readonly Dictionary<char, string[]> CurrentFolders = [];
    public char CurrentDrive { get; protected set; } = 'C';
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

    public void SetPath(char drive, string[] path) => CurrentFolders[drive] = path;
    public void SetCurrentDrive(char drive) => CurrentDrive = drive;
    public string[] GetPathForDrive(char drive) => CurrentFolders.TryGetValue(drive, out var p) ? p : [];

    protected void InitializeFromEnvironment()
    {
        // Copy all process environment variables into the context
        foreach (System.Collections.DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && entry.Value is string value)
                EnvironmentVariables[key] = value;
        }

        // Translate host PATH to Bat virtual drives
        if (EnvironmentVariables.TryGetValue("PATH", out var hostPath))
        {
            var batPath = PathTranslator.TranslateHostPathToBat(hostPath, fileSystem);
            EnvironmentVariables["PATH"] = batPath;
        }

        // PROMPT: if not provided by the environment, default to $P$G
        if (!EnvironmentVariables.ContainsKey("PROMPT"))
            EnvironmentVariables["PROMPT"] = "$P$G";

        // Current drive + path: platform-specific
        InitializeCurrentDirectory();
    }

    /// <summary>
    /// Platform-specific: map the process working directory into drive + path.
    /// Override in platform subclasses.
    /// </summary>
    protected virtual void InitializeCurrentDirectory() { }
}
