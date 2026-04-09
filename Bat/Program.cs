#pragma warning disable CS0028
#pragma warning disable IDE0060
using Bat.Console;
using Bat.Context;
using Context;

namespace Bat.Tokens;

public static class Program
{
    internal static IRepl Repl { get; } = new Repl(new Console.Console(), new Dispatcher());

    private static readonly string BannerText =
        $"""
        🦇Bat [Version {typeof(Program).Assembly.GetName().Version}]
        (c) Bart Kemps. Released under GPLv3+.

        """;

    private static string GetHelp() => GenerateHelp(ContextFactory.IsWindows);

    private static readonly (string Win, string Unix, string Description)[] HelpFlags =
    [
        ("/?", "-h, --help", "Display this help message."),
        ("/C string", "-c string", "Carries out the command specified by string and then terminates."),
        ("/K string", "-k string", "Carries out the command specified by string but remains."),
        ("/N", "-n, --nologo", "Suppress startup banner."),
        ("/Q", "-q", "Turns echo off."),
        ("/D", "-d", "Disable execution of AutoRun commands (ignored in BAT)."),
        ("/E:ON", "-e:on", "Enable command extensions (default)."),
        ("/E:OFF", "-e:off", "Disable command extensions."),
        ("/F:ON", "-f:on", "Enable file and directory name completion characters."),
        ("/F:OFF", "-f:off", "Disable file and directory name completion characters (default)."),
        ("/V:ON", "-v:on", """
            Enable delayed environment variable expansion using ! as the
            delimiter. For example, /V:ON would allow !var! to expand
            the variable var at execution time.
            """),
        ("/V:OFF", "-v:off", "Disable delayed environment expansion (default)."),
        ("/M:X path", "-m X path", "{MAP_DESC}"),
        ("/T:fg", "-t:fg", "Sets the foreground/background colors (see COLOR /?)."),
        ("/A", "-a", """
            Causes the output of internal commands to a pipe or file to
            be ANSI.
            """),
        ("/U", "-u", """
            Causes the output of internal commands to a pipe or file to
            be Unicode.
            """),
        ("filename", "filename", "Execute batch file then terminate.")
    ];

    private static string GenerateHelp(bool isWindows)
    {
        var exe = isWindows ? "BAT" : "bat";
        var mFlag = isWindows ? "/M" : "-m";
        var defaultMapping = isWindows ? @"Z: -> C:\" : "Z: -> /";

        var syntax = isWindows
            ? "BAT [/? | /N] [/A | /U] [/Q] [/D] [/E:ON | /E:OFF] [/F:ON | /F:OFF]\n    [/M:X path ...] [/V:ON | /V:OFF] [/T:fg]\n    [[/C | /K] string | filename]"
            : "bat [-h | --help | -n | --nologo] [-a | -u] [-q] [-d] [-e:on | -e:off]\n    [-f:on | -f:off] [-m X path ...] [-v:on | -v:off] [-t:fg]\n    [[-c | -k] string | filename]";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Starts {exe} command interpreter with virtual drive mappings.\n");
        sb.AppendLine(syntax);
        sb.AppendLine();

        var maxSyntaxLen = HelpFlags.Max(f => (isWindows ? f.Win : f.Unix).Length);
        foreach (var (win, unix, desc) in HelpFlags)
        {
            var descText = desc.Replace("{MAP_DESC}",
                $"Map virtual drive X: to native path. First {mFlag} replaces default.\n            Without {mFlag}, default mapping is {defaultMapping}.");

            var syntaxPart = (isWindows ? win : unix).PadRight(maxSyntaxLen + 2);
            var lines = descText.Split('\n');
            sb.AppendLine($"  {syntaxPart}{lines[0]}");
            for (var i = 1; i < lines.Length; i++)
                sb.AppendLine($"  {new string(' ', maxSyntaxLen + 2)}{lines[i]}");
        }

        sb.AppendLine();
        if (!isWindows)
            sb.AppendLine("Flags without : may be combined: -cq = -c -q\n");

        sb.AppendLine("Note: Multiple commands separated by && are accepted for string if");
        sb.AppendLine("surrounded by quotes.\n");
        sb.AppendLine("Examples:");
        var p = isWindows ? (Func<string, string>)(s => $"/{s.ToUpperInvariant()}") : (s => s.Contains("nologo") ? "--nologo" : $"-{s}");
        sb.AppendLine($"  {exe} {p("m")} C {(isWindows ? @"C:\Projects" : "/")} {p("m")} D {(isWindows ? @"D:\Data" : "/home/user")}");
        sb.AppendLine($"  {exe} {p("c")} \"echo hello && echo world\"");
        sb.AppendLine($"  {exe} {p("k")}q");
        sb.AppendLine($"  {exe} {(isWindows ? @"Z:\AUTOEXEC.BAT" : "script.sh")}");

        return sb.ToString();
    }

    public static Task<int> Main(params string[] args)
    {
        var isWindows = ContextFactory.IsWindows;
        var dirSeparator = isWindows ? '\\' : '/';

        var parser = new BatArgumentParser(dirSeparator);
        var batArgs = parser.Parse(args);

        if (batArgs.ShowHelp)
        {
            System.Console.Write(GetHelp());
            return Task.FromResult(0);
        }

        if (!batArgs.SuppressBanner)
            System.Console.Write(BannerText);

        var context = ContextFactory.CreateContext();
        context.DelayedExpansion = batArgs.DelayedExpansion;
        context.ExtensionsEnabled = batArgs.ExtensionsEnabled;
        context.EchoEnabled = batArgs.EchoEnabled;
        return Main(context, batArgs);
    }

    public static async Task<int> Main(IContext context, BatArguments batArgs)
    {
        if (!batArgs.EchoEnabled)
            context.EchoEnabled = false;

        if (batArgs.Command != null)
        {
            var isBatchFile = batArgs.Command.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
                              batArgs.Command.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase);

            if (isBatchFile)
            {
                await Repl.ExecuteBatchAsync(context, batArgs.Command);
            }
            else
            {
                await Repl.ExecuteCommandAsync(context, batArgs.Command);
            }

            if (batArgs.ExitBehavior == BatExitBehavior.TerminateAfterCommand)
                return context.ErrorCode;
        }

        if (batArgs.BatchFile != null)
        {
            await Repl.ExecuteBatchAsync(context, batArgs.BatchFile);
            return context.ErrorCode;
        }

        await Repl.StartAsync(context);
        return context.ErrorCode;
    }

    public static Task<int> Main(IContext context, params string[] args)
    {
        var parser = new BatArgumentParser(ContextFactory.IsWindows ? '\\' : '/');
        return Main(context, parser.Parse(args));
    }
}