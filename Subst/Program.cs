#pragma warning disable CS8892, IDE0060
using Context;

namespace Subst;

[Command(Flags = "D")]
public static class Program
{
    private const string HelpText =
        """
        Associates a path with a drive letter.

        SUBST [drive1: [drive2:]path]
        SUBST drive1: /D

          drive1:        Specifies a virtual drive to which you want to assign a path.
          [drive2:]path  Specifies a physical drive and path you want to assign to
                         a virtual drive.
          /D             Deletes a substituted (virtual) drive.

        Type SUBST with no parameters to display a list of current virtual drives.
        """;

    public static Task<int> Main(IContext context, IArgumentSet args) =>
        Main(context, args, context.Console.Out);

    public static async Task<int> Main(IContext context, IArgumentSet args, TextWriter output)
    {
        if (args.IsHelpRequest) { await output.WriteLineAsync(HelpText); return 0; }

        var deleteFlag = args.GetFlagValue('D');

        if (args.Positionals.Count == 0)
            return await ListSubsts(context, output);

        var driveArg = args.Positionals[0];
        if (driveArg.Length == 0 || driveArg[0] == ':')
        {
            await output.WriteLineAsync($"Invalid parameter - {driveArg}");
            return 1;
        }
        var drive = char.ToUpperInvariant(driveArg[0]);

        if (deleteFlag)
            return await DeleteSubst(context, drive, driveArg, output);

        if (args.Positionals.Count < 2)
        {
            await output.WriteLineAsync($"Invalid parameter - {driveArg}");
            return 1;
        }

        return await AssignSubst(context, drive, driveArg, args.Positionals[1], output);
    }

    private static async Task<int> ListSubsts(IContext context, TextWriter output)
    {
        foreach (var kvp in context.FileSystem.GetSubsts().OrderBy(k => k.Key))
            await output.WriteLineAsync($"{kvp.Key}:\\: => {kvp.Value}");
        return 0;
    }

    private static async Task<int> DeleteSubst(IContext context, char drive, string driveArg, TextWriter output)
    {
        if (!context.FileSystem.GetSubsts().ContainsKey(drive))
        {
            await output.WriteLineAsync($"Invalid parameter - {char.ToUpperInvariant(driveArg[0])}:");
            return 1;
        }
        context.FileSystem.RemoveSubst(drive);
        return 0;
    }

    private static async Task<int> AssignSubst(IContext context, char drive, string driveArg, string pathArg, TextWriter output)
    {
        if (context.FileSystem.GetSubsts().ContainsKey(drive))
        {
            await output.WriteLineAsync("Drive already SUBSTed");
            return 1;
        }

        var (targetDrive, targetSegments) = ParseDosPath(pathArg, context);
        if (!await context.FileSystem.DirectoryExistsAsync(targetDrive, targetSegments))
        {
            await output.WriteLineAsync($"Path not found - {pathArg}");
            return 1;
        }

        var batPath = targetSegments.Length == 0
            ? $"{targetDrive}:\\"
            : $"{targetDrive}:\\{string.Join("\\", targetSegments)}";
        context.FileSystem.AddSubst(drive, batPath);
        return 0;
    }

    private static (char Drive, string[] Segments) ParseDosPath(string path, IContext context)
    {
        var drive = context.CurrentDrive;
        var rest = path;

        if (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':')
        {
            drive = char.ToUpperInvariant(path[0]);
            rest = path[2..];
        }

        string[] raw;
        if (rest.StartsWith('\\') || rest.StartsWith('/'))
            raw = rest.TrimStart('\\', '/').Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        else if (rest.Length == 0)
            raw = context.GetPathForDrive(drive);
        else
            raw = [..context.GetPathForDrive(drive), ..rest.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)];

        var segs = new List<string>();
        foreach (var seg in raw)
        {
            if (seg == ".") continue;
            if (seg == ".." && segs.Count > 0) { segs.RemoveAt(segs.Count - 1); continue; }
            if (seg != "..") segs.Add(seg);
        }
        return (drive, [..segs]);
    }
}
#pragma warning restore CS8892, IDE0060
