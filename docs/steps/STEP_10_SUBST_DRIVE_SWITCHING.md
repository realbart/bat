# STEP 07 - SUBST + Drive Switching

**Doel:** Virtual drive mapping via SUBST (in IFileSystem) en intern drive switching-commando (`D:`).

## Achtergrond

### Real CMD gedrag (test EERST in echte CMD!)

```cmd
C:\> subst /?
SUBST [drive1: [drive2:]path]
SUBST drive1: /D

  drive1:        Specifies a virtual drive to which you want to assign a path.
  [drive2:]path  Specifies a physical drive and path you want to assign to
                 a virtual drive.
  /D             Deletes a substituted (virtual) drive.

Type SUBST with no parameters to display a list of current virtual drives.

C:\> subst Q: C:\Temp
C:\> Q:
Q:\> cd .vscode
Q:\.vscode> subst Q: /D
Q:\.vscode> dir
The system cannot find the path specified.
Q:\.vscode> C:
C:\> Q:
The system cannot find the drive specified.
```

**Samenvatting van edge cases:**

| Situatie | Gedrag |
|---|---|
| `Q:` (bestaat) | Wisselt naar Q:, behoudt per-drive dir |
| `Q:` (bestaat niet) | `The system cannot find the drive specified.` |
| `SUBST Q: /D` terwijl op Q: | Drive verdwijnt, prompt toont nog Q: |
| `dir` na verdwijnen drive | `The system cannot find the path specified.` |
| `C:` vanuit Q: | Wisselt terug, prompt toont gesaved C: pad |

## Architectuurbeslissingen

### 1. SUBST werkt via IFileSystem (niet system-wide)

`SUBST` manipuleert de virtuele schijfmappings in `IFileSystem` — **niet** de echte Windows
SUBST API (`DefineDosDevice`). Voordelen:
- Volledig testbaar zonder adminrechten
- Werkt op Unix
- Sandbox: Bat-substs zijn pas zichtbaar buiten Bat als de DosFileSystem-implementatie dat doorkoppelt

### 2. Drive switching is intern Bat-commando

Een invoer als `Q:` (een drive letter gevolgd door een dubbele punt, niks anders) is een **intern commando** in Bat,
niet een externe executable. De dispatcher herkent dit patroon voor externe executables.

### 3. Per-drive paden zitten in IContext

`IContext` houdt een `Dictionary<char, string[]>` bij met het huidige pad per drive. Dit is
nodig voor:
- `C:` → terug naar C: met het gesaved pad
- SETLOCAL snapshots (stap 8)

## Benodigde wijzigingen

### A. IFileSystem uitbreiden

**Bestand:** `Context/IFileSystem.cs`

```csharp
namespace Context;

public interface IFileSystem
{
    // Bestaand:
    string GetFullPathDisplayName(char drive, string[] path);
    string GetNativePath(char drive, string[] path);
    string GetDisplayName(string segment);

    // NIEUW — drive management:
    bool DriveExists(char drive);
    IReadOnlyDictionary<char, string> GetSubsts();
    void AddSubst(char drive, string nativePath);
    void RemoveSubst(char drive);
}
```

**`DriveExists` logica per implementatie:**
- `DosFileSystem`: check SUBST-dictionary ÓEIRST, dan `Directory.GetLogicalDrives()`
- Test-implementatie: alleen SUBST-dictionary (plus evt. een set van "echte" drives)

### B. IContext uitbreiden

**Bestand:** `Context/IContext.cs`

```csharp
public interface IContext
{
    // Bestaand:
    char CurrentDrive { get; }
    string[] CurrentPath { get; }
    string CurrentPathDisplayName { get; }
    Dictionary<string, string> EnvironmentVariables { get; }
    int ErrorCode { get; set; }
    IFileSystem FileSystem { get; }

    // NIEUW — rijfstation beheer:
    bool SwitchDrive(char drive);                         // false als drive niet bestaat
    string[] GetPathForDrive(char drive);                 // Leeg array als nog niet bezocht
    void SetPathForDrive(char drive, string[] path);      // Voor CD /D en interne use
    IReadOnlyDictionary<char, string[]> GetAllDrivePaths(); // Voor SETLOCAL snapshots
}
```

> `CurrentDrive` blijft **read-only** in de interface; muteren gaat via `SwitchDrive`.

### C. Context implementatie aanpassen

**Bestand:** `Bat/Context/Context.cs`

