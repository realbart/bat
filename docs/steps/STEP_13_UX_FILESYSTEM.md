# STEP 13 - UxFileSystem / UxContext

**Doel:** Bat draait volledig op Unix via een adapter die Unix-paden presenteert als Windows-drives.

## Context

Na stap 3 (DosFileSystem) is er een volledige referentie-implementatie voor Windows. Stap 13 bouwt
de Unix-pendant. De modusdetectie uit stap 12 (`Path.DirectorySeparatorChar == '/'`) bepaalt welke
implementatie wordt gekozen bij opstarten.

Het doel: batchbestanden geschreven voor Windows (met `C:\`, `D:\`, etc.) draaien ongewijzigd op
Linux/macOS via de drive-mappings.

## Architectuur

### Drive-mapping op Unix

Zonder `/M`-opties geldt de standaardmapping:

```
C: → /   (root van het Unix-bestandssysteem)
```

Met `/M`-opties (stap 12) kan dit worden aangepast:

```
bat -m C /home/user -m P /mnt/projects
→  C: = /home/user
→  P: = /mnt/projects
```

### Padvertaling (bidirectioneel)

```
Windows-pad (intern)      Unix-pad (extern)
C:\Users\Bart          ↔  /home/bart           (via C:→/home/bart mapping)
P:\src\main.cs         ↔  /mnt/projects/src/main.cs
```

**Regels:**
- Scheidingsteken: `\` (intern) ↔ `/` (extern)
- Hoofdlettergevoeligheid: intern altijd case-insensitive (match CMD-gedrag), extern case-sensitive
- Bestanden op schijven die niet gemapped zijn: foutmelding (drive bestaat niet)

### UxFileSystemAdapter

Implementeert `IFileSystem` volledig op basis van `System.IO`:

```csharp
internal class UxFileSystemAdapter : FileSystem
{
    // Drive-mappings: 'C' → "/home/user", 'P' → "/mnt/projects"
    private readonly Dictionary<char, string> _mappings;

    public override string GetNativePath(char drive, string[] path)
    {
        var root = _mappings[char.ToUpper(drive)];
        return path.Length == 0
            ? root
            : root.TrimEnd('/') + '/' + string.Join('/', path);
    }

    // Alle andere IFileSystem-methoden delegeren naar System.IO
    // na omzetting via GetNativePath
}
```

### Case-insensitiviteit

Unix-bestandssystemen zijn case-sensitive. CMD en batchbestanden zijn case-insensitief. De adapter
lost dit op door bij elke bestandsoperatie een **case-insensitive zoekopdracht** uit te voeren als
de exacte naam niet bestaat:

```csharp
private string? FindCaseInsensitive(string directory, string name)
{
    return Directory.EnumerateFileSystemEntries(directory)
        .FirstOrDefault(e => string.Equals(
            Path.GetFileName(e), name, StringComparison.OrdinalIgnoreCase));
}
```

### UxContextAdapter

Identiek aan `DosContext`, maar initialiseert vanuit de Unix-mappings in plaats van Windows-drives.

```csharp
internal class UxContextAdapter(UxFileSystemAdapter fileSystem) : Context(fileSystem)
{
    // CurrentDrive wordt ingesteld op de drive die gemapped is naar de startdirectory
}
```

## TDD — Test-first aanpak

**Strategie:** Alle bestaande `DosFileSystem`-tests worden als **gedeelde testklasse** opgezet
zodat dezelfde tests ook draaien voor `UxFileSystemAdapter`. Dit garandeert dat beide
implementaties identiek gedrag vertonen voor de abstracte IFileSystem-interface.

```csharp
// Abstracte testklasse — wordt gedeeld
public abstract class FileSystemContractTests
{
    protected abstract IFileSystem CreateFileSystem();
    protected abstract char DefaultDrive { get; }

    [Fact] public abstract void FileExists_ExistingFile_ReturnsTrue();
    [Fact] public abstract void CreateDirectory_CreatesDirectory();
    // ... alle contracttests
}

// DOS-implementatie (bestaand)
public class DosFileSystemTests : FileSystemContractTests
{
    protected override IFileSystem CreateFileSystem() => new DosFileSystem();
    protected override char DefaultDrive => 'C';
}

// Unix-implementatie (nieuw)
public class UxFileSystemAdapterTests : FileSystemContractTests
{
    private readonly string _tempRoot = Path.GetTempPath();

    protected override IFileSystem CreateFileSystem()
        => new UxFileSystemAdapter(new() { ['C'] = _tempRoot });

    protected override char DefaultDrive => 'C';
}
```

### Aanvullende Unix-specifieke tests

**Bestand:** `Bat.UnitTests/UxFileSystemTests.cs`

#### Test 1: GetNativePath vertaalt Windows-pad naar Unix-pad

```csharp
[Fact]
public void GetNativePath_TranslatesWindowsToUnix()
{
    var fs = new UxFileSystemAdapter(new() { ['C'] = "/home/user" });
    var native = fs.GetNativePath('C', ["Projects", "bat"]);
    Assert.Equal("/home/user/Projects/bat", native);
}
```

#### Test 2: Case-insensitieve lookup vindt bestand

```csharp
[Fact]
public void FileExists_CaseInsensitive_FindsFile()
{
    // Maak bestand "Hello.txt" aan op Unix (lowercase directory)
    var dir = Path.GetTempPath();
    var file = Path.Combine(dir, "Hello.txt");
    File.WriteAllText(file, "test");

    try
    {
        var fs = new UxFileSystemAdapter(new() { ['C'] = dir });
        // Zoek met andere case
        Assert.True(fs.FileExists('C', ["hello.txt"]));
        Assert.True(fs.FileExists('C', ["HELLO.TXT"]));
    }
    finally
    {
        File.Delete(file);
    }
}
```

#### Test 3: Niet-gemapte drive geeft fout

```csharp
[Fact]
public void FileExists_UnmappedDrive_ThrowsOrReturnsFalse()
{
    var fs = new UxFileSystemAdapter(new() { ['C'] = "/tmp" });
    // Drive Q bestaat niet in mappings
    Assert.Throws<DriveNotFoundException>(() => fs.FileExists('Q', ["file.txt"]));
}
```

#### Test 4: GetFullPathDisplayName toont Windows-stijl pad

```csharp
[Fact]
public void GetFullPathDisplayName_ShowsWindowsPath()
{
    var fs = new UxFileSystemAdapter(new() { ['C'] = "/home/user" });
    var display = fs.GetFullPathDisplayName('C', ["Projects"]);
    Assert.Equal(@"C:\Projects", display);
}
```

#### Test 5: DriveExists — gemapte drive bestaat

```csharp
[Fact]
public void DriveExists_MappedDrive_ReturnsTrue()
{
    var fs = new UxFileSystemAdapter(new() { ['C'] = "/tmp", ['P'] = "/mnt/p" });
    Assert.True(fs.DriveExists('C'));
    Assert.True(fs.DriveExists('P'));
    Assert.False(fs.DriveExists('Q'));
}
```

#### Test 6: CreateDirectory maakt Unix-map aan

```csharp
[Fact]
public void CreateDirectory_CreatesUnixDirectory()
{
    var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(root);

    try
    {
        var fs = new UxFileSystemAdapter(new() { ['C'] = root });
        fs.CreateDirectory('C', ["TestDir"]);
        Assert.True(Directory.Exists(Path.Combine(root, "TestDir")));
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}
```

#### Test 7: Batchbestand met Windows-paden draait op Unix

```csharp
[Fact]
public async Task BatchFile_WithWindowsPaths_RunsOnUnix()
{
    var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(root);

    try
    {
        var fs = new UxFileSystemAdapter(new() { ['C'] = root });
        var ctx = new UxContext(fs);

        // Schrijf batch naar virtuele C:\test.bat
        fs.WriteFile('C', ["test.bat"], "echo hello from batch\r\ncd \\");
        var console = new TestConsole();

        await BatchExecutor.ExecuteAsync('C', ["test.bat"], [], ctx, console);

        Assert.Equal("hello from batch", console.OutputLines.Single());
        Assert.Equal(Array.Empty<string>(), ctx.CurrentPath);  // CD \ → root
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}
```

## Implementatie Volgorde

1. Schrijf shared contract tests voor `IFileSystem` (refactor bestaande DosFileSystem-tests)
2. Schrijf Unix-specifieke tests (rood)
3. Implementeer `UxFileSystemAdapter.GetNativePath`
4. Implementeer case-insensitieve bestandsopzoeklogica (`FindCaseInsensitive`)
5. Implementeer alle `IFileSystem`-methoden (delegeer naar System.IO na padvertaling)
6. Implementeer `DriveExists` op basis van `_mappings`-dictionary
7. Implementeer `UxContextAdapter`
8. Koppel modusdetectie uit stap 12 aan de juiste factory (`DosContext` vs. `UxContextAdapter`)
9. Run contract tests voor beide implementaties → beide groen
10. Run batchbestand-integratietest op Unix → groen

## Aandachtspunten

- **Symlinks op Unix:** Volg symlinks bij padopzoeken (`FileInfo.ResolveLinkTarget`)
- **Bestandsrechten:** `ATTRIB` (stap 51) kan bepaalde Unix-rechten niet uitdrukken; documenteer beperkingen
- **Executables:** `.exe`/`.com`/`.bat`-extensies worden op Unix genegeerd bij PATH-lookup — zoek
  ook naar bestanden zonder extensie (stap 6 aandachtspunt)
- **Regeleinden:** Batchbestanden geschreven op Windows hebben `\r\n`; `BatchExecutor.ReadNextLine`
  moet beide ondersteunen

## Acceptance Criteria (Definition of Done)

- [ ] `UxFileSystemAdapter` implementeert alle `IFileSystem`-methoden
- [ ] Alle DosFileSystem-contracttests slagen ook voor `UxFileSystemAdapter`
- [ ] Case-insensitieve bestandslookup werkt op Unix
- [ ] `GetNativePath` vertaalt `C:\Users\Bart` → `/home/user/Users/Bart` correct
- [ ] `GetFullPathDisplayName` toont altijd Windows-stijl (`C:\...`)
- [ ] Niet-gemapte drives geven `DriveNotFoundException`
- [ ] Batchbestand met Windows-paden draait correct op Linux/macOS
- [ ] Modusdetectie in `Program.Main` kiest automatisch de juiste implementatie
- [ ] Alle bestaande tests slagen nog steeds
