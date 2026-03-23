# STEP 04 - CD en DIR Commands (Volledig Geïmplementeerd)

**Doel:** Werkende CD en DIR commands met **ALLE** switches en features zoals CMD.

## Context

CD en DIR zijn de **meest gebruikte** commands in CMD. Ze moeten 100% correct werken.
Raadpleeg de documentatie op het huidige OS met bijvoorbeeld `cmd /c dir /? >path_to_documentation.txt`

### CD (CHDIR) Features

- `CD` zonder args → toon huidige directory
- `CD path` → verander naar directory
- `CD /D drive:\path` → verander drive EN directory
- `CD ..` → parent directory
- `CD \` → root van drive
- `CD ..\..` → multiple levels omhoog

### DIR Features (26 switches!)

**Basis:**
- `DIR` → lijst huidige directory
- `DIR path` → lijst andere directory
- `DIR *.txt` → wildcard filtering

**Attribute filters (/A):**
- `/A:D` → alleen directories
- `/A:H` → hidden files
- `/A:S` → system files
- `/A:R` → read-only
- `/A:A` → archive
- `/A:-D` → alles BEHALVE directories

**Display format:**
- `/B` → bare (alleen namen)
- `/W` → wide (kolommen)
- `/L` → lowercase

**Sorting (/O):**
- `/O:N` → naam
- `/O:E` → extensie
- `/O:S` → grootte
- `/O:D` → datum
- `/O:-N` → omgekeerde naam

**Recursion:**
- `/S` → inclusief subdirectories

**Others:**
- `/P` → pause per pagina
- `/Q` → toon eigenaar
- `/T:C` → creation time (default: last write)
- `/T:A` → last access
- `/T:W` → last write

## Test-First Aanpak

### Test File: `CdCommandTests.cs`

**Test 1: CD zonder args toont huidige directory**
```csharp
[Fact]
public async Task Cd_NoArgs_ShowsCurrentDirectory()
{
    // Arrange
    var ctx = CreateContext(drive: 'Z', path: ["Users", "Bart"]);
    var cmd = new CdCommand();
    
    var output = new StringWriter();
    Console.SetOut(output);
    
    // Act
    var exitCode = await cmd.ExecuteAsync(ctx, [], ctx.CurrentBatch, []);
    
    // Assert
    Assert.Equal(0, exitCode);
    Assert.Equal("Z:\\Users\\Bart\r\n", output.ToString());
}
```

**Test 2: CD naar subdirectory**
```csharp
[Fact]
public async Task Cd_ToSubdirectory_ChangesPath()
{
    // Arrange
    var ctx = CreateContext(drive: 'Z', path: []);
    CreateDirectory(ctx.FileSystem, 'Z', ["Windows"]);
    var cmd = new CdCommand();
    
    // Act
    var exitCode = await cmd.ExecuteAsync(ctx, 
        [Token.Text("Windows")], ctx.CurrentBatch, []);
    
    // Assert
    Assert.Equal(0, exitCode);
    Assert.Equal(new[] { "Windows" }, ctx.CurrentPath);
}
```

**Test 3: CD .. naar parent**
```csharp
[Fact]
public async Task Cd_DotDot_GoesToParent()
{
    // Arrange
    var ctx = CreateContext(drive: 'Z', path: ["Users", "Bart", "Documents"]);
    var cmd = new CdCommand();
    
    // Act
    await cmd.ExecuteAsync(ctx, [Token.Text("..")], ctx.CurrentBatch, []);
    
    // Assert
    Assert.Equal(new[] { "Users", "Bart" }, ctx.CurrentPath);
}
```

**Test 4: CD /D verandert drive**
```csharp
[Fact]
public async Task Cd_WithD_ChangesDrive()
{
    // Arrange
    var ctx = CreateContext(drive: 'Z', path: ["Users"]);
    var cmd = new CdCommand();
    
    // Act
    await cmd.ExecuteAsync(ctx, 
        [Token.Text("/D"), Token.Text("D:\\Projects")], 
        ctx.CurrentBatch, []);
    
    // Assert
    Assert.Equal('D', ctx.CurrentDrive);
    Assert.Equal(new[] { "Projects" }, ctx.CurrentPath);
}
```

**Test 5: CD naar niet-bestaande directory faalt**
```csharp
[Fact]
public async Task Cd_NonExistingDir_ReturnsError()
{
    // Arrange
    var ctx = CreateContext(drive: 'Z', path: []);
    var cmd = new CdCommand();
    
    var errorOutput = new StringWriter();
    Console.SetError(errorOutput);
    
    // Act
    var exitCode = await cmd.ExecuteAsync(ctx, 
        [Token.Text("NotExist")], ctx.CurrentBatch, []);
    
    // Assert
    Assert.NotEqual(0, exitCode);
    Assert.Contains("cannot find", errorOutput.ToString().ToLower());
}
```

### Test File: `DirCommandTests.cs`

**Test 6: DIR zonder args lijst huidige directory**
```csharp
[Fact]
public async Task Dir_NoArgs_ListsCurrentDirectory()
{
    // Arrange
    var ctx = CreateContext(drive: 'Z', path: ["TestDir"]);
    CreateFile(ctx.FileSystem, 'Z', ["TestDir", "file1.txt"], "content");
    CreateFile(ctx.FileSystem, 'Z', ["TestDir", "file2.txt"], "content");
    
    var output = new StringWriter();
    Console.SetOut(output);
    
    var cmd = new DirCommand();
    
    // Act
    await cmd.ExecuteAsync(ctx, [], ctx.CurrentBatch, []);
    
    // Assert
    var result = output.ToString();
    Assert.Contains("file1.txt", result);
    Assert.Contains("file2.txt", result);
}
```

**Test 7: DIR met wildcard**
```csharp
[Fact]
public async Task Dir_Wildcard_FiltersFiles()
{
    // Arrange
    var ctx = CreateContext(drive: 'Z', path: []);
    CreateFile(ctx.FileSystem, 'Z', ["test.txt"], "");
    CreateFile(ctx.FileSystem, 'Z', ["test.log"], "");
    CreateFile(ctx.FileSystem, 'Z', ["other.txt"], "");
    
    var output = new StringWriter();
    Console.SetOut(output);
    
    var cmd = new DirCommand();
    
    // Act
    await cmd.ExecuteAsync(ctx, [Token.Text("*.log")], ctx.CurrentBatch, []);
    
    // Assert
    var result = output.ToString();
    Assert.Contains("test.log", result);
    Assert.DoesNotContain("test.txt", result);
    Assert.DoesNotContain("other.txt", result);
}
```

**Test 8: DIR /B bare format**
```csharp
[Fact]
public async Task Dir_BareFormat_OnlyNames()
{
    // Arrange
    var ctx = CreateContext(drive: 'Z', path: []);
    CreateFile(ctx.FileSystem, 'Z', ["file1.txt"], "");
    CreateDirectory(ctx.FileSystem, 'Z', ["dir1"]);
    
    var output = new StringWriter();
    Console.SetOut(output);
    
    var cmd = new DirCommand();
    
    // Act
    await cmd.ExecuteAsync(ctx, [Token.Text("/B")], ctx.CurrentBatch, []);
    
    // Assert
    var result = output.ToString();
    var lines = result.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    
    Assert.Contains("file1.txt", lines);
    Assert.Contains("dir1", lines);
    Assert.DoesNotContain("Directory of", result);  // Geen header in /B mode
}
```

**Test 9: DIR /A:D alleen directories**
```csharp
[Fact]
public async Task Dir_AttributeD_OnlyDirectories()
{
    // Arrange
    var ctx = CreateContext(drive: 'Z', path: []);
    CreateFile(ctx.FileSystem, 'Z', ["file.txt"], "");
    CreateDirectory(ctx.FileSystem, 'Z', ["dir1"]);
    CreateDirectory(ctx.FileSystem, 'Z', ["dir2"]);
    
    var output = new StringWriter();
    Console.SetOut(output);
    
    var cmd = new DirCommand();
    
    // Act
    await cmd.ExecuteAsync(ctx, [Token.Text("/A:D")], ctx.CurrentBatch, []);
    
    // Assert
    var result = output.ToString();
    Assert.Contains("dir1", result);
    Assert.Contains("dir2", result);
    Assert.DoesNotContain("file.txt", result);
}
```

**Test 10: DIR /S recursief**
```csharp
[Fact]
public async Task Dir_Recursive_ShowsSubdirectories()
{
    // Arrange
    var ctx = CreateContext(drive: 'Z', path: []);
    CreateFile(ctx.FileSystem, 'Z', ["root.txt"], "");
    CreateDirectory(ctx.FileSystem, 'Z', ["subdir"]);
    CreateFile(ctx.FileSystem, 'Z', ["subdir", "nested.txt"], "");
    
    var output = new StringWriter();
    Console.SetOut(output);
    
    var cmd = new DirCommand();
    
    // Act
    await cmd.ExecuteAsync(ctx, [Token.Text("/S")], ctx.CurrentBatch, []);
    
    // Assert
    var result = output.ToString();
    Assert.Contains("root.txt", result);
    Assert.Contains("nested.txt", result);
    Assert.Contains("Directory of Z:\\subdir", result);
}
```

**Test 11: DIR /O:N sorteert op naam**
```csharp
[Fact]
public async Task Dir_OrderByName_Sorts()
{
    // Arrange
    var ctx = CreateContext(drive: 'Z', path: []);
    CreateFile(ctx.FileSystem, 'Z', ["zebra.txt"], "");
    CreateFile(ctx.FileSystem, 'Z', ["alpha.txt"], "");
    CreateFile(ctx.FileSystem, 'Z', ["middle.txt"], "");
    
    var output = new StringWriter();
    Console.SetOut(output);
    
    var cmd = new DirCommand();
    
    // Act
    await cmd.ExecuteAsync(ctx, [Token.Text("/B"), Token.Text("/O:N")], ctx.CurrentBatch, []);
    
    // Assert
    var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal("alpha.txt", lines[0]);
    Assert.Equal("middle.txt", lines[1]);
    Assert.Equal("zebra.txt", lines[2]);
}
```

## Implementatie Stappen

### 4.1 CD Command implementeren

**Bestand:** `Bat/Commands/CdCommand.cs`

```csharp
namespace Bat.Commands;

