using System.Threading;
using System.Threading.Tasks;
using Bat.Commands;
using Bat.FileSystem;
using Bat.Protocol.Models;
using Bat.Protocol.Server;
using Spectre.Console;

namespace Bat;

public class CommandDispatcher
{
    private readonly FileSystemService _fileSystem;
    private readonly Dictionary<string, ICommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _history = new();
    private int _historySize = 50;
    private readonly Dictionary<string, Dictionary<string, string>> _macros = new(StringComparer.OrdinalIgnoreCase);

    public CommandDispatcher(FileSystemService fileSystem)
    {
        _fileSystem = fileSystem;
        var historySizeVar = _fileSystem.GetEnvironmentVariable("DIR_HISTORY_SIZE");
        if (int.TryParse(historySizeVar, out var size) && size >= 1 && size <= 999)
        {
            _historySize = size;
        }
        RegisterCommand(new CdCommand());
        RegisterCommand(new ExitCommand());
        RegisterCommand(new DirCommand());
        RegisterCommand(new MdCommand());
        RegisterCommand(new RdCommand());
        RegisterCommand(new CopyCommand());
        RegisterCommand(new DelCommand());
        RegisterCommand(new RenCommand());
        RegisterCommand(new TypeCommand());
        RegisterCommand(new ClsCommand());
        RegisterCommand(new DateCommand());
        RegisterCommand(new TimeCommand());
        RegisterCommand(new VerCommand());
        RegisterCommand(new VolCommand());
        RegisterCommand(new EchoCommand());
        RegisterCommand(new MoveCommand());
        RegisterCommand(new PauseCommand());
        RegisterCommand(new RemCommand());
        RegisterCommand(new TitleCommand());
        RegisterCommand(new ColorCommand());
        RegisterCommand(new SetCommand());
        RegisterCommand(new PathCommand());
        RegisterCommand(new PromptCommand());
        RegisterCommand(new PushdCommand());
        RegisterCommand(new PopdCommand());
        RegisterCommand(new VerifyCommand());
        RegisterCommand(new BreakCommand());
        RegisterCommand(new AssocCommand());
        RegisterCommand(new FtypeCommand());
        RegisterCommand(new MklinkCommand());
        RegisterCommand(new IfCommand());
        RegisterCommand(new GotoCommand());
        RegisterCommand(new CallCommand());
        RegisterCommand(new ShiftCommand());
        RegisterCommand(new SetlocalCommand());
        RegisterCommand(new EndlocalCommand());
        RegisterCommand(new ForCommand());
        RegisterCommand(new StartCommand());
    }

    public IReadOnlyList<string> History => _history.AsReadOnly();

    private void AddToHistory(string input)
    {
        _history.Add(input);
        if (_history.Count > _historySize)
        {
            _history.RemoveAt(0);
        }
    }

    private void RegisterCommand(ICommand command)
    {
        _commands[command.Name] = command;
        foreach (var alias in command.Aliases)
        {
            _commands[alias] = command;
        }
    }

    private int _lastErrorLevel = 0;

    public async Task DispatchAsync(string input, CancellationToken cancellationToken = default, IAnsiConsole? consoleOverride = null)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        var baseConsole = consoleOverride ?? AnsiConsole.Console;

        // Check for @ prefix to suppress echo
        bool suppressEcho = input.StartsWith("@");
        string workingInput = suppressEcho ? input.Substring(1).TrimStart() : input;

        if (string.IsNullOrWhiteSpace(workingInput)) return;

        // DOS 'slimmigheid': cd.. en cd\ direct herkennen
        if (workingInput.StartsWith("cd..", StringComparison.OrdinalIgnoreCase))
        {
            workingInput = "cd ..";
        }
        else if (workingInput.StartsWith("cd\\", StringComparison.OrdinalIgnoreCase))
        {
            workingInput = "cd \\";
        }
        else if (workingInput.StartsWith("echo.", StringComparison.OrdinalIgnoreCase))
        {
            workingInput = "echo .";
        }

        // Echo command if not suppressed and echo is on
        if (!suppressEcho && _fileSystem.EchoOn && consoleOverride != null)
        {
            // Only echo if we are in a batch file (indicated by consoleOverride being passed from ExecuteBatchFileAsync)
            baseConsole.WriteLine(workingInput);
        }

        AddToHistory(workingInput);