```csharp
internal abstract class Context(IFileSystem fileSystem) : IContext
{
    public int ErrorCode { get; set; } = 0;
    public Dictionary<string, string> EnvironmentVariables { get; } = [];

    private char _currentDrive = 'C';
    public char CurrentDrive => _currentDrive;

    private readonly Dictionary<char, string[]> _drivePaths = [];

    public string[] CurrentPath => _drivePaths.TryGetValue(_currentDrive, out var p) ? p : [];
    public string CurrentPathDisplayName =>
        fileSystem.GetFullPathDisplayName(CurrentDrive, CurrentPath);
    public IFileSystem FileSystem => fileSystem;

    public bool SwitchDrive(char drive)
    {
        if (!fileSystem.DriveExists(drive))
            return false;
        _currentDrive = drive;
        return true;
    }

    public string[] GetPathForDrive(char drive) =>
        _drivePaths.TryGetValue(drive, out var p) ? p : [];

    public void SetPathForDrive(char drive, string[] path) =>
        _drivePaths[drive] = path;

    public IReadOnlyDictionary<char, string[]> GetAllDrivePaths() => _drivePaths;
}
```

## TDD — Stap voor stap

### Fase 1: IFileSystem uitbreidingen testen

**Bestand:** `Bat.UnitTests/FileSystemTests.cs`

#### Test 1.1: DriveExists — bekende drive

```csharp
[Fact]
public void DriveExists_SubstedDrive_ReturnsTrue()
{
    // Arrange
    var fs = new TestFileSystem();
    fs.AddSubst('Q', @"C:\Temp");

    // Act + Assert
    Assert.True(fs.DriveExists('Q'));
}
```

#### Test 1.2: DriveExists — onbekende drive

```csharp
[Fact]
public void DriveExists_UnknownDrive_ReturnsFalse()
{
    var fs = new TestFileSystem();
    Assert.False(fs.DriveExists('Q'));
}
```

#### Test 1.3: AddSubst / GetSubsts

```csharp
[Fact]
public void AddSubst_ThenGetSubsts_ReturnsMapping()
{
    var fs = new TestFileSystem();
    fs.AddSubst('Q', @"C:\Temp");

    var substs = fs.GetSubsts();

    Assert.Single(substs);
    Assert.Equal(@"C:\Temp", substs['Q']);
}
```

#### Test 1.4: RemoveSubst verwijdert de drive

```csharp
[Fact]
public void RemoveSubst_RemovesDrive()
{
    var fs = new TestFileSystem();
    fs.AddSubst('Q', @"C:\Temp");
    fs.RemoveSubst('Q');

    Assert.False(fs.DriveExists('Q'));
    Assert.Empty(fs.GetSubsts());
}
```

#### Test 1.5: RemoveSubst van niet-bestaande drive is noop

```csharp
[Fact]
public void RemoveSubst_NonExistentDrive_DoesNotThrow()
{
    var fs = new TestFileSystem();
    // Should not throw:
    fs.RemoveSubst('Q');
}
```

### Fase 2: Drive switching testen

**Bestand:** `Bat.UnitTests/DriveSwitchingTests.cs`

#### Test 2.1: Wisselen naar bestaande drive lukt

```csharp
[Fact]
public void SwitchDrive_ExistingDrive_SwitchesDrive()
{
    var fs = new TestFileSystem();
    fs.AddSubst('Q', @"C:\Temp");
    var ctx = new TestContext(fs);

    ctx.SwitchDrive('Q');

    Assert.Equal('Q', ctx.CurrentDrive);
}
```

#### Test 2.2: Wisselen naar niet-bestaande drive geeft false

```csharp
[Fact]
public void SwitchDrive_NonExistentDrive_ReturnsFalse()
{
    var fs = new TestFileSystem();
    var ctx = new TestContext(fs);

    Assert.False(ctx.SwitchDrive('Q'));
}
```

#### Test 2.3: Per-drive pad wordt onthouden

```csharp
[Fact]
public void SwitchDrive_RemembersPathPerDrive()
{
    var fs = new TestFileSystem();
    fs.AddSubst('Q', @"C:\Temp");
    var ctx = new TestContext(fs);

    // Sla pad op voor C:
    ctx.SetPathForDrive('C', ["Users", "Bart"]);
    // Wissel naar Q:
    ctx.SwitchDrive('Q');
    ctx.SetPathForDrive('Q', [".vscode"]);

    // Wissel terug naar C:
    ctx.SwitchDrive('C');
    Assert.Equal(["Users", "Bart"], ctx.CurrentPath);

    // Wissel naar Q: — pad wordt hersteld
    ctx.SwitchDrive('Q');
    Assert.Equal([".vscode"], ctx.CurrentPath);
}
```

