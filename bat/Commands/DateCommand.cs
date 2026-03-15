using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class DateCommand : ICommand
{
    public string Name => "date";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Displays or sets the date.";
    public string HelpText => "DATE [/T | date]\n\nType DATE without parameters to display the current date setting and\na prompt for a new one.  Press ENTER to keep the same date.\n\nIf Command Extensions are enabled, the DATE command supports the /T\nswitch which tells the command to just output the current date, without\nprompting for a new date.";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        if (args.Any(a => a.Equals("/t", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(DateTime.Now.ToString("dd-MM-yyyy"));
            return;
        }

        console.WriteLine($"The current date is: {DateTime.Now:ddd dd-MM-yyyy}");
        console.Write("Enter the new date: (dd-mm-yy) ");
        // In a real DOS, this would wait for input. For now we just don't change it.
    }
}
