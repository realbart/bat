using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class BreakCommand : ICommand
{
    public string Name => "break";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Sets or clears extended CTRL+C checking on DOS systems.";
    public string HelpText => "Sets or clears extended CTRL+C checking on DOS systems.\n\nThis is present for compatibility with DOS systems. It has no effect\nunder Windows.";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }
    }
}
