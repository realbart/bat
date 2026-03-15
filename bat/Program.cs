using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bat;
using Bat.FileSystem;
using Spectre.Console;

AnsiConsole.MarkupLine("[yellow]bat [[Version 0.1.0]][/]");
AnsiConsole.MarkupLine("(c) Bart Corporation. All rights reserved.");
AnsiConsole.WriteLine();

var fileSystem = new FileSystemService();
var dispatcher = new CommandDispatcher(fileSystem);

var cts = new CancellationTokenSource();

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
            AnsiConsole.WriteLine();
            return currentInput.ToString();
        }
        else if (keyInfo.Key == ConsoleKey.Backspace)
        {
            if (cursorPosition > 0)
            {
                currentInput.Remove(cursorPosition - 1, 1);
                cursorPosition--;
                AnsiConsole.Write("\b \b");
                // Herteken de rest van de regel als we niet aan het eind waren
                if (cursorPosition < currentInput.Length)
                {
                    var remaining = currentInput.ToString().Substring(cursorPosition);
                    AnsiConsole.Write(remaining + " ");
                    for (var i = 0; i <= remaining.Length; i++) AnsiConsole.Write("\b");
                }
            }
        }
        else if (keyInfo.Key == ConsoleKey.Delete)
        {
            if (cursorPosition < currentInput.Length)
            {
                currentInput.Remove(cursorPosition, 1);
                var remaining = currentInput.ToString().Substring(cursorPosition);
                AnsiConsole.Write(remaining + " ");
                for (var i = 0; i <= remaining.Length; i++) AnsiConsole.Write("\b");
            }
        }
        else if (keyInfo.Key == ConsoleKey.LeftArrow)
        {
            if (cursorPosition > 0)
            {
                cursorPosition--;
                AnsiConsole.Write("\b");
            }
        }
        else if (keyInfo.Key == ConsoleKey.RightArrow)
        {
            if (cursorPosition < currentInput.Length)
            {
                AnsiConsole.Write(currentInput[cursorPosition].ToString());
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
                AnsiConsole.Write(currentInput.ToString());
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
                AnsiConsole.Write(currentInput.ToString());
                cursorPosition = currentInput.Length;
            }
        }
        else if (keyInfo.Key == ConsoleKey.Tab)
        {
            // Tab completion voor files en directories
            var completion = TryCompletePathAsync(currentInput.ToString(), cursorPosition);
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
                AnsiConsole.Write(completion);
                cursorPosition += completion.Length;

                // Herteken rest
                if (cursorPosition < currentInput.Length)
                {
                    var remaining = currentInput.ToString().Substring(cursorPosition);
                    AnsiConsole.Write(remaining);
                    for (var i = 0; i < remaining.Length; i++) AnsiConsole.Write("\b");
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
            AnsiConsole.Write(keyInfo.KeyChar.ToString());
            cursorPosition++;
            
            // Herteken de rest van de regel
            if (cursorPosition < currentInput.Length)
            {
                var remaining = currentInput.ToString().Substring(cursorPosition);
                AnsiConsole.Write(remaining);
                for (var i = 0; i < remaining.Length; i++) AnsiConsole.Write("\b");
            }
        }
    }
}

void ClearCurrentLine(string prompt, int inputLength)
{
    for (var i = 0; i < inputLength; i++) AnsiConsole.Write("\b \b");
}

void ClearPartialInput(int length)
{
    for (var i = 0; i < length; i++) AnsiConsole.Write("\b \b");
}

string? TryCompletePathAsync(string input, int cursorPos)
{
    // Haal het huidige woord op (vanaf laatste spatie tot cursor)
    var lastSpace = input.LastIndexOf(' ', cursorPos - 1);
    var partialPath = input.Substring(lastSpace + 1, cursorPos - lastSpace - 1);

    if (string.IsNullOrEmpty(partialPath)) return null;

    try
    {
        // Bepaal directory en pattern
        string searchDir;
        string pattern;

        var lastSlash = Math.Max(partialPath.LastIndexOf('/'), partialPath.LastIndexOf('\\'));
        if (lastSlash >= 0)
        {
            // Pad bevat directory component
            searchDir = fileSystem.ResolvePath(partialPath.Substring(0, lastSlash + 1));
            pattern = partialPath.Substring(lastSlash + 1);
        }
        else
        {
            // Alleen filename pattern
            searchDir = fileSystem.CurrentDirectory;
            pattern = partialPath;
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
                ? partialPath.Substring(0, lastSlash + 1) + matchedName
                : matchedName;

            // Voeg trailing backslash toe voor directories
            var resolvedFullPath = fileSystem.ResolvePath(fullPath);
            if (fileSystem.FileSystem.Directory.Exists(resolvedFullPath))
            {
                if (!fullPath.EndsWith("\\") && !fullPath.EndsWith("/"))
                {
                    fullPath += "\\";
                }
            }

            return fullPath;
        }

        // Multiple matches - vind common prefix (case-insensitive)
        var commonPrefix = FindCommonPrefix(entries);
        if (commonPrefix.Length > pattern.Length)
        {
            // Er is een langere gemeenschappelijke prefix
            var completion = lastSlash >= 0
                ? partialPath.Substring(0, lastSlash + 1) + commonPrefix
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