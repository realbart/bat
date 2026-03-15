using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class ClsCommand : ICommand
{
    public string Name => "cls";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Clears the screen.";
    public string HelpText => "CLS";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        console.Clear();
    }
}
