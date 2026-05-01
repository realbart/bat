using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("type")]
internal class TypeCommand : ICommand
{
    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        var context = batchContext.Context;
        var console = batchContext.Console;

        if (arguments.IsHelpRequest)
        {
            await console.Out.WriteLineAsync("Displays the contents of a text file or files.\r\n\r\nTYPE [drive:][path]filename");
            return 0;
        }

        var raw = arguments.FullArgument.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            await console.Error.WriteLineAsync("The syntax of the command is incorrect.");
            return 1;
        }

        // Strip surrounding quotes
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
            raw = raw[1..^1];

        var path = ResolvePath(raw, context);
        if (!await context.FileSystem.FileExistsAsync(path))
        {
            await console.Error.WriteLineAsync($"The system cannot find the file specified.");
            return 1;
        }

        var content = await context.FileSystem.ReadAllTextAsync(path);
        // Normalise line endings to \r\n and ensure a trailing newline
        content = content.Replace("\r\n", "\n").Replace("\r", "\n");
        if (!content.EndsWith('\n'))
            content += '\n';
        content = content.Replace("\n", "\r\n");
        await console.Out.WriteAsync(content);
        return 0;
    }

    private static BatPath ResolvePath(string filePath, IContext context)
    {
        var drive = context.CurrentDrive;
        var pathPart = filePath;

        if (filePath.Length >= 2 && char.IsLetter(filePath[0]) && filePath[1] == ':')
        {
            drive = char.ToUpperInvariant(filePath[0]);
            pathPart = filePath.Length > 2 ? filePath[2..] : "";
        }

        if (pathPart.StartsWith('\\') || pathPart.StartsWith('/'))
        {
            var segs = pathPart.TrimStart('\\', '/').Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
            return new BatPath(drive, segs);
        }

        var current = new List<string>(context.GetPathForDrive(drive));
        foreach (var part in pathPart.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "..") { if (current.Count > 0) current.RemoveAt(current.Count - 1); }
            else if (part != ".") current.Add(part);
        }
        return new BatPath(drive, [.. current]);
    }
}
