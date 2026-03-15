using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class ColorCommand : ICommand
{
    public string Name => "color";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Sets the default console foreground and background colors.";
    public string HelpText => "COLOR [attr]\n\n  attr        Specifies attributes for console color output\n\nColor attributes are specified by TWO hex digits -- the first\ncorresponds to the background; the second the foreground.  Each digit\ncan be any of the following values:\n\n    0 = Black       8 = Gray\n    1 = Blue        9 = Light Blue\n    2 = Green       A = Light Green\n    3 = Aqua        B = Light Aqua\n    4 = Red         C = Light Red\n    5 = Purple      D = Light Purple\n    6 = Yellow      E = Light Yellow\n    7 = White       F = Bright White";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        if (args.Length == 0)
        {
            // Reset to default
            return;
        }

        // Dummy for now, as Spectre.Console handling is different
        // In a real DOS emulator, we would change the actual console colors.
    }
}
