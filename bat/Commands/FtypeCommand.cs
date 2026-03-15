using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class FtypeCommand : ICommand
{
    public string Name => "ftype";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Displays or modifies file types used in file extension associations.";
    public string HelpText => "FTYPE [fileType[=[openCommandString]]]\n\n  fileType  Specifies the file type to examine or change\n  openCommandString Specifies the open command to use when launching files\n                     of this type.\n\nType FTYPE without parameters to display the current file types.";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        if (args.Length == 0)
        {
            console.WriteLine("batfile=\"%1\" %*");
            console.WriteLine("txtfile=notepad.exe \"%1\"");
        }
    }
}
