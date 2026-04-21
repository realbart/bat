using Context;

namespace Bat.Execution;

/// <summary>
/// Batch execution state - analogous to ReactOS BATCH_CONTEXT
/// https://doxygen.reactos.org/d3/d0a/cmd_8h_source.html
/// </summary>
internal class BatchContext
{
    internal required IContext Context { get; set; }

    /// <summary>
    /// Console for this execution. Defaults to Context.Console.
    /// Set to override for redirections.
    /// </summary>
    internal IConsole Console => Context.Console;

    public string? BatchFilePath { get; set; }
    public string? FileContent { get; set; }
    public int FilePosition { get; set; }
    public int LineNumber { get; set; }

    // Parameters (%0..%9)
    // REPL: ["CMD", null, ...] → %1 blijft %1
    // Batch: [filePath, arg1, ...] → %1 wordt arg1
    public string?[] Parameters { get; set; } = new string?[10];
    public int ShiftOffset { get; set; }

    // SETLOCAL stack
    public Stack<EnvironmentSnapshot> SetLocalStack { get; } = new();

    // CALL nesting (ReactOS naming: prev)
    public BatchContext? Prev { get; set; }

    // Label cache (null for REPL → GOTO doet niks)
    public Dictionary<string, int>? LabelPositions { get; set; }

    // Helpers
    public bool IsReplMode => BatchFilePath == null;
    public bool IsBatchFile => BatchFilePath != null;
}

/// <summary>
/// Snapshot of environment state for SETLOCAL/ENDLOCAL
/// </summary>
public record EnvironmentSnapshot(
    Dictionary<string, string> Variables,
    Dictionary<char, string[]> Paths,
    char CurrentDrive,
    bool DelayedExpansion,
    bool ExtensionsEnabled,
    int ErrorCode
);