        var parts = ParseInput(workingInput);
        if (parts.Length == 0) return;

        // Variabele expansie
        parts = parts.Select(ExpandVariables).ToArray();

        // Redirectie detectie (simpele vorm: > of >> aan het eind of gevolgd door pad)
        string? redirectionPath = null;
        var append = false;
        var commandParts = new List<string>();

        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i] == ">")
            {
                if (i + 1 < parts.Length)
                {
                    redirectionPath = parts[i + 1];
                    i++; // Sla het pad over
                }
            }
            else if (parts[i] == ">>")
            {
                if (i + 1 < parts.Length)
                {
                    redirectionPath = parts[i + 1];
                    append = true;
                    i++;
                }
            }
            else if (parts[i].Contains(">"))
            {
                // Bijv. echo hi>file.txt
                var isAppend = parts[i].Contains(">>");
                var subParts = parts[i].Split(isAppend ? new[] { ">>" } : new[] { ">" }, StringSplitOptions.None);
                if (subParts.Length > 0 && !string.IsNullOrEmpty(subParts[0]))
                {
                    commandParts.Add(subParts[0]);
                }
                
                if (subParts.Length > 1)
                {
                    redirectionPath = subParts[1];
                    append = isAppend;
                }
            }
            else
            {
                commandParts.Add(parts[i]);
            }
        }

        if (commandParts.Count == 0) return;

        var commandName = commandParts[0];
        var args = commandParts.Skip(1).ToArray();

        // Check for macro expansion
        var exeName = "bat"; // Voor nu altijd bat, later evt per executable
        if (_macros.TryGetValue(exeName, out var macroDict) &&
            macroDict.TryGetValue(commandName, out var macroText))
        {
            // Expand macro
            var expandedCommand = ExpandMacro(macroText, args);

            // Execute expanded command(s)
            var macroCommands = expandedCommand.Split(new[] { "$T", "$t" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var cmd in macroCommands)
            {
                await DispatchAsync(cmd.Trim(), cancellationToken, consoleOverride);
            }
            return;
        }

        var console = baseConsole;
        IDisposable? redirectDisposable = null;

        if (!string.IsNullOrEmpty(redirectionPath))
        {
            var resolvedPath = _fileSystem.ResolvePath(redirectionPath);
            try
            {
                var stream = _fileSystem.FileSystem.File.Open(resolvedPath, append ? FileMode.Append : FileMode.Create, FileAccess.Write);
                var writer = new StreamWriter(stream);
                writer.AutoFlush = true;
                
                // We gebruiken een Recorder of een andere manier om de output op te vangen.
                // Spectre.Console heeft geen directe "redirect naar stream" zonder IAnsiConsole te wrappen.
                // Voor nu simuleren we het simpel door een nieuwe IAnsiConsole te maken of de tekst te capturen.
                // Maar de commando's gebruiken de meegegeven 'console'.
                
                // Een simpele wrapper console voor redirectie:
                console = CreateRedirectConsole(writer);
                redirectDisposable = writer;
            }
            catch (Exception ex)
            {
                baseConsole.MarkupLine($"[red]The system cannot find the path specified: {ex.Message}[/]");
                return;
            }
        }

        try
        {
            if (_commands.TryGetValue(commandName, out var command))
            {
                await command.ExecuteAsync(args, _fileSystem, console, cancellationToken);
                _lastErrorLevel = 0; // Internal commands currently don't return exit code, assume 0
            }
            else if (commandName.EndsWith(":") && commandName.Length == 2 && char.IsLetter(commandName[0]))
            {
                // Drive change (e.g., D:)
                var drive = commandName.ToUpper();
                if (drive == "C:")
                {
                    _fileSystem.ChangeDirectory("C:\\");
                    _lastErrorLevel = 0;
                }
                else
                {
                    var substPath = _fileSystem.GetSubstPath(drive);
                    if (substPath != null)
                    {
                        _fileSystem.ChangeDirectory(drive + "\\");
                        _lastErrorLevel = 0;
                    }
                    else
                    {
                        console.MarkupLine($"[red]The system cannot find the drive specified.[/]");
                        _lastErrorLevel = 1;
                    }
                }
            }
            else if (commandName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) || 
                     commandName.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
            {
                var fullPath = _fileSystem.ResolvePath(commandName);
                if (_fileSystem.FileSystem.File.Exists(fullPath))
                {
                    await ExecuteBatchFileAsync(fullPath, args, console, cancellationToken);
                }
                else
                {
                    console.MarkupLine($"[red]The system cannot find the file specified: {commandName}[/]");
                    _lastErrorLevel = 1;
                }
            }
            else
            {
                var exePath = _fileSystem.FindExecutable(commandName);
                if (exePath != null)
                {
                    if (exePath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) || 
                        exePath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
                    {
                        await ExecuteBatchFileAsync(exePath, args, console, cancellationToken);
                    }
                    else
                    {
                        await TryExecuteExternalApplicationAsync(exePath, args, console, cancellationToken);
                    }
                }
                else
                {
                    console.MarkupLine($"'{commandName}' is not recognized as an internal or external command, operable program or batch file.");
                    _lastErrorLevel = 9009; // Standard CMD error for command not found
                }
            }
        }
        finally
        {
            redirectDisposable?.Dispose();
        }
    }

    private async Task ExecuteBatchFileAsync(string path, string[] args, IAnsiConsole console, CancellationToken cancellationToken)
    {
        var lines = await _fileSystem.FileSystem.File.ReadAllLinesAsync(path, cancellationToken);
        
        // Pre-parse labels
        var labels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < lines.Length; i++)
        {
            var trimmedLine = lines[i].Trim();
            if (trimmedLine.StartsWith(":") && !trimmedLine.StartsWith("::"))
            {
                var labelName = trimmedLine.Substring(1).Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (labelName != null)
                {
                    labels[labelName] = i;
                }
            }
        }

        int currentLineIndex = 0;
        while (currentLineIndex < lines.Length)
        {
            if (cancellationToken.IsCancellationRequested) break;
            
            var line = lines[currentLineIndex];
            currentLineIndex++;

            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine)) continue;
            
            // Labels and comments
            if (trimmedLine.StartsWith(":") || trimmedLine.StartsWith("REM", StringComparison.OrdinalIgnoreCase)) continue;

            // Skip polyglot shebang
            if (trimmedLine.Contains("#!")) continue;

            // Parameter substitutie (%0-%9)
            var processedLine = trimmedLine;
            processedLine = processedLine.Replace("%0", path);
            for (int i = 0; i < args.Length && i < 9; i++)
            {
                processedLine = processedLine.Replace($"%{i + 1}", args[i]);
            }
            // Lege parameters verwijderen
            for (int i = args.Length; i < 9; i++)
            {
                processedLine = processedLine.Replace($"%{i + 1}", "");
            }

            // GOTO handling
            if (processedLine.StartsWith("GOTO", StringComparison.OrdinalIgnoreCase))
            {
                var gotoParts = processedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (gotoParts.Length > 1)
                {
                    var targetLabel = gotoParts[1];
                    if (targetLabel.StartsWith(":")) targetLabel = targetLabel.Substring(1);
                    
                    if (targetLabel.Equals("EOF", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    if (labels.TryGetValue(targetLabel, out var labelIndex))
                    {
                        currentLineIndex = labelIndex + 1;
                        continue;
                    }
                    else
                    {
                        console.MarkupLine($"[red]Label not found: {targetLabel}[/]");
                        continue;
                    }
                }
            }

            // IF handling
            if (processedLine.ToUpper().StartsWith("IF ") || processedLine.ToUpper().StartsWith("@IF "))
            {
                bool suppressIfEcho = processedLine.ToUpper().StartsWith("@IF ");
                string workingIfLine = suppressIfEcho ? processedLine.Substring(1).TrimStart() : processedLine;

                var (conditionMet, commands, nextIndex) = ParseIfCondition(workingIfLine, currentLineIndex, lines);
                currentLineIndex = nextIndex;

                if (_fileSystem.EchoOn && !suppressIfEcho)
                {
                    console.WriteLine(workingIfLine);
                }

                if (conditionMet)
                {
                    foreach (var cmd in commands)
                    {
                        await DispatchAsync(cmd, cancellationToken, console);
                    }
                }
                continue;
            }

            // Voer regel uit (zonder recursie naar deze functie via DispatchAsync)
            // Maar we gebruiken DispatchAsync om interne commando's en redirecties te ondersteunen.
            await DispatchAsync(processedLine, cancellationToken, console);
        }
    }

    private (bool conditionMet, List<string> commands, int nextIndex) ParseIfCondition(string line, int currentLineIndex, string[] allLines)
    {
        var parts = ParseInput(line);
        if (parts.Length < 2 || parts[0].ToUpper() != "IF")
            return (false, new List<string>(), currentLineIndex);

        bool isNot = false;
        int index = 1;
        if (parts[index].Equals("NOT", StringComparison.OrdinalIgnoreCase))
        {
            isNot = true;
            index++;
        }

        bool conditionMet = false;
        if (index >= parts.Length) return (false, new List<string>(), currentLineIndex);

        if (parts[index].Equals("ERRORLEVEL", StringComparison.OrdinalIgnoreCase))
        {
            index++;
            if (index < parts.Length && int.TryParse(parts[index], out int level))
            {
                conditionMet = _lastErrorLevel >= level;
                index++;
            }
        }
        else if (parts[index].Equals("DEFINED", StringComparison.OrdinalIgnoreCase))
        {
            index++;
            if (index < parts.Length)
            {
                var varName = parts[index];
                conditionMet = !string.IsNullOrEmpty(_fileSystem.GetEnvironmentVariable(varName));
                index++;
            }
        }
        else if (parts[index].Equals("EXIST", StringComparison.OrdinalIgnoreCase))
        {
            index++;
            if (index < parts.Length)
            {
                var path = _fileSystem.ResolvePath(parts[index]);
                conditionMet = _fileSystem.FileSystem.File.Exists(path) || _fileSystem.FileSystem.Directory.Exists(path);
                index++;
            }
        }
        else if (parts[index].Contains("=="))
        {
            var comparison = parts[index].Split(new[] { "==" }, StringSplitOptions.None);
            if (comparison.Length == 2)
            {
                conditionMet = comparison[0].Equals(comparison[1], StringComparison.OrdinalIgnoreCase);
                index++;
            }
        }
        else if (index + 2 < parts.Length && parts[index+1] == "==")
        {
             conditionMet = parts[index].Equals(parts[index+2], StringComparison.OrdinalIgnoreCase);
             index += 3;
        }

        if (isNot) conditionMet = !conditionMet;

        var commands = new List<string>();
        int nextIndex = currentLineIndex + 1;

        // Check if it's a block with (
        var remaining = string.Join(" ", parts.Skip(index)).Trim();
        if (remaining.StartsWith("("))
        {
            // Collect lines until )
            int parenCount = 1; // Already have one (
            bool inBlock = true;
            while (nextIndex < allLines.Length && inBlock)
            {
                var blockLine = allLines[nextIndex].Trim();
                nextIndex++;
                if (blockLine == ")")
                {
                    parenCount--;
                    if (parenCount == 0)
                    {
                        inBlock = false;
                    }
                }
                else if (blockLine.StartsWith("("))
                {
                    parenCount++;
                }
                else if (!string.IsNullOrWhiteSpace(blockLine))
                {
                    commands.Add(blockLine);
                }
            }
        }
        else
        {
            // Single command
            commands.Add(remaining);
        }

        // Handle ELSE
        if (nextIndex < allLines.Length)
        {
            var nextLine = allLines[nextIndex].Trim();
            if (nextLine.StartsWith("ELSE", StringComparison.OrdinalIgnoreCase))
            {
                nextIndex++;
                var elseRemaining = nextLine.Substring(4).Trim();
                if (elseRemaining.StartsWith("("))
                {
                    // Else block
                    int parenCount = 1;
                    bool inBlock = true;
                    var elseCommands = new List<string>();
                    while (nextIndex < allLines.Length && inBlock)
                    {
                        var blockLine = allLines[nextIndex].Trim();
                        nextIndex++;
                        if (blockLine == ")")
                        {
                            parenCount--;
                            if (parenCount == 0)
                            {
                                inBlock = false;
                            }
                        }
                        else if (blockLine.StartsWith("("))
                        {
                            parenCount++;
                        }
                        else if (!string.IsNullOrWhiteSpace(blockLine))
                        {
                            elseCommands.Add(blockLine);
                        }
                    }
                    if (!conditionMet)
                    {
                        commands = elseCommands;
                        conditionMet = true;
                    }
                }
                else
                {
                    // Single else command
                    if (!conditionMet)
                    {
                        commands = new List<string> { elseRemaining };
                        conditionMet = true;
                    }
                }
            }
        }

        return (conditionMet, commands, nextIndex);
    }

    private async Task TryExecuteExternalApplicationAsync(string exePath, string[] args, IAnsiConsole console, CancellationToken cancellationToken)
    {
        var handshake = Guid.NewGuid().ToString("N").Substring(0, 8);
        var pipeName = "bat-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        
        // Handshake: proberen voor programma's in de bat directory OF .NET assemblies
        var batDir = _fileSystem.GetBatDirectory();
        var fullExePath = Path.GetFullPath(exePath);
        var isInBatDir = fullExePath.StartsWith(batDir, StringComparison.OrdinalIgnoreCase);
        var isDotNet = _fileSystem.IsDotNetAssembly(exePath);
        
        var shouldTryHandshake = isInBatDir || isDotNet;

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = exePath,
            Arguments = string.Join(" ", args.Select(a => a.Contains(" ") ? $"\"{a}\"" : a)),
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            UseShellExecute = false, // Altijd false om WorkingDirectory en Environment te kunnen zetten
            CreateNoWindow = false,
            WorkingDirectory = _fileSystem.GetLinuxPath(_fileSystem.CurrentDirectory)
        };

        // Check for shebang
        var shebang = _fileSystem.GetShebang(exePath);
        if (shebang != null)
        {
            var parts = shebang.Split(' ', 2);
            startInfo.FileName = parts[0];
            var shebangArgs = parts.Length > 1 ? parts[1] + " " : "";
            startInfo.Arguments = shebangArgs + $"\"{exePath}\" " + startInfo.Arguments;
        }

        if (shouldTryHandshake)
        {
            startInfo.EnvironmentVariables["DOS_HANDSHAKE"] = handshake;
            startInfo.EnvironmentVariables["BAT_PIPE"] = pipeName;
        }

        // Voeg andere DOS omgevingsvariabelen toe
        foreach (var kvp in _fileSystem.GetAllEnvironmentVariables())
        {
            // Skip Rider-specific .NET startup hooks that cause issues with external .NET processes
            if (kvp.Key.Equals("DOTNET_STARTUP_HOOKS", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
        }

        // Als het een bekende shell is, gebruik de raw environment variabelen van het systeem
        var isShell = exePath.EndsWith("bash", StringComparison.OrdinalIgnoreCase) || 
                      exePath.EndsWith("pwsh", StringComparison.OrdinalIgnoreCase) ||
                      exePath.EndsWith("sh", StringComparison.OrdinalIgnoreCase);
        
        if (isShell)
        {
            var rawEnv = _fileSystem.GetRawEnvironmentVariables();
            foreach (var kvp in rawEnv)
            {
                startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
            }
        }

        try
        {
            if (shouldTryHandshake)
            {
                using var pipeServer = new System.IO.Pipes.NamedPipeServerStream(pipeName, System.IO.Pipes.PipeDirection.InOut, 1, System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous);
                
                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process == null) return;

                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var protocolServer = new DosProtocolServer();

                // Wait for connection, process exit, or timeout
                var connectTask = pipeServer.WaitForConnectionAsync(cts.Token);
                var processExitTask = process.WaitForExitAsync(cts.Token);
                var timeoutTask = Task.Delay(500, cts.Token);

                var completedTask = await Task.WhenAny(connectTask, processExitTask, timeoutTask);
                if (completedTask == connectTask)
                {
                    var handshakeResult = await protocolServer.WaitForHandshakeAsync(pipeServer, handshake, cts.Token);
                    if (handshakeResult.Success)
                    {
                        var protocolTask = protocolServer.ProcessStreamAsync(
                            pipeServer,
                            async command => await ProcessJsonCommandAsync(command, console, pipeServer, cts.Token),
                            cts.Token);

                        await process.WaitForExitAsync(cancellationToken);
                        cts.Cancel();
                        try { await protocolTask; } catch (OperationCanceledException) {}
                        _lastErrorLevel = process.ExitCode;
                    }
                    else
                    {
                        await process.WaitForExitAsync(cancellationToken);
                        _lastErrorLevel = process.ExitCode;
                    }
                }
                else if (completedTask == processExitTask)
                {
                    // Process exited before connecting or timeout
                    _lastErrorLevel = process.ExitCode;
                }
                else // timeout
                {
                    // No handshake, just wait for process exit
                    await process.WaitForExitAsync(cancellationToken);
                    _lastErrorLevel = process.ExitCode;
                }
            }
            else
            {
                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process == null) return;
                await process.WaitForExitAsync(cancellationToken);
                _lastErrorLevel = process.ExitCode;
            }
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]Error executing {exePath}: {ex.Message}[/]");
        }
    }

    private async Task ProcessJsonCommandAsync(DosCommand command, IAnsiConsole console, Stream responseStream, CancellationToken cancellationToken)
    {
        DosResponse? response = null;

        switch (command.Command.ToLower())
        {
            case "get_history":
                response = new DosResponse
                {
                    Command = "history_response",
                    Success = true,
                    History = _history.ToArray()
                };
                break;

            case "get_macros":
                var exeName = command.ExeName ?? "bat";
                if (_macros.TryGetValue(exeName, out var macros))
                {
                    response = new DosResponse
                    {
                        Command = "macros_response",
                        Success = true,
                        Macros = macros
                    };
                }
                else
                {
                    response = new DosResponse
                    {
                        Command = "macros_response",
                        Success = true,
                        Macros = new Dictionary<string, string>()
                    };
                }
                break;

            case "set_macro":
                if (!string.IsNullOrEmpty(command.MacroName) && !string.IsNullOrEmpty(command.Text))
                {
                    var exe = command.ExeName ?? "bat";
                    if (!_macros.ContainsKey(exe))
                    {
                        _macros[exe] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }
                    _macros[exe][command.MacroName] = command.Text;
                    response = new DosResponse { Command = "set_macro_response", Success = true };
                }
                break;

            case "clear_history":
                _history.Clear();
                response = new DosResponse { Command = "clear_history_response", Success = true };
                break;

            case "set_history_size":
                if (command.Size.HasValue && command.Size.Value >= 1 && command.Size.Value <= 999)
                {
                    _historySize = command.Size.Value;
                    while (_history.Count > _historySize)
                    {
                        _history.RemoveAt(0);
                    }
                    response = new DosResponse { Command = "set_history_size_response", Success = true };
                }
                break;

            case "get_subst":
                var substs = _fileSystem.GetAllSubsts();
                response = new DosResponse
                {
                    Command = "subst_list_response",
                    Success = true,
                    Macros = substs // Reuse Macros field for key-value pairs
                };
                break;

            case "set_subst":
                if (!string.IsNullOrEmpty(command.Text) && !string.IsNullOrEmpty(command.FileName))
                {
                    var result = _fileSystem.SetSubst(command.Text, command.FileName);
                    response = new DosResponse
                    {
                        Command = "set_subst_response",
                        Success = result.IsSuccess,
                        Error = result.IsSuccess ? null : result.ErrorMessage
                    };
                }
                break;

            case "delete_subst":
                if (!string.IsNullOrEmpty(command.Text))
                {
                    var result = _fileSystem.DeleteSubst(command.Text);
                    response = new DosResponse
                    {
                        Command = "delete_subst_response",
                        Success = result.IsSuccess,
                        Error = result.IsSuccess ? null : result.ErrorMessage
                    };
                }
                break;

            case "reset_buffer":
                if (OperatingSystem.IsWindows())
                {
                    try { Console.Clear(); } catch { }
                }
                else
                {
                    console.Write(new ControlCode("\x1bc")); // Full reset
                }
                response = new DosResponse { Command = "reset_buffer_response", Success = true };
                break;

            case "resize_buffer":
                if (command.Width.HasValue && command.Height.HasValue)
                {
                    try
                    {
                        if (OperatingSystem.IsWindows())
                        {
                            Console.SetWindowSize(command.Width.Value, command.Height.Value);
                            Console.SetBufferSize(command.Width.Value, command.Height.Value);
                        }
                        else
                        {
                            console.Write(new ControlCode($"\x1b[8;{command.Height.Value};{command.Width.Value}t"));
                        }
                        response = new DosResponse { Command = "resize_buffer_response", Success = true };
                    }
                    catch
                    {
                        response = new DosResponse { Command = "resize_buffer_response", Success = false, Error = "Failed to resize buffer" };
                    }
                }
                break;

            case "write":
                if (!string.IsNullOrEmpty(command.Text))
                {
                    console.Write(command.Text);
                }
                break;

            case "markup":
                if (!string.IsNullOrEmpty(command.Text))
                {
                    console.Markup(command.Text);
                }
                break;
        }

        // Send response if one was generated
        if (response != null)
        {
            await SendResponseAsync(responseStream, response, cancellationToken);
        }
    }

    private async Task SendResponseAsync(Stream stream, DosResponse response, CancellationToken cancellationToken)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(response, DosResponseContext.Default.DosResponse);
        var bytes = System.Text.Encoding.UTF8.GetBytes("\0" + json);
        await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private string ExpandMacro(string macroText, string[] args)
    {
        // Expand macro special codes:
        // $1-$9 = parameters
        // $* = all parameters
        // $T = command separator (already handled in caller)
        // $$ = literal $
        // $G $g = >
        // $L $l = <
        // $B $b = |
        // $C $c = (
        // $F $f = )

        var result = new System.Text.StringBuilder();

        for (var i = 0; i < macroText.Length; i++)
        {
            if (macroText[i] == '$' && i + 1 < macroText.Length)
            {
                var next = macroText[i + 1];

                if (char.IsDigit(next))
                {
                    var paramNum = next - '0';
                    if (paramNum >= 1 && paramNum <= 9 && paramNum - 1 < args.Length)
                    {
                        result.Append(args[paramNum - 1]);
                    }
                    i++;
                    continue;
                }

                switch (char.ToLower(next))
                {
                    case '*':
                        result.Append(string.Join(" ", args));
                        i++;
                        continue;
                    case '$':
                        result.Append('$');
                        i++;
                        continue;
                    case 'g':
                        result.Append('>');
                        i++;
                        continue;
                    case 'l':
                        result.Append('<');
                        i++;
                        continue;
                    case 'b':
                        result.Append('|');
                        i++;
                        continue;
                    case 'c':
                        result.Append('(');
                        i++;
                        continue;
                    case 'f':
                        result.Append(')');
                        i++;
                        continue;
                }
            }

            result.Append(macroText[i]);
        }

        return result.ToString();
    }

    private string ExpandVariables(string input)
    {
        if (!input.Contains('%')) return input;

        var result = new System.Text.StringBuilder();
        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] == '%')
            {
                var nextPercent = input.IndexOf('%', i + 1);
                if (nextPercent > i + 1)
                {
                    var varName = input.Substring(i + 1, nextPercent - i - 1);
                    var varValue = _fileSystem.GetEnvironmentVariable(varName);
                    if (!string.IsNullOrEmpty(varValue))
                    {
                        result.Append(varValue);
                        i = nextPercent;
                        continue;
                    }
                    else
                    {
                        // DOS-gedrag: als de variabele niet bestaat, laat de %-tekens staan
                        result.Append('%');
                        result.Append(varName);
                        result.Append('%');
                        i = nextPercent;
                        continue;
                    }
                }
            }
            result.Append(input[i]);
        }
        return result.ToString();
    }

    private IAnsiConsole CreateRedirectConsole(StreamWriter writer)
    {
        // Spectre.Console heeft IAnsiConsole.
        // We kunnen een recorder gebruiken of een simpele console implementatie die naar de writer schrijft.
        // Voor nu een simpele fallback naar Spectre's eigen mechanismen indien mogelijk, 
        // maar IAnsiConsole is een interface die we kunnen implementeren.
        return AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer)
        });
    }

    private class ConsoleStreamWrapper : Stream
    {
        private readonly IAnsiConsole _console;

        public ConsoleStreamWrapper(IAnsiConsole console)
        {
            _console = console;
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override long Seek(long offset, SeekOrigin origin) => 0;

        public override void SetLength(long value) { }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var text = System.Text.Encoding.Default.GetString(buffer, offset, count);
            _console.Write(text);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var text = System.Text.Encoding.Default.GetString(buffer, offset, count);
            _console.Write(text);
            await Task.CompletedTask;
        }

        public override void WriteByte(byte value)
        {
            _console.Write(System.Text.Encoding.Default.GetString(new[] { value }));
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get; set; }
    }

    private string[] ParseInput(string input)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            
            // Speciale tekens > en >> afhandelen als delimiters als ze niet in quotes staan
            if (!inQuotes && (c == '>' || (c == '>' && i + 1 < input.Length && input[i+1] == '>')))
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                
                if (c == '>' && i + 1 < input.Length && input[i+1] == '>')
                {
                    result.Add(">>");
                    i++;
                }
                else
                {
                    result.Add(">");
                }
                continue;
            }

            if (c == '\"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        return result.ToArray();
    }
}
