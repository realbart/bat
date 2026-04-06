namespace Bat.Context;

internal class BatArgumentParser(char directorySeparator)
{
    private readonly BatMode _mode = directorySeparator == '\\' ? BatMode.Windows : BatMode.Unix;
    private readonly char _flagPrefix = directorySeparator == '\\' ? '/' : '-';

    public BatArguments Parse(string[] args)
    {
        var echoEnabled = true;
        var delayedExpansion = false;
        var extensionsEnabled = true;
        var filenameCompletion = false;
        var showHelp = false;
        var suppressBanner = false;
        var outputEncoding = OutputEncoding.Default;
        var exitBehavior = BatExitBehavior.Repl;
        string? command = null;
        string? batchFile = null;
        string? colorSpec = null;
        Dictionary<char, string>? driveMappings = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (IsHelpFlag(arg))
            {
                showHelp = true;
                continue;
            }

            if (!IsFlag(arg))
            {
                batchFile = arg;
                exitBehavior = BatExitBehavior.TerminateAfterCommand;
                continue;
            }

            var flagPart = arg[1..];
            if (_mode == BatMode.Unix && flagPart.Length > 1 && !flagPart.StartsWith('-'))
            {
                var parts = SplitCombinedFlags(flagPart);
                foreach (var part in parts)
                    ProcessSingleFlag(part, ref echoEnabled, ref delayedExpansion, ref extensionsEnabled,
                        ref filenameCompletion, ref suppressBanner, ref outputEncoding, ref exitBehavior, ref command,
                        ref batchFile, ref colorSpec, ref driveMappings, args, ref i);
                continue;
            }

            ProcessSingleFlag(flagPart, ref echoEnabled, ref delayedExpansion, ref extensionsEnabled,
                ref filenameCompletion, ref suppressBanner, ref outputEncoding, ref exitBehavior, ref command,
                ref batchFile, ref colorSpec, ref driveMappings, args, ref i);
        }

        return new BatArguments
        {
            Mode = _mode,
            ExitBehavior = exitBehavior,
            Command = command,
            BatchFile = batchFile,
            EchoEnabled = echoEnabled,
            DelayedExpansion = delayedExpansion,
            ExtensionsEnabled = extensionsEnabled,
            FilenameCompletion = filenameCompletion,
            ShowHelp = showHelp,
            SuppressBanner = suppressBanner,
            OutputEncoding = outputEncoding,
            DriveMappings = driveMappings,
            ColorSpec = colorSpec,
            NativeCwd = Environment.CurrentDirectory
        };
    }

    private void ProcessSingleFlag(string flag, ref bool echoEnabled, ref bool delayedExpansion,
        ref bool extensionsEnabled, ref bool filenameCompletion, ref bool suppressBanner,
        ref OutputEncoding outputEncoding, ref BatExitBehavior exitBehavior, ref string? command,
        ref string? batchFile, ref string? colorSpec, ref Dictionary<char, string>? driveMappings,
        string[] args, ref int i)
    {
        var key = flag.Split(':', 2)[0].ToUpperInvariant();
        var value = flag.Contains(':') ? flag.Split(':', 2)[1] : null;

        switch (key)
        {
            case "C":
                exitBehavior = BatExitBehavior.TerminateAfterCommand;
                if (i + 1 < args.Length) command = args[++i];
                break;
            case "K":
                exitBehavior = BatExitBehavior.KeepAliveAfterCommand;
                if (i + 1 < args.Length) command = args[++i];
                break;
            case "Q":
                echoEnabled = false;
                break;
            case "V":
                if (value?.Equals("ON", StringComparison.OrdinalIgnoreCase) == true)
                    delayedExpansion = true;
                else if (value?.Equals("OFF", StringComparison.OrdinalIgnoreCase) == true)
                    delayedExpansion = false;
                break;
            case "E":
                if (value?.Equals("ON", StringComparison.OrdinalIgnoreCase) == true)
                    extensionsEnabled = true;
                else if (value?.Equals("OFF", StringComparison.OrdinalIgnoreCase) == true)
                    extensionsEnabled = false;
                break;
            case "F":
                if (value?.Equals("ON", StringComparison.OrdinalIgnoreCase) == true)
                    filenameCompletion = true;
                else if (value?.Equals("OFF", StringComparison.OrdinalIgnoreCase) == true)
                    filenameCompletion = false;
                break;
            case "A":
                outputEncoding = OutputEncoding.Ansi;
                break;
            case "U":
                outputEncoding = OutputEncoding.Unicode;
                break;
            case "N":
            case "-NOLOGO":
                suppressBanner = true;
                break;
            case "M":
                driveMappings ??= [];
                var drive = value?[0] ?? (i + 1 < args.Length ? args[++i][0] : '\0');
                var path = i + 1 < args.Length ? args[++i] : "";
                if (drive != '\0' && char.IsLetter(drive))
                    driveMappings[char.ToUpperInvariant(drive)] = path;
                break;
            case "T":
                colorSpec = value ?? (i + 1 < args.Length ? args[++i] : null);
                break;
            case "D":
                break;
        }
    }

    private bool IsFlag(string arg) => arg.Length > 1 && arg[0] == _flagPrefix;

    private bool IsHelpFlag(string arg) =>
        arg == "/?" || arg == "-h" || arg == "--help";

    private static List<string> SplitCombinedFlags(string combined)
    {
        var result = new List<string>();
        var i = 0;
        while (i < combined.Length)
        {
            var start = i;
            i++;
            if (i < combined.Length && combined[i] == ':')
            {
                i++;
                while (i < combined.Length && !char.IsLetter(combined[i]))
                    i++;
            }
            result.Add(combined[start..i]);
        }
        return result;
    }
}
