using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class IfCommand : ICommand
{
    public string Name => "if";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Performs conditional processing in batch programs.";
    public string HelpText => "Performs conditional processing in batch programs.\n\nIF [NOT] ERRORLEVEL number command\nIF [NOT] string1==string2 command\nIF [NOT] EXIST filename command\n\n  NOT               Specifies that cmd.exe should carry out the command\n                    only if the condition is false.\n  ERRORLEVEL number Specifies a true condition if the last program run\n                    returned an exit code equal to or greater than the\n                    number specified.\n  string1==string2  Specifies a true condition if the specified text strings\n                    match.\n  EXIST filename    Specifies a true condition if the specified filename\n                    exists.\n  command           Specifies the command to carry out if the condition is\n                    met.";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        // Scripting engine needed for full support
    }
}
