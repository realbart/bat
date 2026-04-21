using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("setlocal")]
internal class SetLocalCommand : ICommand
{
    private const string HelpText = """
        Begins localization of environment changes in a batch file.  Environment
        changes made after SETLOCAL has been issued are local to the batch file.
        ENDLOCAL must be issued to restore the previous settings.  When the end
        of a batch script is reached, an implied ENDLOCAL is executed for any
        outstanding SETLOCAL commands issued by that batch script.

        SETLOCAL

        If Command Extensions are enabled SETLOCAL changes as follows:

        SETLOCAL batch command now accepts optional arguments:
                ENABLEEXTENSIONS / DISABLEEXTENSIONS
                    enable or disable command processor extensions. These
                    arguments takes precedence over the CMD /E:ON or /E:OFF
                    switches. See CMD /? for details.
                ENABLEDELAYEDEXPANSION / DISABLEDELAYEDEXPANSION
                    enable or disable delayed environment variable
                    expansion. These arguments takes precedence over the CMD
                    /V:ON or /V:OFF switches. See CMD /? for details.
        These modifications last until the matching ENDLOCAL command,
        regardless of their setting prior to the SETLOCAL command.

        The SETLOCAL command will set the ERRORLEVEL value if given
        an argument.  It will be zero if one of the two valid arguments
        is given and one otherwise.  You can use this in batch scripts
        to determine if the extensions are available, using the following
        technique:

            VERIFY OTHER 2>nul
            SETLOCAL ENABLEEXTENSIONS
            IF ERRORLEVEL 1 echo Unable to enable extensions

        This works because on old versions of CMD.EXE, SETLOCAL does NOT
        set the ERRORLEVEL value. The VERIFY command with a bad argument
        initializes the ERRORLEVEL value to a non-zero value.
        """;

    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        if (arguments.IsHelpRequest) { await batchContext.Console!.Out.WriteAsync(HelpText); return 0; }

        var ctx = batchContext.Context;
        var snapshot = new EnvironmentSnapshot(
            new(ctx.EnvironmentVariables, StringComparer.OrdinalIgnoreCase),
            new(ctx.GetAllDrivePaths()),
            ctx.CurrentDrive,
            ctx.DelayedExpansion,
            ctx.ExtensionsEnabled,
            ctx.ErrorCode
        );
        batchContext.SetLocalStack.Push(snapshot);

        // SETLOCAL sets ERRORLEVEL to 0 if a valid argument is given, 1 otherwise.
        // No argument → no ERRORLEVEL change (return 0 from command).
        var errorLevel = 0;
        foreach (var positional in arguments.Positionals)
        {
            var upper = positional.ToUpperInvariant();
            switch (upper)
            {
                case "ENABLEDELAYEDEXPANSION": ctx.DelayedExpansion = true; break;
                case "DISABLEDELAYEDEXPANSION": ctx.DelayedExpansion = false; break;
                case "ENABLEEXTENSIONS": ctx.ExtensionsEnabled = true; break;
                case "DISABLEEXTENSIONS": ctx.ExtensionsEnabled = false; break;
                default: errorLevel = 1; break;
            }
        }

        return errorLevel;
    }
}
