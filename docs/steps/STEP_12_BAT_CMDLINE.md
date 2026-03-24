# STEP 12 - Command Line Parameters van Bat

**Doel:** Bat accepteert command line opties die grotendeels compatibel zijn met CMD.EXE, met een
aantal uitbreidingen en een automatische Windows/Unix-modusdetectie.

## Modusdetectie: Windows vs. Unix

Bat detecteert de actieve modus op basis van het **native directory-scheidingsteken** van het
hostsysteem:

| Situatie | Modus | Vlagprefix |
|---|---|---|
| `Path.DirectorySeparatorChar == '\\'` | Windows-modus | `/` (zoals CMD.EXE) |
| `Path.DirectorySeparatorChar == '/'` | Unix-modus | `-` |

In **Unix-modus** kunnen vlaggen worden gecombineerd: `-cq` staat gelijk aan `/C /Q`.

De modus beïnvloedt ook welke `IFileSystem`-implementatie wordt gebruikt:
- Windows → `DosFileSystem`
- Unix → `UxFileSystemAdapter` (stap 13)

## Volledige Syntax

### Windows-modus

```
BAT [/A | /U] [/Q] [/D] [/E:ON | /E:OFF] [/F:ON | /F:OFF]
    [/M:driveletter path [/M:driveletter path ...]]
    [/V:ON | /V:OFF] [/T:fg]
    [[/S] [/C | /K] string]
```

### Unix-modus

Identieke opties, maar met `-` als prefix. Vlaggen zonder waarde mogen gecombineerd worden:

```
bat [-a | -u] [-q] [-d] [-e:on | -e:off] [-f:on | -f:off]
    [-m driveletter path [-m driveletter path ...]]
    [-v:on | -v:off] [-t:fg]
    [[-s] [-c | -k] string]
```

Combinatievoorbeelden:
- `-cq`  = `/C /Q`
- `-kve:on` = `/K /V:ON /E:ON`

## Vlagbeschrijvingen

### Uitvoeringscontrole

```
/C string   Voer de opgegeven opdracht uit en sluit daarna af.
/K string   Voer de opdracht uit maar blijf actief (interactieve REPL).
/S          Pas de behandeling van string na /C of /K aan (zie hieronder).
```

**`/S`-gedrag:** Zet de string tussen dubbele aanhalingstekens als hij begint en eindigt
met `"`. Anders wordt de string ongewijzigd doorgegeven.

### Uitvoercodering

```
/A          Uitvoer van interne commando's naar pipe/bestand: ANSI-codering.
/U          Uitvoer van interne commando's naar pipe/bestand: Unicode (UTF-16 LE).
```

Standaard: platform-native (UTF-8 op Unix, systeemcodepage op Windows).

### Omgevingsvlaggen (context-initialisatie)

```
/Q          Echo uitschakelen bij opstart (standaard: aan).
/D          Sla AutoRun-commando's over (niet relevant buiten Windows; wordt genegeerd).
/E:ON       Commando-extensies inschakelen (standaard: aan).
/E:OFF      Commando-extensies uitschakelen.
/F:ON       Bestandsnaamcompletion inschakelen.
/F:OFF      Bestandsnaamcompletion uitschakelen (standaard).
/V:ON       Vertraagde variabele-expansie inschakelen (gebruik ! als scheidingsteken).
/V:OFF      Vertraagde variabele-expansie uitschakelen (standaard).
/T:fg       Stel voorgrond-/achtergrondkleur in (zie COLOR /? voor codes).
```

### Drive-mappings

```
/M:driveletter path
```

Wijs een schijfletter toe aan een extern pad. Meerdere `/M`-opties zijn mogelijk.

**Standaardgedrag zonder /M:**
- Windows-modus: `Z:` → `C:\` (alleen de systeemschijf; geen andere drives automatisch).
- Unix-modus: `Z:` → `/` (rootdirectory).

**Met /M:**
- **Vervangt** de volledige standaardmapping — voegt niet toe.
- De eerste `/M` gooit de standaard `Z:`-mapping weg; alleen de expliciet opgegeven
  mappings zijn daarna geldig.
- Meerdere `/M`-opties zijn mogelijk om meerdere drives te definiëren.

```
# Voorbeeld Unix:
bat /M:C /home/user /M:P /mnt/projects

# Resultaat in Bat:
# C: → /home/user
# P: → /mnt/projects
```

Elke `/M`-mapping roept intern `FileSystem.AddSubst()` aan (dezelfde infrastructuur als stap 10).

### Vertaalmodus

```
/T          Vertaal het hostcommando (inclusief paden en parameters) naar een intern commando.
            Paden moeten mappeerbaar zijn via de geconfigureerde drive-mappings.
