using System.Diagnostics;

var choices = "YN";
var hideChoices = false;
var caseSensitive = false;
var timeout = -1;
var defaultChoice = '\0';
var message = "";

int i = 0;
while (i < args.Length)
{
    var arg = args[i];
    if (arg.StartsWith("/C", StringComparison.OrdinalIgnoreCase))
    {
        if (i + 1 < args.Length)
        {
            choices = args[i + 1];
            i += 2;
        }
        else
        {
            ShowHelp();
            return 255;
        }
    }
    else if (arg.Equals("/N", StringComparison.OrdinalIgnoreCase))
    {
        hideChoices = true;
        i++;
    }
    else if (arg.Equals("/CS", StringComparison.OrdinalIgnoreCase))
    {
        caseSensitive = true;
        i++;
    }
    else if (arg.StartsWith("/T", StringComparison.OrdinalIgnoreCase))
    {
        if (i + 1 < args.Length && int.TryParse(args[i + 1], out var t) && t >= 0 && t <= 9999)
        {
            timeout = t;
            i += 2;
        }
        else
        {
            ShowHelp();
            return 255;
        }
    }
    else if (arg.StartsWith("/D", StringComparison.OrdinalIgnoreCase))
    {
        if (i + 1 < args.Length && args[i + 1].Length == 1)
        {
            defaultChoice = args[i + 1][0];
            i += 2;
        }
        else
        {
            ShowHelp();
            return 255;
        }
    }
    else if (arg.StartsWith("/M", StringComparison.OrdinalIgnoreCase))
    {
        if (i + 1 < args.Length)
        {
            message = args[i + 1];
            i += 2;
        }
        else
        {
            message = "";
            i++;
        }
    }
    else
    {
        // Start of message
        break;
    }
}

// Remaining args are the message
var messageArgs = args.Skip(i).ToArray();
if (!string.IsNullOrEmpty(message))
{
    message += " " + string.Join(" ", messageArgs);
}
else
{
    message = string.Join(" ", messageArgs);
}

// Validate choices
if (string.IsNullOrEmpty(choices))
{
    Console.WriteLine("Invalid choice specification.");
    return 255;
}

var choiceList = choices.ToCharArray();
if (timeout >= 0 && defaultChoice != '\0' && !choiceList.Contains(defaultChoice))
{
    Console.WriteLine("Invalid default choice.");
    return 255;
}

// Display message
if (!string.IsNullOrEmpty(message))
{
    Console.Write(message);
}

// Display choices if not /N
if (!hideChoices)
{
    Console.Write(" [");
    for (int j = 0; j < choiceList.Length; j++)
    {
        if (j > 0) Console.Write(",");
        Console.Write(choiceList[j]);
    }
    Console.Write("]? ");
}

// Wait for input
var cts = new CancellationTokenSource();
if (timeout >= 0)
{
    cts.CancelAfter(timeout * 1000);
}

while (!cts.Token.IsCancellationRequested)
{
    try
    {
        var key = Console.ReadKey(true);
        var inputChar = key.KeyChar;
        if (!caseSensitive)
        {
            inputChar = char.ToUpper(inputChar);
            choiceList = choices.ToUpper().ToCharArray();
        }

        var index = Array.IndexOf(choiceList, inputChar);
        if (index >= 0)
        {
            return index + 1;
        }
        else
        {
            Console.Beep();
        }
    }
    catch (OperationCanceledException)
    {
        break;
    }
}

// Timeout or cancelled
if (defaultChoice != '\0')
{
    var defChar = caseSensitive ? defaultChoice : char.ToUpper(defaultChoice);
    var index = Array.IndexOf(choiceList, defChar);
    if (index >= 0)
    {
        return index + 1;
    }
}
return 0;

void ShowHelp()
{
    Console.WriteLine("Prompts the user to make a choice in a batch program.");
    Console.WriteLine();
    Console.WriteLine("CHOICE [/C choices] [/N] [/CS] [/T timeout /D choice] [/M text]");
    Console.WriteLine();
    Console.WriteLine("  /C choices    Specifies allowable keys. Default is YN.");
    Console.WriteLine("  /N            Do not display choices and ? at end of prompt string.");
    Console.WriteLine("  /CS           Enables case-sensitive choices to be selected.");
    Console.WriteLine("  /T timeout    Makes choice automatically after timeout seconds.");
    Console.WriteLine("  /D choice     Specifies default choice after timeout seconds.");
    Console.WriteLine("  /M text       Specifies the message to display before the prompt.");
    Console.WriteLine("                If not specified, only the prompt is displayed.");
    Console.WriteLine();
    Console.WriteLine("  text          Prompt string to display.");
    Console.WriteLine();
    Console.WriteLine("ERRORLEVEL is set to the index of the key that was selected from the set of choices.");
    Console.WriteLine("The first choice listed returns 1, the second 2, etc.");
}
