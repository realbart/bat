using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class ExitCommand : ICommand
{
    public string Name => "exit";
    public string[] Aliases => Array.Empty<string>();

    public string Description => "Quits the DOSUX program (command-interpreter).";
    public string HelpText => "EXIT [/B] [exitCode]\n\n  /B          specifies to exit the current batch script instead of\n              DOSUX.EXE.  If executed from outside a batch script, it\n              will quit DOSUX.EXE\n  exitCode    specifies a numeric number.  If /B is specified, sets\n              ERRORLEVEL that number.  If quitting DOSUX.EXE, sets the\n              process exit code with that number.";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        var exitCode = 0;

        // Microsoft spec: exit [/b] [<exitcode>]
        // /b is for scripts, we don't have those yet, but let's parse it anyway
        var exitCodeArg = args.FirstOrDefault(a => !a.Equals("/b", StringComparison.OrdinalIgnoreCase));
        
        if (exitCodeArg != null && int.TryParse(exitCodeArg, out var code))
        {
            exitCode = code;
        }

        Environment.Exit(exitCode);
    }
}
