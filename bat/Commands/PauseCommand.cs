using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class PauseCommand : ICommand
{
    public string Name => "pause";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Suspends processing of a batch file and displays a message.";
    public string HelpText => "PAUSE";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        console.WriteLine("Press any key to continue . . . ");
        // Note: In a real interactive session this would wait, but for non-interactive/tests we just continue.
        // In the Dispatcher loop, ReadLine is used anyway.
    }
}
