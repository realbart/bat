using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class TimeCommand : ICommand
{
    public string Name => "time";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Displays or sets the system time.";
    public string HelpText => "TIME [/T | time]\n\nType TIME with no parameters to display the current time setting and a prompt\nfor a new one.  Press ENTER to keep the same time.\n\nIf Command Extensions are enabled, the TIME command supports the /T\nswitch which tells the command to just output the current time, without\nprompting for a new time.";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        if (args.Any(a => a.Equals("/t", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(DateTime.Now.ToString("HH:mm"));
            return;
        }

        console.WriteLine($"The current time is: {DateTime.Now:HH:mm:ss.ff}");
        console.Write("Enter the new time: ");
    }
}
