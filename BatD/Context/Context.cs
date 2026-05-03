using Bat.Context;
using BatD.Files;
using global::Context;

namespace BatD.Context;

// todo: rename context and icontext to session and isession
// todo: add flag that causes the inner context to be cloned only on write
public abstract class Context : IContext
{
    public volatile int CloneCount = 0;

    public Context(IFileSystem fileSystem, IConsole console)
    {
        _environmentVariables = new(StringComparer.OrdinalIgnoreCase);
        _macros = new(StringComparer.OrdinalIgnoreCase);
        CommandHistory = [];
        HistorySize = 50;
        // todo: no hard coded current drives, except in one place.
        // todo: use currentBatPath instead of current drive + path.
        CurrentFolders = [];
        CurrentDrive = 'C';
        this.fileSystem = fileSystem;
        this.Console = console;
    }

    public Context(IFileSystem fileSystem, IConsole console, Context inner)
    {
        _environmentVariables = inner._environmentVariables.Clone();
        _macros = inner._macros.Clone();
        CommandHistory = inner.CommandHistory.ToList();
        HistorySize = inner.HistorySize;
        CurrentFolders = inner.CurrentFolders.Clone();
        CurrentDrive = inner.CurrentDrive;
        this.fileSystem = fileSystem;
        this.Console = console;
        ErrorCode = inner.ErrorCode;
    }


    private IFileSystem fileSystem;
    public IConsole Console { get; }
    public int ErrorCode { get; set; } = 0;
    // todo: implement ClonableList
    private readonly ClonableDictionary<string, string> _environmentVariables;
    public IDictionary<string, string> EnvironmentVariables => _environmentVariables;
    private readonly ClonableDictionary<string, string> _macros;
    public IDictionary<string, string> Macros => _macros;
    public List<string> CommandHistory { get; }
    public int HistorySize { get; set; }

    protected readonly ClonableDictionary<char, string[]> CurrentFolders;
    public char CurrentDrive { get; protected set; }

    public string[] CurrentPath => CurrentFolders.TryGetValue(CurrentDrive, out var path) ? path : [];
    public string CurrentPathDisplayName => fileSystem.GetFullPathDisplayName(new BatPath(CurrentDrive, CurrentPath));
    public IFileSystem FileSystem => fileSystem;

    // Batch execution state (null only at startup)
    public object? CurrentBatch { get; set; }

    // CMD state
    public bool EchoEnabled { get; set; } = true;
    public bool DelayedExpansion { get; set; } = false;
    public bool ExtensionsEnabled { get; set; } = true;
    public string PromptFormat { get; set; } = "$P$G"; // Default: C:\path>

    public System.Globalization.CultureInfo FileCulture { get; } =
        System.Globalization.CultureInfo.CurrentCulture.Create();

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

    public void ApplySnapshot(IContext other)
    {
        if (other is Context otherCtx)
        {
            _environmentVariables.ApplySnapshot(otherCtx._environmentVariables);
            _macros.ApplySnapshot(otherCtx._macros);
            CurrentFolders.ApplySnapshot(otherCtx.CurrentFolders);
            CurrentDrive = otherCtx.CurrentDrive;
            ErrorCode = otherCtx.ErrorCode;
            EchoEnabled = otherCtx.EchoEnabled;
            DelayedExpansion = otherCtx.DelayedExpansion;
            ExtensionsEnabled = otherCtx.ExtensionsEnabled;
            PromptFormat = otherCtx.PromptFormat;
        }
    }

    public async Task<(bool Found, string NativePath)> TryGetCurrentFolderAsync()
    {
        if (!await fileSystem.DirectoryExistsAsync(new BatPath(CurrentDrive, CurrentPath)))
            return (false, "");
        var hostPath = await fileSystem.GetNativePathAsync(new BatPath(CurrentDrive, CurrentPath));
        return (true, hostPath.Path);
    }

    protected async Task InitializeFromEnvironmentAsync()
    {
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && entry.Value is string value) EnvironmentVariables[key] = value;
        }

        if (EnvironmentVariables.TryGetValue("PATH", out var hostPath))
        {
            EnvironmentVariables["PATH"] = await BatD.Files.PathTranslator.TranslateHostPathToBat(hostPath, fileSystem);
        }
        // todo: remove this code as the fallback prompt already is $P$G
        if (!EnvironmentVariables.ContainsKey("PROMPT")) EnvironmentVariables["PROMPT"] = "$P$G";

        PostProcessEnvironmentVariables();
        InitializeCurrentDirectory();

        // Point ComSpec at bat's own cmd.exe (like cmd.exe points at itself)
        var cmdExePath = Path.Combine(AppContext.BaseDirectory, "bin", "cmd.exe");
        var virtualCmdPath = await BatD.Files.PathTranslator.TranslateHostPathEntryToBat(cmdExePath, fileSystem);
        if (virtualCmdPath != null)
            EnvironmentVariables["ComSpec"] = virtualCmdPath;
    }

    /// <summary>
    /// Override in platform subclasses to translate OS-native paths in environment variables
    /// to BAT virtual drive paths after the base environment has been loaded.
    /// </summary>
    protected virtual void PostProcessEnvironmentVariables()
    {
    }

    /// <summary>
    /// Platform-specific: map the process working directory into drive + path.
    /// Override in platform subclasses.
    /// </summary>
    protected virtual void InitializeCurrentDirectory()
    {
    }

    public abstract IContext StartNew(IConsole? console = null);

    public abstract IPseudoTerminal CreatePty();

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

public sealed class Session: IDisposable
{
    private readonly Context context;

    public Session(Context context)
    {
        this.context = context;
        Interlocked.Increment(ref context.CloneCount);
    }

    // todo:
    // - session implements IContext
    // - all read operations delegate to context
    // - when writing (modifying lists or dictionaries), Session checks if context.CloneCount > 1,
    // and if so, clones the context and replaces it with the clone, then performs the write on the clone.
    // (note: clones use ClonableDictionaries so deep copies are only ever made when needed)
    // (the clone has clonecount = 1)
    // - the context itself is no longer passed, only the session

    /// <summary>
    /// Always use "using var session = context.StartNew();" when starting a new command execution context.
    /// </summary>
    public Session StartNew() => new (context);


    public void Dispose()
    {
        Interlocked.Decrement(ref context.CloneCount);
    }
}