public class CdCommand : ICommand
{
    public async Task<int> ExecuteAsync(IContext ctx, IReadOnlyList<IToken> args,
                                       BatchContext bc, IReadOnlyList<Redirection> redirects)
    {
        // Geen argumenten → toon huidige directory
        if (args.Count == 0)
        {
            await Console.Out.WriteLineAsync(ctx.CurrentPathDisplayName);
            return 0;
        }
        
        // Check voor /D switch
        var hasD = args.Any(a => a.ToString().Equals("/D", StringComparison.OrdinalIgnoreCase));
        var pathArg = args.FirstOrDefault(a => !a.ToString().StartsWith("/"))?.ToString() ?? "";
        
        // Parse path (kan drive bevatten: D:\path of \path of ..\path)
        var (targetDrive, targetPath) = ParsePath(pathArg, ctx);
        
        // Als /D niet gezet en drive verschilt: error
        if (!hasD && targetDrive != ctx.CurrentDrive)
        {
            await Console.Error.WriteLineAsync($"Use /D switch to change drive to {targetDrive}:");
            return 1;
        }
        
        // Valideer dat directory bestaat
        if (!ctx.FileSystem.DirectoryExists(targetDrive, targetPath))
        {
            await Console.Error.WriteLineAsync($"The system cannot find the path specified.");
            return 1;
        }
        
        // Update context
        ctx.SetCurrentPath(targetDrive, targetPath);
        if (hasD)
            ctx.CurrentDrive = targetDrive;
        
        return 0;
    }
    
