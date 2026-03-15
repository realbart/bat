using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bat;
using Bat.FileSystem;
using Spectre.Console;

AnsiConsole.MarkupLine("[yellow]🦇bat [[Version 0.1.1]][/]");
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

    AnsiConsole.Write(prompt);

    while (true)
    {
        var keyInfo = Console.ReadKey(true);

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
            // Tab completion voor commands en files
            var completion = TryCompleteAsync(currentInput.ToString(), cursorPosition, dispatcher, fileSystem);
            if (!string.IsNullOrEmpty(completion))
            {

                // Vind het laatste woord (path component)
                var input = currentInput.ToString();
                var lastSpace = input.LastIndexOf(' ', cursorPosition - 1);
                var startPos = lastSpace + 1;
                var lengthToReplace = cursorPosition - startPos;

                // Verwijder oud gedeelte
                ClearPartialInput(lengthToReplace);
                currentInput.Remove(startPos, lengthToReplace);
                cursorPosition = startPos;

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

string? TryCompleteAsync(string input, int cursorPos, CommandDispatcher dispatcher, FileSystemService fileSystem)
{
    // Haal het huidige woord op (vanaf laatste spatie tot cursor)
    var lastSpace = input.LastIndexOf(' ', cursorPos - 1);
    var partialWord = input.Substring(lastSpace + 1, cursorPos - lastSpace - 1);

    if (string.IsNullOrEmpty(partialWord)) return null;

    // Als het het eerste woord is, probeer command completion
    if (lastSpace == -1)
    {
        var commands = dispatcher.GetCommandNames()
            .Where(name => name.StartsWith(partialWord, StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (commands.Count == 1)
        {
            return commands[0];
        }
        else if (commands.Count > 1)
        {
            var commonPrefix = FindCommonPrefix(commands);
            if (commonPrefix.Length > partialWord.Length)
            {
                return commonPrefix;
            }
            Console.Beep();
            return null;
        }
    }

    // Anders, path completion
    try
    {
        // Bepaal directory en pattern
        string searchDir;
        string pattern;

        var lastSlash = Math.Max(partialWord.LastIndexOf('/'), partialWord.LastIndexOf('\\'));
        if (lastSlash >= 0)
        {
            // Pad bevat directory component
            searchDir = fileSystem.ResolvePath(partialWord.Substring(0, lastSlash + 1));
            pattern = partialWord.Substring(lastSlash + 1);
        }
        else
        {
            // Alleen filename pattern
            searchDir = fileSystem.CurrentDirectory;
            pattern = partialWord;
        }

        if (!fileSystem.FileSystem.Directory.Exists(searchDir)) return null;

        // Zoek matches (case-insensitive)
        var entries = fileSystem.FileSystem.Directory.GetFileSystemEntries(searchDir)
            .Select(e => fileSystem.FileSystem.Path.GetFileName(e))
            .Where(name => !string.IsNullOrEmpty(name) && name.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (entries.Count == 0) return null;

        if (entries.Count == 1)
        {
            // Exact match - return volledige naam
            var matchedName = entries[0];
            var fullPath = lastSlash >= 0
                ? partialWord.Substring(0, lastSlash + 1) + matchedName
                : matchedName;


            return fullPath;
        }

        // Multiple matches - vind common prefix (case-insensitive)
        var commonPrefix = FindCommonPrefix(entries);
        if (commonPrefix.Length > pattern.Length)
        {
            // Er is een langere gemeenschappelijke prefix
            var completion = lastSlash >= 0
                ? partialWord.Substring(0, lastSlash + 1) + commonPrefix
                : commonPrefix;
            return completion;
        }

        // Geen vooruitgang mogelijk - beep (optioneel)
        Console.Beep();
        return null;
    }
    catch
    {
        return null;
    }
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