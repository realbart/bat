using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("cd", Flags = "D")]
[BuiltInCommand("chdir", Flags = "D")]
internal class CdCommand : ICommand
{
    private const string HelpText = """
        Displays the name of or changes the current directory.

        CHDIR [/D] [drive:][path]
        CHDIR [..]
        CD [/D] [drive:][path]
        CD [..]

          ..   Specifies that you want to change to the parent directory.

        Type CD drive: to display the current directory in the specified drive.
        Type CD without parameters to display the current drive and directory.

        Use the /D switch to change current drive in addition to changing current
        directory for a drive.

        If Command Extensions are enabled CHDIR changes as follows:

        The current directory string is converted to use the same case as
        the on disk names.  So CD C:\TEMP would actually set the current
        directory to C:\Temp if that is the case on disk.

        CHDIR command does not treat spaces as delimiters, so it is possible to
        CD into a subdirectory name that contains a space without surrounding
        the name with quotes.  For example:

            cd \winnt\profiles\username\programs\start menu

        is the same as:

            cd "\winnt\profiles\username\programs\start menu"

        which is what you would have to type if extensions were disabled.
        """;

    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        if (arguments.IsHelpRequest) { await batchContext.Console!.Out.WriteAsync(HelpText); return 0; }

        var context = batchContext.Context;
        var slashD = arguments.GetFlagValue('D');
        var positional = arguments.Positionals.FirstOrDefault() ?? "";

        if (positional.Length == 0)
        {
            await batchContext.Console.Out.WriteLineAsync(context.CurrentPathDisplayName);
            return 0;
        }

        var targetDrive = context.CurrentDrive;
        var pathPart = positional;

        if (positional.Length >= 2 && char.IsLetter(positional[0]) && positional[1] == ':')
        {
            targetDrive = char.ToUpperInvariant(positional[0]);
            pathPart = positional.Substring(2);
        }

        if (pathPart.Length == 0 && !slashD)
        {
            await batchContext.Console!.Out.WriteLineAsync(context.FileSystem.GetFullPathDisplayName(targetDrive, context.GetPathForDrive(targetDrive)));
            return 0;
        }

        if (pathPart.Length == 0)
        {
            context.SetCurrentDrive(targetDrive);
            return 0;
        }

        var newPath = ResolvePath(context, targetDrive, pathPart);

        if (!context.FileSystem.DirectoryExists(targetDrive, newPath))
        {
            await batchContext.Console.Error.WriteLineAsync("The system cannot find the path specified.");
            return 1;
        }

        context.SetPath(targetDrive, newPath);
        if (slashD || targetDrive == context.CurrentDrive) context.SetCurrentDrive(targetDrive);
        return 0;
    }

    private static string[] ResolvePath(IContext context, char drive, string pathPart)
    {
        if (pathPart.StartsWith('\\'))
        {
            return pathPart.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
        }

        var current = new List<string>(context.GetPathForDrive(drive));
        foreach (var part in pathPart.Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "..")
            {
                if (current.Count > 0) current.RemoveAt(current.Count - 1);
            }
            else if (part != ".")
            {
                current.Add(part);
            }
        }
        return [.. current];
    }
}
