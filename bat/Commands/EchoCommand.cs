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

        if (args.Length == 0)
        {
            console.WriteLine("ECHO is ON.");
            return;
        }

        if (args.Length == 1 && args[0].Equals("ON", StringComparison.OrdinalIgnoreCase))
        {
            // Dummy for now
            return;
        }
        
        if (args.Length == 1 && args[0].Equals("OFF", StringComparison.OrdinalIgnoreCase))
        {
            // Dummy for now
            return;
        }

        console.WriteLine(string.Join(" ", args));
    }
}
