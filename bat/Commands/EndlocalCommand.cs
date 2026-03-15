using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class EndlocalCommand : ICommand
{
    public string Name => "endlocal";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Ends localization of environment variables in a batch file.";
    public string HelpText => "ENDLOCAL";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        // Scripting engine needed
    }
}