    private (char drive, string[] path) ParsePath(string pathArg, IContext ctx)
    {
        // Implementatie: parse D:\path, \path, ..\path, path
        // Zie ReactOS ChangeDirectory() in cmd.c
    }
}
```

### 4.2 DIR Command implementeren

**Bestand:** `Bat/Commands/DirCommand.cs`

```csharp
namespace Bat.Commands;

public class DirCommand : ICommand
{
    public async Task<int> ExecuteAsync(IContext ctx, IReadOnlyList<IToken> args,
                                       BatchContext bc, IReadOnlyList<Redirection> redirects)
    {
        // Parse switches
        var options = ParseDirOptions(args);
        
        // Get target path (default: current directory)
        var (drive, path, pattern) = ParseTarget(args, ctx);
        
        // Enumerate entries
        var entries = ctx.FileSystem
            .EnumerateEntries(drive, path, pattern)
            .ToList();
        
        // Apply attribute filter
        if (options.AttributeFilter != null)
            entries = ApplyAttributeFilter(entries, options.AttributeFilter, ctx.FileSystem, drive, path);
        
        // Apply sorting
        if (options.SortOrder != null)
            entries = ApplySort(entries, options.SortOrder, ctx.FileSystem, drive, path);
        
        // Recursive?
        if (options.Recursive)
            entries = EnumerateRecursive(drive, path, pattern, options, ctx.FileSystem);
        
        // Display
        if (options.BareFormat)
            await DisplayBare(entries);
        else if (options.WideFormat)
            await DisplayWide(entries, ctx);
        else
            await DisplayStandard(entries, drive, path, ctx);
        
        return 0;
    }
    
