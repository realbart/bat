using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Bat.Context.Ux;
using Bat.Console;
using Bat.Context;
using Bat.Tokens;
using Tree;

if (OperatingSystem.IsWindows())
{
    Console.WriteLine("Deze test moet op Linux gedraaid worden.");
    return;
}

var testRoot = Path.Combine(Path.GetTempPath(), $"TreeRepro_{Guid.NewGuid():N}");
Directory.CreateDirectory(testRoot);
try
{
    // Structuur van de user:
    // /home/bart/.c/Users -> /home (of iets dergelijks dat terug naar bart wijst)
    // Laten we dit versimpelen:
    // testRoot/
    //   .c/
    //     Users -> testRoot
    
    var dotC = Path.Combine(testRoot, ".c");
    Directory.CreateDirectory(dotC);
    
    var usersSymlink = Path.Combine(dotC, "Users");
    File.CreateSymbolicLink(usersSymlink, testRoot);

    var fs = new UxFileSystemAdapter(new Dictionary<char, string> { ['Z'] = testRoot });
    var console = new TestConsole();
    var ctx = new UxContextAdapter(fs, console);
    ctx.SetCurrentDrive('Z');
    ctx.SetPath('Z', [".c"]);

    Console.WriteLine($"Testing TREE in {dotC} (Z:\\.c)");

    // Simuleer TREE
    // We gebruiken een timeout om te voorkomen dat de test oneindig blijft draaien
    var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
    
    try {
        var spec = ArgumentSpec.From([new BuiltInCommandAttribute("tree")]);
        var args = ArgumentSet.Parse([], spec);

        var task = Tree.Program.Main(ctx, args);
        if (await Task.WhenAny(task, Task.Delay(-1, cts.Token)) == task)
        {
            await task;
            Console.WriteLine("Tree.Program.Main finished.");
        }
        else
        {
            Console.WriteLine("FAIL: Tree.Program.Main timed out! Loop detected.");
            // We kunnen de output tot nu toe bekijken
        }
    }
    catch (OperationCanceledException)
    {
         Console.WriteLine("FAIL: Tree.Program.Main timed out (Cancelled)! Loop detected.");
    }

    var output = string.Join("\n", console.OutLines);
    Console.WriteLine("Output snippet (first 20 lines):");
    Console.WriteLine(string.Join("\n", console.OutLines.Take(20)));
    
    if (console.OutLines.Count > 100) {
         Console.WriteLine($"Total output lines: {console.OutLines.Count}. Definitely a loop.");
    }

    // Inspect attributes directly via UxFileSystemAdapter for "Users"
    var entries = fs.EnumerateEntries('Z', [".c"], "*").ToList();
    foreach (var entry in entries)
    {
        Console.WriteLine($"Entry: {entry.Name}, IsDir: {entry.IsDirectory}, Attributes: {entry.Attributes}, (ReparsePoint: {(entry.Attributes & FileAttributes.ReparsePoint) != 0})");
    }

}
finally
{
    // De symlink eerst verwijderen om recursieve delete problemen te voorkomen
    try {
        var usersSymlink = Path.Combine(testRoot, ".c", "Users");
        if (File.Exists(usersSymlink)) File.Delete(usersSymlink);
    } catch {}
    
    if (Directory.Exists(testRoot))
        Directory.Delete(testRoot, true);
}
