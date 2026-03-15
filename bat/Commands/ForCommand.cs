using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class ForCommand : ICommand
{
    public string Name => "for";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Runs a specified command for each file in a set of files.";
    public string HelpText => "FOR %variable IN (set) DO command [command-parameters]\n\n  %variable  Specifies a replaceable parameter.\n  (set)      Specifies a set of one or more files. Wildcards may be used.\n  command    Specifies the command to carry out for each file.\n  command-parameters\n             Specifies parameters or switches for the specified command.";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        // Complex parsing needed
    }
}
