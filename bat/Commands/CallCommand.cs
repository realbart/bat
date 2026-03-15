using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class CallCommand : ICommand
{
    public string Name => "call";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Calls one batch program from another.";
    public string HelpText => "CALL [drive:][path]filename [batch-parameters]\n\n  batch-parameters   Specifies any command-line information required by the\n                     batch program.";

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
