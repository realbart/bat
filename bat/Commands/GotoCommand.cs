using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class GotoCommand : ICommand
{
    public string Name => "goto";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Directs cmd.exe to a labeled line in a batch program.";
    public string HelpText => "GOTO label\n\n  label   Specifies a text string used in a batch program as a label.";

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
