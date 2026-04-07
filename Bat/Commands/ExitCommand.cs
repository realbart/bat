using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("exit", Flags = "B")]
[BuiltInCommand("quit", Flags = "B")]
internal class ExitCommand : ICommand
{
    internal const int ExitSentinel = int.MinValue;
    internal const int ExitBatchSentinel = int.MinValue + 1;

    private const string HelpText =
        """
        Quits the CMD.EXE program (command interpreter) or the current batch
        script.

        EXIT [/B] [exitCode]

          /B          specifies to exit the current batch script instead of
                      CMD.EXE.  If executed from outside a batch script, it
                      will quit CMD.EXE

          exitCode    specifies a numeric number.  if /B is specified, sets
                      ERRORLEVEL that number.  If quitting CMD.EXE, sets the process
                      exit code with that number.

        If Command Extensions are enabled EXIT changes as follows:

        Executing EXIT /B after a CALL with a target label will return control
        to the statement immediately following the CALL.
        """;

    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        if (arguments.IsHelpRequest) { await batchContext.Console.Out.WriteLineAsync(HelpText); return 0; }
        if (int.TryParse(arguments.Positionals.FirstOrDefault(), out var code)) batchContext.Context.ErrorCode = code;
        if (arguments.GetFlagValue('B') && batchContext.IsBatchFile) return ExitBatchSentinel;
        return ExitSentinel;
    }
}
