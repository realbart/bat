#pragma warning disable CS8892, IDE0060
using Bat.Console;
using Bat.Execution;
using Context;

namespace Cmd;

public static class Program
{
    private static string BannerText =>
        $"🦇Cmd [Version {typeof(Program).Assembly.GetName().Version}]\r\n(c) Bart Kemps. Released under GPLv3+.\r\n";

    internal const string HelpText =
        """
        Starts a new instance of the command interpreter.

        CMD [/C | /K] [/Q] [/V:ON | /V:OFF] [/E:ON | /E:OFF] string

          /C      Carries out the command specified by string and then terminates.
          /K      Carries out the command specified by string but remains.
          /Q      Turns echo off.
          /?      Displays this help message.
        """;

    public static Task<int> Main(IContext context, IArgumentSet args) =>
        RunAsync(context, args.FullArgument.Trim());

    public static Task<int> Main(IContext context, string[] args) =>
        RunAsync(context, string.Join(" ", args));

    public static Task<int> Main(IContext context, string commandLine) =>
        RunAsync(context, commandLine);

    private static async Task<int> RunAsync(IContext context, string full)
    {
        var repl = new Repl(new Dispatcher());

        string? command = null;
        var exitAfter = false;

        var i = 0;
        while (i < full.Length)
        {
            if (full[i] == '/' && i + 1 < full.Length)
            {
                var flag = char.ToUpperInvariant(full[i + 1]);
                switch (flag)
                {
                    case 'C':
                        exitAfter = true;
                        command = full[(i + 2)..].TrimStart();
                        i = full.Length;
                        continue;
                    case 'K':
                        exitAfter = false;
                        command = full[(i + 2)..].TrimStart();
                        i = full.Length;
                        continue;
                    case 'Q':
                        context.EchoEnabled = false;
                        i += 2;
                        while (i < full.Length && full[i] == ' ') i++;
                        continue;
                    case 'V':
                        if (HasOnOff(full, i + 2, out var vOn, out var vEnd))
                        { context.DelayedExpansion = vOn; i = vEnd; }
                        else i += 2;
                        while (i < full.Length && full[i] == ' ') i++;
                        continue;
                    case 'E':
                        if (HasOnOff(full, i + 2, out var eOn, out var eEnd))
                        { context.ExtensionsEnabled = eOn; i = eEnd; }
                        else i += 2;
                        while (i < full.Length && full[i] == ' ') i++;
                        continue;
                    case 'F':
                        if (HasOnOff(full, i + 2, out var fOn, out var fEnd))
                        { i = fEnd; }
                        else i += 2;
                        while (i < full.Length && full[i] == ' ') i++;
                        continue;
                }
            }
            break;
        }

        if (command != null)
        {
            var isBatch = command.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
                          command.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase);

            if (isBatch)
                await repl.ExecuteBatchAsync(context, command);
            else
                await repl.ExecuteCommandAsync(context, command);

            if (exitAfter)
                return context.ErrorCode;
        }

        // Show banner before starting REPL
        await context.Console.Out.WriteAsync(BannerText);

        await repl.StartAsync(context);
        return context.ErrorCode;
    }

    private static bool HasOnOff(string s, int pos, out bool isOn, out int endPos)
    {
        isOn = false;
        endPos = pos;
        if (pos >= s.Length || s[pos] != ':') return false;
        var rest = s[(pos + 1)..];
        if (rest.StartsWith("ON", StringComparison.OrdinalIgnoreCase))
        { isOn = true; endPos = pos + 3; return true; }
        if (rest.StartsWith("OFF", StringComparison.OrdinalIgnoreCase))
        { isOn = false; endPos = pos + 4; return true; }
        return false;
    }
}
#pragma warning restore CS8892, IDE0060
