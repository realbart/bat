using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class PushdCommand : ICommand
{
    public string Name => "pushd";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Stores the current directory for use by the POPD command, then changes to the specified directory.";
    public string HelpText => "PUSHD [path]";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        if (args.Length == 0)
        {
            console.MarkupLine("[red]The syntax of the command is incorrect.[/]");
            return;
        }

        fileSystem.PushDirectory(args[0]);
    }
}
