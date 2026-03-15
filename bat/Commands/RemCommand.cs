using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class RemCommand : ICommand
{
    public string Name => "rem";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Records comments (remarks) in batch files or CONFIG.SYS.";
    public string HelpText => "REM [comment]";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }
    }
}
