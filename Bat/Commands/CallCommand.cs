using Bat.Console;
using Bat.Execution;
using Bat.Nodes;
using Bat.Tokens;
using Context;

namespace Bat.Commands;

[BuiltInCommand("call")]
internal class CallCommand : ICommand
{
    private const string HelpText =
        """
        Calls one batch program from another.

        CALL [drive:][path]filename [batch-parameters]

          batch-parameters   Specifies any command-line information required by the
                             batch program.

        If Command Extensions are enabled CALL changes as follows:

        CALL command now accepts labels as the target of the CALL.  The syntax
        is:

            CALL :label arguments

        A new batch file context is created with the specified arguments and
        control is passed to the statement after the label specified.  You must
        "exit" twice by reaching the end of the batch script file twice.  The
        first time you read the end, control will return to just after the CALL
        statement.  The second time will exit the batch script.  Type GOTO /?
        for a description of the GOTO :EOF extension that will allow you to
        "return" from a batch script.

        In addition, expansion of batch script argument references (%0, %1,
        etc.) have been changed as follows:

            %* in a batch script refers to all the arguments (e.g. %1 %2 %3
                %4 %5 ...)

            Substitution of batch parameters (%n) has been enhanced.  You can
            now use the following optional syntax:

                %~1         - expands %1 removing any surrounding quotes (")
                %~f1        - expands %1 to a fully qualified path name
                %~d1        - expands %1 to a drive letter only
                %~p1        - expands %1 to a path only
                %~n1        - expands %1 to a file name only
                %~x1        - expands %1 to a file extension only
                %~s1        - expanded path contains short names only
                %~a1        - expands %1 to file attributes
                %~t1        - expands %1 to date/time of file
                %~z1        - expands %1 to size of file
                %~$PATH:1   - searches the directories listed in the PATH
                               environment variable and expands %1 to the fully
                               qualified name of the first one found.  If the
                               environment variable name is not defined or the
                               file is not found by the search, then this
                               modifier expands to the empty string

            The modifiers can be combined to get compound results:

                %~dp1       - expands %1 to a drive letter and path only
                %~nx1       - expands %1 to a file name and extension only
                %~dp$PATH:1 - searches the directories listed in the PATH
                               environment variable for %1 and expands to the
                               drive letter and path of the first one found.
                %~ftza1     - expands %1 to a DIR like output line

            In the above examples %1 and PATH can be replaced by other
            valid values.  The %~ syntax is terminated by a valid argument
            number.  The %~ modifiers may not be used with %*
        """;

    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        if (arguments.IsHelpRequest) { await batchContext.Console.Out.WriteLineAsync(HelpText); return 0; }

        var target = arguments.Positionals.FirstOrDefault() ?? "";
        if (target.Length == 0)
        {
            await batchContext.Console.Error.WriteLineAsync("The syntax of the command is incorrect.");
            return 1;
        }

        // CALL :label — subroutine within the current batch file
        if (target.StartsWith(':'))
            return await CallLabelAsync(target[1..], arguments, batchContext);

        // CALL file.bat [args] — call another batch file
        return await CallFileAsync(target, arguments, batchContext);
    }

    private static async Task<int> CallLabelAsync(string label, IArgumentSet arguments, BatchContext bc)
    {
        if (bc.LabelPositions == null || bc.FileContent == null)
        {
            await bc.Console.Error.WriteLineAsync("Cannot CALL :label outside of a batch file.");
            return 1;
        }

        if (!label.Equals("eof", StringComparison.OrdinalIgnoreCase) &&
            !bc.LabelPositions.TryGetValue(label, out _))
        {
            await bc.Console.Error.WriteLineAsync($"The system cannot find the batch label specified - {label}");
            return 1;
        }

        // Save current position as return address
        var savedPosition = bc.FilePosition;
        var savedLineNumber = bc.LineNumber;

        // Build subroutine parameters from remaining positionals
        var subParams = new string?[10];
        subParams[0] = bc.Parameters[0]; // Keep %0 as the batch file name
        for (var i = 1; i < arguments.Positionals.Count && i < 10; i++)
            subParams[i] = arguments.Positionals[i];

        var savedParams = bc.Parameters;
        var savedShift = bc.ShiftOffset;
        bc.Parameters = subParams;
        bc.ShiftOffset = 0;

        // Jump to label (or EOF)
        if (label.Equals("eof", StringComparison.OrdinalIgnoreCase))
        {
            bc.FilePosition = bc.FileContent.Length;
        }
        else
        {
            bc.FilePosition = bc.LabelPositions[label];
        }

        // Execute from the label position
        await BatchExecutor.ExecuteBatchLoopAsync(bc);

        // Restore state
        bc.FilePosition = savedPosition;
        bc.LineNumber = savedLineNumber;
        bc.Parameters = savedParams;
        bc.ShiftOffset = savedShift;

        return bc.Context.ErrorCode;
    }

    private static async Task<int> CallFileAsync(string target, IArgumentSet arguments, BatchContext bc)
    {
        var resolvedPath = ExecutableResolver.Resolve(target, bc.Context);
        if (resolvedPath == null)
        {
            await bc.Console.Error.WriteLineAsync($"'{target}' is not recognized as an internal or external command,");
            await bc.Console.Error.WriteLineAsync("operable program or batch file.");
            return 1;
        }

        var ext = Path.GetExtension(resolvedPath).ToLowerInvariant();
        if (ext is not (".bat" or ".cmd"))
        {
            await bc.Console.Error.WriteLineAsync("CALL: Only batch files (.bat, .cmd) can be called.");
            return 1;
        }

        var fileArgs = string.Join(" ", arguments.Positionals.Skip(1));
        var executor = new BatchExecutor();
        return await executor.ExecuteAsync(resolvedPath, fileArgs, bc, []);
    }
}