#### Test 2.4: Staan op drive die verdwijnt — SwitchDrive geeft error

```csharp
[Fact]
public void SwitchDrive_AfterRemoveSubst_ReturnsFalse()
{
    var fs = new TestFileSystem();
    fs.AddSubst('Q', @"C:\Temp");
    var ctx = new TestContext(fs);
    ctx.SwitchDrive('Q');

    // Verwijder de subst terwijl we op Q: staan
    fs.RemoveSubst('Q');

    // Terug naar Q: is nu niet meer mogelijk
    ctx.SwitchDrive('C');
    Assert.False(ctx.SwitchDrive('Q'));
}
```

### Fase 3: Drive-switching intern commando testen

Het intern commando herkent het patroon `<letter>:` (alleen een drive letter + dubbele punt).

**Bestand:** `Bat.UnitTests/DriveCommandTests.cs`

#### Test 3.1: Drive letter commando wisselt drive

```csharp
[Fact]
public async Task DriveCommand_ValidDrive_SwitchesDrive()
{
    var fs = new TestFileSystem();
    fs.AddSubst('Q', @"C:\Temp");
    var ctx = new TestContext(fs);
    var command = new DriveCommand('Q');

    await command.ExecuteAsync(ctx, [], ReplBatchContext.Value, []);

    Assert.Equal('Q', ctx.CurrentDrive);
}
```

#### Test 3.2: Drive letter commando — drive niet gevonden

```csharp
[Fact]
public async Task DriveCommand_InvalidDrive_WritesError()
{
    var fs = new TestFileSystem();
    var ctx = new TestContext(fs);
    var console = new TestConsole();
    var command = new DriveCommand('Q', console);

    await command.ExecuteAsync(ctx, [], ReplBatchContext.Value, []);

    Assert.Contains("The system cannot find the drive specified.", console.ErrorOutput);
    Assert.Equal(1, ctx.ErrorCode);
    Assert.Equal('C', ctx.CurrentDrive);  // Niet veranderd
}
```

#### Test 3.3: Dispatcher herkent drive letter patroon

```csharp
[Fact]
public void Parser_DriveLetter_ParsesAsDriveCommand()
{
    var result = Parser.Parse("Q:");

    var node = Assert.IsType<DriveCommandNode>(result);
    Assert.Equal('Q', node.Drive);
}
```

### Fase 4: SUBST executable testen

**Bestand:** `Bat.UnitTests/SubstCommandTests.cs`

#### Test 4.1: `subst` zonder args toont alle mappings

```csharp
[Fact]
public async Task Subst_NoArgs_PrintsAllMappings()
{
    var fs = new TestFileSystem();
    fs.AddSubst('Q', @"C:\Temp");
    fs.AddSubst('Z', @"C:\Projects");
    var ctx = new TestContext(fs);
    var console = new TestConsole();

    await Subst.Program.Main(ctx, console);  // overload met testable console

    Assert.Contains("Q: => C:\\Temp", console.Output);
    Assert.Contains("Z: => C:\\Projects", console.Output);
}
```

#### Test 4.2: `subst Q: C:\Temp` voegt drive toe

```csharp
[Fact]
public async Task Subst_AddDrive_AddsToFileSystem()
{
    var fs = new TestFileSystem();
    var ctx = new TestContext(fs);

    await Subst.Program.Main(ctx, "Q:", @"C:\Temp");

    Assert.True(fs.DriveExists('Q'));
    Assert.Equal(@"C:\Temp", fs.GetSubsts()['Q']);
}
```

#### Test 4.3: `subst Q: /D` verwijdert drive

```csharp
[Fact]
public async Task Subst_DeleteDrive_RemovesFromFileSystem()
{
    var fs = new TestFileSystem();
    fs.AddSubst('Q', @"C:\Temp");
    var ctx = new TestContext(fs);

    await Subst.Program.Main(ctx, "Q:", "/D");

    Assert.False(fs.DriveExists('Q'));
}
```

#### Test 4.4: `subst Q: /D` op niet-bestaande drive geeft foutmelding

```csharp
[Fact]
public async Task Subst_DeleteNonExistent_PrintsError()
{
    var fs = new TestFileSystem();
    var ctx = new TestContext(fs);
    var console = new TestConsole();

    var exitCode = await Subst.Program.Main(ctx, console, "Q:", "/D");

    Assert.NotEqual(0, exitCode);
    Assert.Contains("Invalid parameter", console.ErrorOutput);
}
```

