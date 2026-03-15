using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class VolCommand : ICommand
{
    public string Name => "vol";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Displays a disk volume label and serial number.";
    public string HelpText => "VOL [drive:]";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        console.WriteLine(" Volume in drive C is OS");
        console.WriteLine(" Volume Serial Number is ABCD-1234");
    }
}
