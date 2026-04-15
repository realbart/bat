using Context;

namespace Bat.Context;

internal abstract class Context(IFileSystem fileSystem, IConsole console) : IContext
{
    public IConsole Console { get; private set; } = console;
    public int ErrorCode { get; set; } = 0;
    public Dictionary<string, string> EnvironmentVariables { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Macros { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> CommandHistory { get; } = [];
    public int HistorySize { get; set; } = 50;
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
    public System.Globalization.CultureInfo FileCulture { get; } = NormalizedFileCulture.Create(System.Globalization.CultureInfo.CurrentCulture);

    // Directory stack for PUSHD/POPD
    public Stack<(char Drive, string[] Path)> DirectoryStack { get; } = new();

    public void SetPath(char drive, string[] path) => CurrentFolders[drive] = path;
    public void SetCurrentDrive(char drive) => CurrentDrive = drive;
    public string[] GetPathForDrive(char drive) => CurrentFolders.TryGetValue(drive, out var p) ? p : [];

    public IReadOnlyDictionary<char, string[]> GetAllDrivePaths() => CurrentFolders;

    public void RestoreAllDrivePaths(Dictionary<char, string[]> paths)
    {
        CurrentFolders.Clear();
        foreach (var kv in paths)
            CurrentFolders[kv.Key] = kv.Value.ToArray();
    }

    public (bool Found, string NativePath) TryGetCurrentFolder()
    {
        if (!fileSystem.DirectoryExists(CurrentDrive, CurrentPath))
            return (false, "");
        return (true, fileSystem.GetNativePath(CurrentDrive, CurrentPath));
    }

    protected void InitializeFromEnvironment()
    {
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && entry.Value is string value) EnvironmentVariables[key] = value;
        }

        if (EnvironmentVariables.TryGetValue("PATH", out var hostPath))
        {
            EnvironmentVariables["PATH"] = PathTranslator.TranslateHostPathToBat(hostPath, fileSystem);
        }

        if (!EnvironmentVariables.ContainsKey("PROMPT")) EnvironmentVariables["PROMPT"] = "$P$G";

        PostProcessEnvironmentVariables();
        InitializeCurrentDirectory();
    }

    /// <summary>
    /// Override in platform subclasses to translate OS-native paths in environment variables
    /// to BAT virtual drive paths after the base environment has been loaded.
    /// </summary>
    protected virtual void PostProcessEnvironmentVariables() { }

    /// <summary>
    /// Platform-specific: map the process working directory into drive + path.
    /// Override in platform subclasses.
    /// </summary>
    protected virtual void InitializeCurrentDirectory() { }

    public abstract IContext StartNew(IConsole? console = null);

    protected IContext StartNewCore(IContext newInstance)
    {
        foreach (var kv in EnvironmentVariables)
            newInstance.EnvironmentVariables[kv.Key] = kv.Value;
        foreach (var kv in Macros)
            newInstance.Macros[kv.Key] = kv.Value;
        foreach (var item in CommandHistory)
            newInstance.CommandHistory.Add(item);
        foreach (var kv in GetAllDrivePaths())
            newInstance.SetPath(kv.Key, [.. kv.Value]);
        foreach (var item in DirectoryStack.Reverse())
            newInstance.DirectoryStack.Push(item);

        return newInstance;
    }
}
