using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bat;
using Bat.FileSystem;
using Spectre.Console;

AnsiConsole.MarkupLine("[yellow]🦇 bat 0.1.1 🦇[/]");
AnsiConsole.MarkupLine("(c) Bart Kemps. All rights reserved.");
AnsiConsole.WriteLine();

var fileSystem = new FileSystemService();
var dispatcher = new CommandDispatcher(fileSystem);

// Execute autoexec.bat if it exists
var autoexecPath = fileSystem.ResolvePath("autoexec.bat");
var cts = new CancellationTokenSource();
if (fileSystem.FileSystem.File.Exists(autoexecPath))
{
    try
    {
        await dispatcher.DispatchAsync("autoexec.bat", cts.Token);
    }
    catch (OperationCanceledException)
    {
        // Ignore cancellation during autoexec, continue to main loop
    }
}


// Check if a batch file was passed as argument
if (args.Length > 0)
{
    var batchFile = args[0];
    var batchArgs = args.Skip(1).ToArray();
    var fullPath = fileSystem.ResolvePath(batchFile);
    if (fileSystem.FileSystem.File.Exists(fullPath))
    {
        await dispatcher.DispatchAsync(batchFile + " " + string.Join(" ", batchArgs), cts.Token);
        return;
    }
    else
    {
        AnsiConsole.MarkupLine($"[red]The system cannot find the file specified: {batchFile}[/]");
        return;
    }
}

Console.CancelKeyPress += (s, e) =>
{
    // Voorkom dat de applicatie afsluit
    e.Cancel = true;
    // Annuleer de lopende operatie
    cts.Cancel();
    AnsiConsole.MarkupLine("[red]^C[/]");
};

