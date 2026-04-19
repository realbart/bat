namespace Bat.Execution;

/// <summary>
/// Singleton REPL batch context (per thread).
/// In REPL mode, there's no batch file, parameters default to ["CMD", null, ...], and GOTO does nothing.
/// </summary>
internal static class ReplBatchContext
{
    private static readonly ThreadLocal<BatchContext> _instance = new(() => new()
    {
        Context = null!,
        BatchFilePath = null,
        FileContent = "",
        Parameters = ["CMD", null, null, null, null, null, null, null, null, null],
        LabelPositions = null,  // GOTO doet niks in REPL
    });

    internal static BatchContext Value => _instance.Value!;

    public static void Reset()
    {
        var bc = Value;
        bc.BatchFilePath = null;
        bc.FileContent = "";
        bc.FilePosition = 0;
        bc.LineNumber = 0;
        bc.Parameters = ["CMD", null, null, null, null, null, null, null, null, null];
        bc.ShiftOffset = 0;
        bc.SetLocalStack.Clear();
        bc.Prev = null;
        bc.LabelPositions = null;
    }
}
