using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class VerifyCommand : ICommand
{
    public string Name => "verify";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Tells cmd.exe whether to verify that your files are written correctly to a disk.";
    public string HelpText => "VERIFY [ON | OFF]\n\nType VERIFY without a parameter to display the current VERIFY setting.";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        if (args.Length == 0)
        {
            console.WriteLine("VERIFY is off.");
            return;
        }

        // Dummy implementation
    }
}
