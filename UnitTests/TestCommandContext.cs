using Bat.Console;
using Context;
using Bat.Execution;
using BatD.Files;

namespace Bat.UnitTests;

/// <summary>Minimal IContext for unit-testing individual commands.</summary>
internal class TestCommandContext(IFileSystem? fileSystem = null) : IContext
{
    private readonly Dictionary<char, string[]> _paths = [];

    public IConsole Console { get; set; } = new TestConsole();
    public char CurrentDrive { get; private set; } = 'Z';
    public string[] CurrentPath => _paths.TryGetValue(CurrentDrive, out var p) ? p : [];
    public string CurrentPathDisplayName =>
        CurrentPath.Length == 0 ? $"{CurrentDrive}:\\" : $"{CurrentDrive}:\\{string.Join("\\", CurrentPath)}";
    public IDictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IDictionary<string, string> Macros { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public System.Globalization.CultureInfo FileCulture { get; set; } = NormalizedFileCulture.Create(System.Globalization.CultureInfo.InvariantCulture);
    public List<string> CommandHistory { get; } = [];
    public int HistorySize { get; set; } = 50;
    public int ErrorCode { get; set; }
    public IFileSystem FileSystem => fileSystem!;
    public object? CurrentBatch { get; set; }
    public bool EchoEnabled { get; set; } = true;
    public bool DelayedExpansion { get; set; }
    public bool ExtensionsEnabled { get; set; } = true;
    public string PromptFormat { get; set; } = "$P$G";
    public Stack<(char Drive, string[] Path)> DirectoryStack { get; } = new();
    public void SetPath(char drive, string[] path) => _paths[drive] = path;
    public void SetCurrentDrive(char drive) => CurrentDrive = drive;
    public string[] GetPathForDrive(char drive) => _paths.TryGetValue(drive, out var p) ? p : [];
    public IReadOnlyDictionary<char, string[]> GetAllDrivePaths() => _paths;
    public void RestoreAllDrivePaths(Dictionary<char, string[]> paths)
    {
        _paths.Clear();
        foreach (var kv in paths)
            _paths[kv.Key] = kv.Value.ToArray();
    }
    public async Task<(bool Found, string NativePath)> TryGetCurrentFolderAsync()
    {
        if (!FileSystem.DirectoryExists(CurrentDrive, CurrentPath))
            return (false, "");
        var hp = await FileSystem.GetNativePathAsync(new BatPath(CurrentDrive, CurrentPath));
        return (true, hp.Path);
    }

    public void ApplySnapshot(IContext other)
    {
        ErrorCode = other.ErrorCode;
        EchoEnabled = other.EchoEnabled;
        DelayedExpansion = other.DelayedExpansion;
        ExtensionsEnabled = other.ExtensionsEnabled;
        PromptFormat = other.PromptFormat;
        EnvironmentVariables.Clear();
        foreach (var kv in other.EnvironmentVariables)
            EnvironmentVariables[kv.Key] = kv.Value;
        Macros.Clear();
        foreach (var kv in other.Macros)
            Macros[kv.Key] = kv.Value;
    }

    public IContext StartNew(IConsole? console = null)
    {
        var newContext = new TestCommandContext(fileSystem)
        {
            Console = console ?? this.Console,
            CurrentDrive = this.CurrentDrive,
            ErrorCode = this.ErrorCode,
            EchoEnabled = this.EchoEnabled,
            DelayedExpansion = this.DelayedExpansion,
            ExtensionsEnabled = this.ExtensionsEnabled,
            PromptFormat = this.PromptFormat,
            FileCulture = this.FileCulture,
            HistorySize = this.HistorySize,
            CurrentBatch = this.CurrentBatch
        };

        foreach (var kv in EnvironmentVariables)
            newContext.EnvironmentVariables[kv.Key] = kv.Value;
        foreach (var kv in Macros)
            newContext.Macros[kv.Key] = kv.Value;
        foreach (var item in CommandHistory)
            newContext.CommandHistory.Add(item);
        foreach (var kv in _paths)
            newContext.SetPath(kv.Key, [.. kv.Value]);
        foreach (var item in DirectoryStack.Reverse())
            newContext.DirectoryStack.Push(item);

        return newContext;
    }

    public IPseudoTerminal CreatePty() => throw new NotSupportedException("PTY not available in tests");
}
