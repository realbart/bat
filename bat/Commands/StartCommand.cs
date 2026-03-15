using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class StartCommand : ICommand
{
    public string Name => "start";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Starts a separate window to run a specified program or command.";
    public string HelpText => "START [\"title\"] [/D path] [/I] [/MIN] [/MAX] [/WAIT]\n      [command/program] [parameters]";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        // Would use Process.Start in a real implementation
    }
}
