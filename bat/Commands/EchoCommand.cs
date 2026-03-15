using System.Threading;
using System.Threading.Tasks;
using System.IO.Abstractions;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class EchoCommand : ICommand
{
    public string Name => "echo";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Displays messages, or turns command-echoing on or off.";
    public string HelpText => "ECHO [ON | OFF]\nECHO [message]\n\nType ECHO without parameters to display the current echo setting.";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        if (args.Length == 1 && args[0].Equals("ON", StringComparison.OrdinalIgnoreCase))
        {
            fileSystem.EchoOn = true;
            return;
        }
        
        if (args.Length == 1 && args[0].Equals("OFF", StringComparison.OrdinalIgnoreCase))
        {
            fileSystem.EchoOn = false;
            return;
        }

        if (args.Length == 0)
        {
            console.WriteLine(fileSystem.EchoOn ? "ECHO is ON." : "ECHO is OFF.");
            return;
        }

        if (args.Length == 1 && args[0] == ".")
        {
            console.WriteLine();
            return;
        }

        console.WriteLine(string.Join(" ", args));
    }
}