```

Gebruik: roep een native tool aan met een Windows-pad, en `/T` zorgt dat het vertaald
wordt naar een intern Bat-commando zodat het werkt binnen de virtuele filesysteemlaag.

## Context-initialisatie

Na het parsen van de parameters wordt `IContext` geïnitialiseerd in deze volgorde:

1. Maak `IFileSystem` aan (DosFileSystem of UxFileSystemAdapter op basis van modus)
2. Verwijs alle `/M`-mappings als roots in de filesystem
3. **Stel de initiële werkdirectory in** (zie hieronder)
4. Stel beginwaarden in op `IContext`:
   - `EchoEnabled = !hasQ`
   - `DelayedExpansion = /V:ON`
   - `ExtensionsEnabled = /E:ON | default true`
   - `FilenameCompletion = /F:ON`
   - Uitvoercodering = `/A` of `/U` of default
5. Pas `/T:fg` toe op de console
6. Voer `/C` of `/K` string uit, of start REPL

### Initiële werkdirectory

Bat start altijd met de **native werkdirectory** van het host-proces
(`Environment.CurrentDirectory`). Die wordt omgezet naar een virtuele drive + pad
via de geconfigureerde `/M`-mappings.

**Algoritme:**

1. Haal native CWD op: bijv. `/home/bart/projects`
2. Doorloop alle gedefinieerde drive-mappings, langste native root eerst.
3. Als de native CWD begint met de root van een mapping:
   - Bereken het relatieve deel: `/home/bart/projects` onder `/home/bart` → `projects`
   - Stel in: drive = die schijfletter, pad = `["projects"]`
   - Stop.
4. Als geen enkele mapping de CWD dekt:
   - Stel in: drive = eerste schijfletter (op volgorde van opgave), pad = `[]` (root)

**Voorbeeld (Unix, `-M C=/ -M D=/home/bart`):**

| Native CWD | Langste overeenkomende root | Virtueel |  |
|---|---|---|---|
| `/home/bart/projects` | `D=/home/bart` | `D:\projects` | ✓ eerste match |
| `/usr/bin` | `C=/` | `C:\usr\bin` | ✓ via C: want geen D-match |
| `/mnt/data` | (geen) | `C:\` (fallback root van C:) | geen match |

**Voorbeeld (Windows, `/M C=C:\Windows /M D=C:\Users\Bart`):**

| Native CWD | Match | Virtueel |
|---|---|---|
| `C:\Users\Bart\projects` | `D=C:\Users\Bart` | `D:\projects` |
| `C:\Windows\System32` | `C=C:\Windows` | `C:\System32` |
| `C:\Temp` | (geen) | `C:\` (fallback) |

> De **langste root eerst** (longest prefix match) voorkomt dat een korte mapping
> zoals `C=/` voorrang heeft op een specifiekere `D=/home/bart`.

### Test 9: Initiële werkdirectory bij opstarten

```csharp
[Fact]
public void BuildContext_CwdMappedToLongestMatchingDrive()
{
    var args = new BatArguments
    {
        DriveMappings = new() { ['C'] = "/", ['D'] = "/home/bart" },
        NativeCwd = "/home/bart/projects",
        Mode = BatMode.Unix
    };
    var ctx = BatContextFactory.Create(args);

    Assert.Equal('D', ctx.CurrentDrive);
    CollectionAssert.AreEqual(new[] { "projects" }, ctx.CurrentPath);
}

[Fact]
public void BuildContext_CwdUnmapped_FallsBackToFirstDriveRoot()
{
    var args = new BatArguments
    {
        DriveMappings = new() { ['C'] = "/home/bart" },
        NativeCwd = "/mnt/data",
        Mode = BatMode.Unix
    };
    var ctx = BatContextFactory.Create(args);

    Assert.Equal('C', ctx.CurrentDrive);
    Assert.Empty(ctx.CurrentPath);
}
```

## TDD — Stap voor stap

**Bestand:** `Bat.UnitTests/CommandLineParserTests.cs`

### Test 1: Modusdetectie

```csharp
[Fact]
public void ParseMode_WindowsSeparator_IsWindowsMode()
{
    var parser = new BatArgumentParser(directorySeparator: '\\');
    var args = parser.Parse(["/C", "echo hello"]);
    Assert.Equal(BatMode.Windows, args.Mode);
}