#### Test 4.5: `subst Q:` zonder pad geeft foutmelding

```csharp
[Fact]
public async Task Subst_DriveWithoutPath_PrintsError()
{
    var fs = new TestFileSystem();
    var ctx = new TestContext(fs);
    var console = new TestConsole();

    var exitCode = await Subst.Program.Main(ctx, console, "Q:");

    Assert.NotEqual(0, exitCode);
}
```

### Fase 5: Integratietest

```csharp
[Fact]
public async Task Integration_SubstAndSwitch_FullScenario()
{
    // Arrange: filesystem met C: als echte drive
    var fs = new TestFileSystem(realDrives: ['C']);
    var ctx = new TestContext(fs);

    // Stap 1: SUBST Q: C:\Temp
    await Subst.Program.Main(ctx, "Q:", @"C:\Temp");
    Assert.True(fs.DriveExists('Q'));

    // Stap 2: Q: — wissel naar Q:
    var driveCmd = new DriveCommand('Q');
    await driveCmd.ExecuteAsync(ctx, [], ReplBatchContext.Value, []);
    Assert.Equal('Q', ctx.CurrentDrive);

    // Stap 3: SUBST Q: /D — verwijder mapping
    await Subst.Program.Main(ctx, "Q:", "/D");
    Assert.False(fs.DriveExists('Q'));

    // Stap 4: Nog steeds op Q: (maar dir geeft path error)
    Assert.Equal('Q', ctx.CurrentDrive);

    // Stap 5: C: — wissel terug
    var cDriveCmd = new DriveCommand('C');
    await cDriveCmd.ExecuteAsync(ctx, [], ReplBatchContext.Value, []);
    Assert.Equal('C', ctx.CurrentDrive);

    // Stap 6: Q: — drive bestaat niet meer
    var qDriveCmd = new DriveCommand('Q', new TestConsole());
    await qDriveCmd.ExecuteAsync(ctx, [], ReplBatchContext.Value, []);
    Assert.Equal('C', ctx.CurrentDrive);  // niet gewisseld
}
```

## Implementatie Volgorde

1. Schrijf alle tests (ze falen — expected)
2. Voeg nieuwe methoden toe aan `IFileSystem` interface
3. Maak `TestFileSystem` voor unit tests (alleen subst dictionary, geen OS-aanroepen)
4. Implementeer `IContext` uitbreidingen (SwitchDrive, GetPathForDrive, SetPathForDrive, GetAllDrivePaths)
5. Implementeer `Context.SwitchDrive` die `false` retourneert als drive niet bestaat
6. Voeg `DriveExists` toe aan `DosFileSystem` (subst dict + `DriveInfo.GetDrives()`)
7. Implementeer `DriveCommandNode` en `DriveCommand`
8. Voeg herkenning van `X:` patroon toe aan parser/dispatcher
9. Implementeer `Subst.Program.Main(IContext, ...)` — parse args, aanroepen filesystem
10. Run alle tests → alles groen

## Referentie: /? output van echte SUBST

```
Assigns a drive letter to a path.

SUBST [drive1: [drive2:]path]
SUBST drive1: /D

  drive1:        Specifies a virtual drive to which you want to assign a path.
  [drive2:]path  Specifies a physical drive and path you want to assign to
                 a virtual drive.
  /D             Deletes a substituted (virtual) drive.

Type SUBST with no parameters to display a list of current virtual drives.
```

## Acceptance Criteria (Definition of Done)

- [ ] `IFileSystem` heeft `DriveExists`, `GetSubsts`, `AddSubst`, `RemoveSubst`
- [ ] `IContext` heeft `SwitchDrive`, `GetPathForDrive`, `SetPathForDrive`, `GetAllDrivePaths`
- [ ] `Context.SwitchDrive` retourneert `false` als drive niet bestaat; aanroepende code schrijft CMD-tekst naar stderr
- [ ] `DosFileSystem` implementeert nieuwe methoden (subst dict + echte drives)
- [ ] `DriveCommand` is in-process commando (herkend door dispatcher/parser)
- [ ] `Subst.Program.Main(IContext, ...)` parset args en manipuleert FileSystem
- [ ] Alle unit tests in FileSystemTests, DriveSwitchingTests, DriveCommandTests, SubstCommandTests slagen
- [ ] Integratietest scenario slaagt
- [ ] Bestaande tests blijven slagen
