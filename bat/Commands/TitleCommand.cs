using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class TitleCommand : ICommand
{
    public string Name => "title";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Sets the window title for the command prompt window.";
    public string HelpText => "TITLE [string]";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        var title = string.Join(" ", args);
        try
        {
            Console.Title = title;
        }
        catch
        {
            // Might fail in some environments
        }
    }
}
