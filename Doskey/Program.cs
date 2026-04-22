#pragma warning disable CS8892, IDE0060
using Context;

namespace Doskey;

public static class Program
{
    private const string HelpText =
        """
        Edits command lines, recalls Windows commands, and creates macros.

        DOSKEY [/REINSTALL] [/LISTSIZE=size] [/MACROS[:ALL | :exename]]
          [/HISTORY] [/INSERT | /OVERSTRIKE] [/EXENAME=exename] [/MACROFILE=filename]
          [macroname=[text]]

          /REINSTALL          Installs a new copy of Doskey.
          /LISTSIZE=size      Sets size of command history buffer.
          /MACROS             Displays all Doskey macros.
          /MACROS:ALL         Displays all Doskey macros for all executables which have
                              Doskey macros.
          /MACROS:exename     Displays all Doskey macros for the given executable.
          /HISTORY            Displays all commands stored in memory.
          /INSERT             Specifies that new text you type is inserted in old text.
          /OVERSTRIKE         Specifies that new text overwrites old text.
          /EXENAME=exename    Specifies the executable.
          /MACROFILE=filename Specifies a file of macros to install.
          macroname           Specifies a name for a macro you create.
          text                Specifies commands you want to record.

        UP and DOWN ARROWS recall commands; ESC clears command line; F7 displays
        command history; ALT+F7 clears command history; F8 searches command
        history; F9 selects a command by number; ALT+F10 clears macro definitions.

        The following are some special codes in Doskey macro definitions:
        $T     Command separator.  Allows multiple commands in a macro.
        $1-$9  Batch parameters.  Equivalent to %1-%9 in batch programs.
        $*     Symbol replaced by everything following macro name on command line.
        """;

    public static Task<int> Main(IContext context, IArgumentSet args) =>
        Main(context, args, context.Console.Out);

    public static async Task<int> Main(IContext context, IArgumentSet args, TextWriter output)
    {
        if (args.IsHelpRequest) { await output.WriteLineAsync(HelpText); return 0; }

        var full = args.FullArgument;
        var trimmed = full.TrimStart();

        // Macro definition: first token contains '=' and doesn't start with '/'
        if (!string.IsNullOrEmpty(trimmed) && trimmed[0] != '/')
        {
            var firstSpace = trimmed.IndexOf(' ');
            var firstToken = firstSpace < 0 ? trimmed : trimmed[..firstSpace];
            var eqIdx = firstToken.IndexOf('=');
            if (eqIdx > 0)
            {
                var name = firstToken[..eqIdx];
                var body = trimmed[(trimmed.IndexOf('=') + 1)..];
                if (string.IsNullOrEmpty(body))
                    context.Macros.Remove(name);
                else
                    context.Macros[name] = body;
                return 0;
            }
        }

        // Parse flags from positionals (with ArgumentSpec.Empty, multi-char switches land there)
        var showHistory = false;
        var showMacros = false;
        string? listSizeStr = null;
        string? macroFile = null;

        foreach (var word in args.Positionals)
        {
            if (word.Length < 2 || (word[0] != '/' && word[0] != '-')) continue;
            var flag = word[1..].ToUpperInvariant();
            if (flag == "HISTORY")
                showHistory = true;
            else if (flag == "MACROS" || flag.StartsWith("MACROS:"))
                showMacros = true;
            else if (flag.StartsWith("LISTSIZE="))
                listSizeStr = flag["LISTSIZE=".Length..];
            else if (flag.StartsWith("MACROFILE="))
                macroFile = word[("/MACROFILE=".Length)..];
            else if (flag is "REINSTALL" or "INSERT" or "OVERSTRIKE")
            { }
            else
            {
                await output.WriteLineAsync("Invalid macro definition.");
                return 1;
            }
        }

        if (listSizeStr != null)
        {
            if (int.TryParse(listSizeStr, out var newSize) && newSize > 0)
            {
                context.HistorySize = newSize;
                while (context.CommandHistory.Count > newSize)
                    context.CommandHistory.RemoveAt(0);
            }
        }

        if (macroFile != null)
            await LoadMacroFileAsync(context, macroFile, output);

        if (showHistory)
        {
            foreach (var entry in context.CommandHistory)
                await output.WriteLineAsync(entry);
        }

        if (showMacros)
        {
            foreach (var kvp in context.Macros.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                await output.WriteLineAsync($"{kvp.Key}={kvp.Value}");
        }

        return 0;
    }

    private static async Task LoadMacroFileAsync(IContext context, string filePath, TextWriter output)
    {
        var (drive, segments) = ParsePath(filePath, context);
        if (!await context.FileSystem.FileExistsAsync(drive, segments))
        {
            await output.WriteLineAsync($"The system cannot find the file specified.");
            return;
        }

        var content = await context.FileSystem.ReadAllTextAsync(drive, segments);
        foreach (var line in content.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            var eqIdx = line.IndexOf('=');
            if (eqIdx <= 0) continue;
            var name = line[..eqIdx].Trim();
            var body = line[(eqIdx + 1)..];
            if (string.IsNullOrEmpty(body))
                context.Macros.Remove(name);
            else
                context.Macros[name] = body;
        }
    }

    private static (char Drive, string[] Segments) ParsePath(string path, IContext context)
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
            raw = [.. context.GetPathForDrive(drive), .. rest.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)];

        var segs = new List<string>();
        foreach (var seg in raw)
        {
            if (seg == ".") continue;
            if (seg == ".." && segs.Count > 0) { segs.RemoveAt(segs.Count - 1); continue; }
            if (seg != "..") segs.Add(seg);
        }
        return (drive, [.. segs]);
    }
}
#pragma warning restore CS8892, IDE0060