    private DirOptions ParseDirOptions(IReadOnlyList<IToken> args)
    {
        // Parse /B, /W, /S, /A:D, /O:N, etc.
    }
}

internal record DirOptions
{
    public bool BareFormat { get; init; }
    public bool WideFormat { get; init; }
    public bool Recursive { get; init; }
    public string? AttributeFilter { get; init; }  // D, H, S, R, A, -D
    public string? SortOrder { get; init; }  // N, E, S, D, -N, etc.
    public bool Lowercase { get; init; }
    public bool Pause { get; init; }
    public char TimeField { get; init; } = 'W';  // C, A, W
}
```

**Tests voor DIR:**

**Test 12: DIR /S /B *.cs recursief**
```csharp
[Fact]
public async Task Dir_RecursiveBare_ShowsAllMatches()
{
    // Arrange
    var ctx = CreateContext(drive: 'Z', path: []);
    CreateFile(ctx.FileSystem, 'Z', ["root.cs"], "");
    CreateDirectory(ctx.FileSystem, 'Z', ["src"]);
    CreateFile(ctx.FileSystem, 'Z', ["src", "file.cs"], "");
    CreateDirectory(ctx.FileSystem, 'Z', ["src", "nested"]);
    CreateFile(ctx.FileSystem, 'Z', ["src", "nested", "deep.cs"], "");
    
    var output = new StringWriter();
    Console.SetOut(output);
    
    var cmd = new DirCommand();
    
    // Act
    await cmd.ExecuteAsync(ctx, 
        [Token.Text("/S"), Token.Text("/B"), Token.Text("*.cs")], 
        ctx.CurrentBatch, []);
    
    // Assert
    var result = output.ToString();
    Assert.Contains("root.cs", result);
    Assert.Contains("file.cs", result);
    Assert.Contains("deep.cs", result);
}
```

**Test 13: DIR combinatie van switches**
```csharp
[Fact]
public async Task Dir_CombinedSwitches_Works()
{
    // Arrange
    var ctx = CreateContext(drive: 'Z', path: []);
    CreateDirectory(ctx.FileSystem, 'Z', ["dir1"]);
    CreateDirectory(ctx.FileSystem, 'Z', ["dir2"]);
    CreateFile(ctx.FileSystem, 'Z', ["file.txt"], "");
    
    var output = new StringWriter();
    Console.SetOut(output);
    
    var cmd = new DirCommand();
    
    // Act - alleen directories, bare format, gesorteerd
    await cmd.ExecuteAsync(ctx,
        [Token.Text("/A:D"), Token.Text("/B"), Token.Text("/O:N")],
        ctx.CurrentBatch, []);
    
    // Assert
    var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(2, lines.Length);
    Assert.Equal("dir1", lines[0]);
    Assert.Equal("dir2", lines[1]);
}
```

## Implementatie Volgorde

### CD Command:
1. Creëer `CdCommand.cs` class
2. Implementeer no-args (show current) + test
3. Implementeer relative path (cd subdir) + test
4. Implementeer .. (parent) + test
5. Implementeer \ (root) + test
6. Implementeer /D switch + test
7. Implementeer absolute path + test
8. Error handling (non-existing) + test

### DIR Command:
1. Creëer `DirCommand.cs` class
2. Implementeer basis (no switches) + test
3. Implementeer wildcard filtering + test
4. Implementeer /B (bare) + test
5. Implementeer /A:D (directories only) + test
6. Implementeer /A:H, /A:S, /A:R + tests
7. Implementeer /A:-D (inverse filter) + test
8. Implementeer /O:N (sort by name) + test
9. Implementeer /O:E, /O:S, /O:D + tests
10. Implementeer /S (recursive) + test
11. Implementeer /W (wide) + test
12. Implementeer /L (lowercase) + test
13. Implementeer combinaties + tests

### Helper methods:
- `ParsePath()` - parse D:\path, ..\path, etc.
- `ParseDirOptions()` - parse alle switches
- `ApplyAttributeFilter()` - filter op attributes
- `ApplySort()` - sorteer entries
- `DisplayBare()` - bare output format
- `DisplayWide()` - wide output format
- `DisplayStandard()` - standaard DIR format

## Acceptance Criteria

- [ ] CD zonder args toont pad
- [ ] CD path werkt (relatief en absoluut)
- [ ] CD .. werkt (multiple levels)
- [ ] CD /D verandert drive
- [ ] CD naar non-existing geeft error
- [ ] DIR toont bestanden en directories
- [ ] DIR *.txt wildcard werkt
- [ ] DIR /B toont alleen namen
- [ ] DIR /A:D toont alleen directories
- [ ] DIR /A:-D toont alles behalve directories
- [ ] DIR /O:N sorteert alfabetisch
- [ ] DIR /S recursief werkt
- [ ] DIR /W wide format werkt
- [ ] 15+ tests slagen

## Manual Testing

Start Bat en test:
```sh
Z:\> cd
Z:\

