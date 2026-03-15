using Bat.Protocol.Client;
using Bat.Protocol.Models;

var handshake = Environment.GetEnvironmentVariable("DOS_HANDSHAKE");

if (!string.IsNullOrEmpty(handshake))
{
    var client = new DosProtocolClient(Console.In, Console.Out);

    // Perform handshake
    if (await client.PerformHandshakeAsync(handshake))
    {
        // Parse arguments
        if (args.Length == 0)
        {
            await client.SendCommandAsync(new DosCommand("markup") { Text = "[green]Doskey connected to bat![/]\n" });
            return;
        }

        var handled = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i].ToLower();

            if (arg == "/reinstall")
            {
                await client.SendCommandAsync(new DosCommand("clear_history"));
                var response = await client.ReadResponseAsync();
                if (response?.Success == true)
                {
                    Console.WriteLine("Command history cleared.");
                }
                handled = true;
            }
            else if (arg == "/history" || arg == "/h")
            {
                await client.SendCommandAsync(new DosCommand("get_history"));
                var response = await client.ReadResponseAsync();
                if (response?.Success == true && response.History != null)
                {
                    foreach (var entry in response.History)
                    {
                        Console.WriteLine(entry);
                    }
                }
                handled = true;
            }
            else if (arg.StartsWith("/listsize="))
            {
                var sizeStr = arg.Substring(10);
                if (int.TryParse(sizeStr, out var size) && size >= 1 && size <= 999)
                {
                    await client.SendCommandAsync(new DosCommand("set_history_size") { Size = size });
                    var response = await client.ReadResponseAsync();
                    if (response?.Success == true)
                    {
                        Console.WriteLine($"History buffer size set to {size}.");
                    }
                }
                else
                {
                    Console.WriteLine("Invalid size. Must be between 1 and 999.");
                }
                handled = true;
            }
            else if (arg.StartsWith("/bufsize="))
            {
                var sizeStr = arg.Substring(9);
                var parts = sizeStr.Split('x');
                if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
                {
                    await client.SendCommandAsync(new DosCommand("resize_buffer") { Width = w, Height = h });
                    var response = await client.ReadResponseAsync();
                    if (response?.Success == true)
                    {
                        Console.WriteLine($"Console buffer resized to {w}x{h}.");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to resize buffer: {response?.Error}");
                    }
                }
                handled = true;
            }
            else if (arg == "/macros" || arg == "/m")
            {
                string? exename = null;
                if (i + 1 < args.Length && args[i + 1].StartsWith("/exename="))
                {
                    exename = args[i + 1].Substring(9);
                    i++;
                }

                await client.SendCommandAsync(new DosCommand("get_macros") { ExeName = exename });
                var response = await client.ReadResponseAsync();
                if (response?.Success == true && response.Macros != null)
                {
                    if (response.Macros.Count == 0)
                    {
                        Console.WriteLine("No macros defined.");
                    }
                    else
                    {
                        foreach (var macro in response.Macros)
                        {
                            Console.WriteLine($"{macro.Key}={macro.Value}");
                        }
                    }
                }
                handled = true;
            }
            else if (arg.Contains("=") && !arg.StartsWith("/"))
            {
                // Macro definition: name=text
                var parts = arg.Split('=', 2);
                if (parts.Length == 2)
                {
                    string? exename = null;
                    // Check if there's an /exename parameter before this
                    if (i > 0 && args[i - 1].StartsWith("/exename="))
                    {
                        exename = args[i - 1].Substring(9);
                    }

                    await client.SendCommandAsync(new DosCommand("set_macro")
                    {
                        MacroName = parts[0],
                        Text = parts[1],
                        ExeName = exename
                    });
                    var response = await client.ReadResponseAsync();
                    if (response?.Success == true)
                    {
                        Console.WriteLine($"Macro '{parts[0]}' defined.");
                    }
                }
                handled = true;
            }
            else if (arg.StartsWith("/exename="))
            {
                // This is handled together with /macros or macro definition
                continue;
            }
            else if (arg.StartsWith("/macrofile="))
            {
                var filename = arg.Substring(11);
                try
                {
                    if (File.Exists(filename))
                    {
                        var lines = File.ReadAllLines(filename);
                        var count = 0;

                        string? currentExeName = null;
                        foreach (var line in lines)
                        {
                            var trimmed = line.Trim();
                            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";"))
                                continue;

                            // Check for exename directive: [exename]
                            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                            {
                                currentExeName = trimmed.Substring(1, trimmed.Length - 2);
                                continue;
                            }

                            // Macro definition: name=text
                            var eqIndex = trimmed.IndexOf('=');
                            if (eqIndex > 0)
                            {
                                var macroName = trimmed.Substring(0, eqIndex).Trim();
                                var macroText = trimmed.Substring(eqIndex + 1).Trim();

                                await client.SendCommandAsync(new DosCommand("set_macro")
                                {
                                    MacroName = macroName,
                                    Text = macroText,
                                    ExeName = currentExeName
                                });
                                var response = await client.ReadResponseAsync();
                                if (response?.Success == true)
                                {
                                    count++;
                                }
                            }
                        }

                        Console.WriteLine($"Loaded {count} macro(s) from '{filename}'.");
                    }
                    else
                    {
                        Console.WriteLine($"File not found: {filename}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading macros: {ex.Message}");
                }
                handled = true;
            }
            else if (arg == "/?" || arg == "/help")
            {
                ShowHelp();
                handled = true;
            }
            else
            {
                await client.SendCommandAsync(new DosCommand("markup") { Text = $"[yellow]Unknown doskey option: {arg}[/]\n" });
                handled = true;
            }
        }

        if (!handled)
        {
            await client.SendCommandAsync(new DosCommand("markup") { Text = "[green]Doskey connected to bat![/]\n" });
        }
    }
}
else
{
    Console.WriteLine("DOS_HANDSHAKE not found. This application should be run from bat.");
}

void ShowHelp()
{
    Console.WriteLine("DOSKEY - Edits command lines, recalls DOS commands, and creates macros.");
    Console.WriteLine();
    Console.WriteLine("DOSKEY [/REINSTALL] [/LISTSIZE=size] [/MACROS[:exename]] [/HISTORY]");
    Console.WriteLine("       [/BUFSIZE=size] [macroname=[text]]");
    Console.WriteLine();
    Console.WriteLine("  /REINSTALL          Clears the command history buffer.");
    Console.WriteLine("  /LISTSIZE=size      Sets the size of the command history buffer (1-999).");
    Console.WriteLine("  /MACROS[:exename]   Displays all Doskey macros for the given executable.");
    Console.WriteLine("  /MACROS             Displays all Doskey macros for all executables.");
    Console.WriteLine("  /HISTORY            Displays all commands stored in memory.");
    Console.WriteLine("  /BUFSIZE=size       Sets the console buffer size (widthxheight).");
    Console.WriteLine("  macroname           Specifies a name for a macro you create.");
    Console.WriteLine("  text                Specifies commands you want to assign to the macro.");
    Console.WriteLine();
    Console.WriteLine("Special codes in Doskey macro definitions:");
    Console.WriteLine("  $T    Command separator (allows multiple commands in a macro)");
    Console.WriteLine("  $1-$9 Batch parameters (equivalent to %1-%9 in batch programs)");
    Console.WriteLine("  $*    Symbol replaced by everything following macro name on command line");
}
