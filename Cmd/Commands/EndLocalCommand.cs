using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("endlocal")]
internal class EndLocalCommand : ICommand
{
    private const string HelpText = """
        Ends localization of environment changes in a batch file.
        Environment changes made after ENDLOCAL has been issued are
        not local to the batch file; the previous settings are not
        restored on termination of the batch file.

        ENDLOCAL

        If Command Extensions are enabled ENDLOCAL changes as follows:

        If the corresponding SETLOCAL enable or disabled command extensions
        using the new ENABLEEXTENSIONS or DISABLEEXTENSIONS options, then
        after the ENDLOCAL, the enabled/disabled state of command extensions
        will be restored to what it was prior to the matching SETLOCAL
        command execution.
        """;

    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        if (arguments.IsHelpRequest) { await batchContext.Console!.Out.WriteAsync(HelpText); return 0; }

        RestoreSnapshot(batchContext);
        return 0;
    }

    internal static void RestoreSnapshot(BatchContext batchContext)
    {
        if (!batchContext.SetLocalStack.TryPop(out var snapshot)) return;

        var ctx = batchContext.Context;
        ctx.EnvironmentVariables.Clear();
        foreach (var kv in snapshot.Variables)
            ctx.EnvironmentVariables[kv.Key] = kv.Value;

        ctx.RestoreAllDrivePaths(snapshot.Paths);
        ctx.SetCurrentDrive(snapshot.CurrentDrive);
        ctx.DelayedExpansion = snapshot.DelayedExpansion;
        ctx.ExtensionsEnabled = snapshot.ExtensionsEnabled;
        ctx.ErrorCode = snapshot.ErrorCode;
    }

    internal static void UnwindSetLocalStack(BatchContext batchContext)
    {
        while (batchContext.SetLocalStack.Count > 0)
            RestoreSnapshot(batchContext);
    }
}