Z:\> cd Windows
Z:\Windows>

Z:\> cd ..
Z:\>

Z:\> dir /B
(lijst van directories en files in Z:\)

Z:\> dir /A:D /B
(alleen directories)

Z:\> dir /S /B *.txt
(alle .txt files recursief)

Z:\> cd /D D:\Projects
D:\Projects>
```

## Geschatte Tijd

3-4 uur (DIR heeft veel switches!)

## Referenties

- **ReactOS CD:** https://doxygen.reactos.org/db/d4f/base_2shell_2cmd_2cmd_8c_source.html (ChangeDirectory)
- **ReactOS DIR:** https://doxygen.reactos.org/d8/d8c/directives_8c_source.html
- **Microsoft CD docs:** https://learn.microsoft.com/windows-server/administration/windows-commands/cd
- **Microsoft DIR docs:** https://learn.microsoft.com/windows-server/administration/windows-commands/dir
- **IMPLEMENTATION_PLAN.md:** Fase 2 (Ingebouwde commando's)

## Tips

**DIR formaat:**
```
 Volume in drive Z has no label.
 Volume Serial Number is 1234-5678

 Directory of Z:\Users\Bart

01/15/2024  02:30 PM    <DIR>          .
01/15/2024  02:30 PM    <DIR>          ..
01/10/2024  10:00 AM             1,234 file.txt
01/12/2024  03:45 PM    <DIR>          Documents
               1 File(s)          1,234 bytes
               1 Dir(s)  123,456,789 bytes free
```

Vergelijk output met echte `cmd.exe` om exact format te matchen!
