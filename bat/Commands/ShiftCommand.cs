using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class ShiftCommand : ICommand
{
    public string Name => "shift";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Changes the position of replaceable parameters in a batch file.";
    public string HelpText => "SHIFT [/n]\n\n  /n    Specifies to start shifting at the nth parameter, where n may be\n        between 0 and 8.";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        // Only useful in batch files
    }
}
