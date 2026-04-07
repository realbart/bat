using Bat.Console;
using Bat.Execution;
using Bat.Nodes;
using Bat.Tokens;
using Context;

namespace Bat.Commands;

[BuiltInCommand("call")]
internal class CallCommand : ICommand
{
    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
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
        var executor = new BatchExecutor(bc.Console);
        return await executor.ExecuteAsync(resolvedPath, fileArgs, bc, []);
    }
}
