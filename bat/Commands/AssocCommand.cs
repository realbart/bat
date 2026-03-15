using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class AssocCommand : ICommand
{
    public string Name => "assoc";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Displays or modifies file extension associations.";
    public string HelpText => "ASSOC [.ext[=[fileType]]]\n\n  .ext      Specifies the file extension to associate the file type with\n  fileType  Specifies the file type to associate with the file extension\n\nType ASSOC without parameters to display the current file associations.";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        // Just show some dummy associations for now
        if (args.Length == 0)
        {
            console.WriteLine(".txt=txtfile");
            console.WriteLine(".bat=batfile");
        }
    }
}