[Fact]
public void ParseMode_UnixSeparator_IsUnixMode()
{
    var parser = new BatArgumentParser(directorySeparator: '/');
    var args = parser.Parse(["-c", "echo hello"]);
    Assert.Equal(BatMode.Unix, args.Mode);
}
```

### Test 2: /C en /K

```csharp
[Fact]
public void Parse_SlashC_SetsCommandAndTerminate()
{
    var args = ParseWindows("/C", "echo hello");
    Assert.Equal("echo hello", args.Command);
    Assert.Equal(BatExitBehavior.TerminateAfterCommand, args.ExitBehavior);
}

[Fact]
public void Parse_SlashK_SetsCommandAndKeepAlive()
{
    var args = ParseWindows("/K", "echo hello");
    Assert.Equal("echo hello", args.Command);
    Assert.Equal(BatExitBehavior.KeepAliveAfterCommand, args.ExitBehavior);
}
```

### Test 3: Gecombineerde Unix-vlaggen

```csharp
[Fact]
public void Parse_Unix_CombinedFlags_ParsesAll()
{
    var args = ParseUnix("-cq", "echo hello");
    Assert.Equal(BatExitBehavior.TerminateAfterCommand, args.ExitBehavior);
    Assert.False(args.EchoEnabled);
}
```

### Test 4: /M drive-mapping

```csharp
[Fact]
public void Parse_MultipleM_CreatesMappings()
{
    var args = ParseUnix("-m", "C", "/home/user", "-m", "P", "/mnt/projects");
    Assert.Equal(2, args.DriveMappings.Count);
    Assert.Equal("/home/user",    args.DriveMappings['C']);
    Assert.Equal("/mnt/projects", args.DriveMappings['P']);
}
```

### Test 5: /V:ON zet DelayedExpansion

```csharp
[Fact]
public void Parse_VOn_SetsDelayedExpansion()
{
    var args = ParseWindows("/V:ON");
    Assert.True(args.DelayedExpansion);
}
```

### Test 6: /Q schakelt echo uit

```csharp
[Fact]
public void Parse_Q_DisablesEcho()
{
    var args = ParseWindows("/Q");
    Assert.False(args.EchoEnabled);
}
```

### Test 7: Standaardwaarden zonder vlaggen

```csharp
[Fact]
public void Parse_NoFlags_DefaultValues()
{
    var args = ParseWindows();
    Assert.True(args.EchoEnabled);
    Assert.False(args.DelayedExpansion);
    Assert.True(args.ExtensionsEnabled);
    Assert.Null(args.Command);
    Assert.Equal(BatExitBehavior.Repl, args.ExitBehavior);
}
```

### Test 8: Context-initialisatie vanuit BatArguments

```csharp
[Fact]
public void BuildContext_AppliesMappingsToFileSystem()
{
    var args = new BatArguments
    {
        DriveMappings = new() { ['P'] = "/mnt/projects" },
        Mode = BatMode.Unix
    };
    var ctx = BatContextFactory.Create(args);

    Assert.True(ctx.FileSystem.DriveExists('P'));
    Assert.Equal("/mnt/projects", ctx.FileSystem.GetSubsts()['P']);
}
```

## Implementatie Volgorde

1. Schrijf alle tests (rood)
2. Maak `BatArguments`-record met alle velden
3. Maak `BatArgumentParser` die Windows- en Unix-vlaggen herkent
4. Implementeer gecombineerde Unix-vlagparsing (`-cq`)
5. Implementeer `/M`-mapping parsing
6. Maak `BatContextFactory.Create(BatArguments)` die `IContext` opbouwt
7. Koppel `BatArgumentParser` aan `Program.Main`
8. Run alle tests → groen

## Acceptance Criteria (Definition of Done)

- [ ] Windows-modus herkent `/`-vlaggen, Unix-modus herkent `-`-vlaggen
- [ ] Unix-vlaggen mogen gecombineerd worden (`-cq`, `-kve:on`)
- [ ] `/C string` voert commando uit en sluit af
- [ ] `/K string` voert commando uit en blijft in REPL
- [ ] `/Q` schakelt echo uit bij opstart
- [ ] `/V:ON` / `/V:OFF` configureert vertraagde expansie
- [ ] `/E:ON` / `/E:OFF` configureert extensies
- [ ] `/M:driveletter path` mapt een drive in de filesysteemlaag
- [ ] Meerdere `/M`-opties worden alle verwerkt
- [ ] Standaardmapping `/`→`C:` geldt op Unix zonder `/M`
- [ ] Initiële werkdirectory wordt gezet via longest-prefix-match op de `/M`-mappings
- [ ] Fallback naar root van eerste drive als CWD niet mappeerbaar is
- [ ] `/T` markeert vertaalverzoek van hostcommando (implementatie mag stub zijn)
- [ ] Alle bestaande tests slagen nog steeds