async Task<string?> ReadLineWithHistoryAsync(string prompt, CommandDispatcher dispatcher)
{
    var history = dispatcher.History;
    var historyIndex = history.Count;
    var currentInput = new StringBuilder();
    var cursorPosition = 0;
    
    // Tab completion state
    string? tabSearchPattern = null;
    List<string>? tabMatches = null;
    int tabMatchIndex = -1;
    int tabStartPos = -1;

    AnsiConsole.Write(prompt);

    while (true)
    {
        var keyInfo = Console.ReadKey(true);
        
        // Reset tab completion state on non-tab keys
        if (keyInfo.Key != ConsoleKey.Tab)
        {
            tabSearchPattern = null;
            tabMatches = null;
            tabMatchIndex = -1;
            tabStartPos = -1;
        }

        if (keyInfo.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            return currentInput.ToString();
        }
        else if (keyInfo.Key == ConsoleKey.Backspace)
        {
            if (cursorPosition > 0)
            {
                currentInput.Remove(cursorPosition - 1, 1);
                cursorPosition--;
                Console.Write("\b \b");
                // Herteken de rest van de regel als we niet aan het eind waren
                if (cursorPosition < currentInput.Length)
                {
                    var remaining = currentInput.ToString().Substring(cursorPosition);
                    Console.Write(remaining + " ");
                    for (var i = 0; i <= remaining.Length; i++) Console.Write("\b");
                }
            }
        }
        else if (keyInfo.Key == ConsoleKey.Delete)
        {
            if (cursorPosition < currentInput.Length)
            {
                currentInput.Remove(cursorPosition, 1);
                var remaining = currentInput.ToString().Substring(cursorPosition);
                Console.Write(remaining + " ");
                for (var i = 0; i <= remaining.Length; i++) Console.Write("\b");
            }
        }
        else if (keyInfo.Key == ConsoleKey.LeftArrow)
        {
            if (cursorPosition > 0)
            {
                cursorPosition--;
                Console.Write("\b");
            }
        }
        else if (keyInfo.Key == ConsoleKey.RightArrow)
        {
            if (cursorPosition < currentInput.Length)
            {
                Console.Write(currentInput[cursorPosition].ToString());
                cursorPosition++;
            }
        }
        else if (keyInfo.Key == ConsoleKey.UpArrow)
        {
            if (history.Count > 0 && historyIndex > 0)
            {
                historyIndex--;
                ClearCurrentLine(prompt, currentInput.Length);
                currentInput.Clear();
                currentInput.Append(history[historyIndex]);
                Console.Write(currentInput.ToString());
                cursorPosition = currentInput.Length;
            }
        }
        else if (keyInfo.Key == ConsoleKey.DownArrow)
        {
            if (historyIndex < history.Count)
            {
                historyIndex++;
                ClearCurrentLine(prompt, currentInput.Length);
                currentInput.Clear();
                if (historyIndex < history.Count)
                {
                    currentInput.Append(history[historyIndex]);
                }
                Console.Write(currentInput.ToString());
                cursorPosition = currentInput.Length;
            }
        }
        else if (keyInfo.Key == ConsoleKey.Tab)
        {
            if (tabMatches == null)
            {
                // Init tab completion
                var input = currentInput.ToString();
                var lastSpace = input.LastIndexOf(' ', cursorPosition - 1);
                tabStartPos = lastSpace + 1;
                tabSearchPattern = input.Substring(tabStartPos, cursorPosition - tabStartPos);

                if (lastSpace == -1)
                {
                    // Command completion
                    tabMatches = dispatcher.GetCommandNames()
                        .Where(name => name.StartsWith(tabSearchPattern, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
                else
                {
                    // Path completion
                    try
                    {
                        string searchDir;
                        string pattern;
                        var lastSlash = Math.Max(tabSearchPattern.LastIndexOf('/'), tabSearchPattern.LastIndexOf('\\'));
                        if (lastSlash >= 0)
                        {
                            searchDir = fileSystem.ResolvePath(tabSearchPattern.Substring(0, lastSlash + 1));
                            pattern = tabSearchPattern.Substring(lastSlash + 1);
                        }
                        else
                        {
                            searchDir = fileSystem.CurrentDirectory;
                            pattern = tabSearchPattern;
                        }

                        var resolved = fileSystem.ResolvePath(searchDir);
                        if (fileSystem.FileSystem.Directory.Exists(resolved))
                        {
                            tabMatches = fileSystem.FileSystem.Directory.GetFileSystemEntries(resolved)
                                .Select(e => fileSystem.FileSystem.Path.GetFileName(e))
                                .Where(name => !string.IsNullOrEmpty(name) && name.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                                .Select(name => lastSlash >= 0 ? tabSearchPattern.Substring(0, lastSlash + 1) + name : name)
                                .ToList();
                        }
                    }
                    catch { }
                }

                if (tabMatches == null || tabMatches.Count == 0)
                {
                    Console.Beep();
                    tabMatches = null;
                    continue;
                }
                tabMatchIndex = 0;
            }
            else
            {
                // Next match
                tabMatchIndex = (tabMatchIndex + 1) % tabMatches.Count;
            }

            var completion = tabMatches[tabMatchIndex];
            var currentLenToReplace = cursorPosition - tabStartPos;

            // Verwijder oud gedeelte
            ClearPartialInput(currentLenToReplace);
            currentInput.Remove(tabStartPos, currentLenToReplace);
            cursorPosition = tabStartPos;

            // Voeg completion toe
            currentInput.Insert(cursorPosition, completion);
            Console.Write(completion);
            cursorPosition += completion.Length;

            // Herteken rest
            if (cursorPosition < currentInput.Length)
            {
                var remaining = currentInput.ToString().Substring(cursorPosition);
                Console.Write(remaining);
                for (var i = 0; i < remaining.Length; i++) Console.Write("\b");
            }
        }
        else if (keyInfo.Key == ConsoleKey.Escape)
        {
            ClearCurrentLine(prompt, currentInput.Length);
            currentInput.Clear();
            cursorPosition = 0;
            historyIndex = history.Count;
        }
        else if (keyInfo.KeyChar != '\0' && !char.IsControl(keyInfo.KeyChar))
        {
            currentInput.Insert(cursorPosition, keyInfo.KeyChar);
            Console.Write(keyInfo.KeyChar.ToString());
            cursorPosition++;
            
            // Herteken de rest van de regel
            if (cursorPosition < currentInput.Length)
            {
                var remaining = currentInput.ToString().Substring(cursorPosition);
                Console.Write(remaining);
                for (var i = 0; i < remaining.Length; i++) Console.Write("\b");
            }
        }
    }
}

void ClearCurrentLine(string prompt, int inputLength)
{
    for (var i = 0; i < inputLength; i++) Console.Write("\b \b");
}

void ClearPartialInput(int length)
{
    for (var i = 0; i < length; i++) Console.Write("\b \b");
}

string FindCommonPrefix(List<string> items)
{
    if (items.Count == 0) return "";
    if (items.Count == 1) return items[0];

    var first = items[0];
    var prefixLen = 0;

    for (var i = 0; i < first.Length; i++)
    {
        var c = first[i];
        var allMatch = items.All(item =>
            i < item.Length &&
            char.ToLowerInvariant(item[i]) == char.ToLowerInvariant(c));

        if (!allMatch) break;
        prefixLen++;
    }

    // Return met case van eerste match
    return first.Substring(0, prefixLen);
}

while (true)
{
    // Gebruik de PROMPT variabele uit FileSystemService
    var prompt = fileSystem.FormatPrompt();
    
    var input = await ReadLineWithHistoryAsync(prompt, dispatcher);
    if (input == null)
    {
        break;
    }

    try
    {
        await dispatcher.DispatchAsync(input, cts.Token);
    }
    catch (OperationCanceledException)
    {
        // Negeer annulering, we gaan gewoon door naar de volgende prompt
    }
    finally
    {
        // Reset de token source voor het volgende commando als het geannuleerd was
        if (cts.IsCancellationRequested)
        {
            cts.Dispose();
            cts = new CancellationTokenSource();
        }
    }
    
    AnsiConsole.WriteLine();
}