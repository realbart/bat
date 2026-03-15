using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class VerCommand : ICommand
{
    public string Name => "ver";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Displays the DOSUX version.";
    public string HelpText => "VER";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        console.WriteLine("DOSUX [Version 0.1.0]");
    }
}
