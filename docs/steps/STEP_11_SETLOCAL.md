# STEP 08 - SETLOCAL / ENDLOCAL

**Doel:** Sla omgevingsstaat op bij `setlocal` en herstel die bij `endlocal`, inclusief CMD extensions-vlaggen.

## Achtergrond

### Real CMD gedrag (test EERST in echte CMD!)

```cmd
C:\> set X=before
C:\> setlocal
C:\> set X=inside
C:\> echo %X%
inside
C:\> endlocal
C:\> echo %X%
before

C:\> REM --- Geneste setlocal ---
C:\> setlocal
C:\> setlocal
C:\> set X=deep
C:\> endlocal
C:\> echo %X%
before
C:\> endlocal
C:\> echo %X%
before

C:\> REM --- Flags ---
C:\> setlocal EnableDelayedExpansion
C:\> setlocal DisableExtensions
C:\> endlocal
C:\> setlocal EnableExtensions DisableDelayedExpansion

C:\> REM --- In batch file: automatisch opgeruimd bij exit ---
REM test.bat:
    setlocal
    set TEMP_VAR=hello
    exit /b 0
REM Na aanroep test.bat: TEMP_VAR is niet meer gezet
```

**Samenvatting van edge cases:**

| Situatie | Gedrag |
|---|---|
| `endlocal` op lege stack | Noop, geen fout |
| `setlocal` aan het einde van een batch | Automatisch opgeruimd bij exit |
| `setlocal` in REPL | Werkt, maar stapel gaat verloren bij afsluiten Bat |
| Geneste setlocal | Stack groeit; elke endlocal popt één niveau |
| `setlocal EnableDelayedExpansion` | Past vlag aan en snapt huidige omgeving |
| CD na setlocal / endlocal | Huidige directory voor alle drives wordt hersteld |

## Architectuurvraag: waar hoort SetLocalStack?

### Antwoord: in `BatchContext`

De `SetLocalStack` zit in `BatchContext.SetLocalStack` (een `Stack<EnvironmentSnapshot>`).

**Redenen:**
- Elke batch-aanroep via CALL heeft zijn eigen stack (CALL-nesting via `BatchContext.prev`)
- De REPL-`BatchContext` (singleton) heeft ook een stack → interactief gebruik werkt ook
- `ENDLOCAL` bij batch-exit ruimt automatisch de resterende stack op

**Relatie BatchContext ↔ IContext (belangrijk!):**

```
IContext
├── IFileSystem          (virtueel bestandssysteem)
├── EnvironmentVariables (globale env vars)
├── CurrentDrive / Paden (per-drive state)
└── CurrentBatch → BatchContext
                   ├── Parameters (%0..%9)
                   ├── FilePosition / LineNumber
                   ├── SetLocalStack ← hier!
                   └── prev → BatchContext (CALL-nesting)
```

`BatchContext` bevat **NIET** de `IContext`. Commands krijgen beide als losse parameters:

```csharp
Task<int> ExecuteAsync(IContext ctx, IReadOnlyList<IToken> args,
                       BatchContext bc, IReadOnlyList<Redirection> redirects);
```

## EnvironmentSnapshot record

`EnvironmentSnapshot` al gepland in STEP_01:

```csharp
public record EnvironmentSnapshot(
    Dictionary<string, string> Variables,
    Dictionary<char, string[]> DrivePaths,
    bool DelayedExpansion,
    bool ExtensionsEnabled
);
```

De `DrivePaths` worden verkregen via `IContext.GetAllDrivePaths()` (uitgebreid in STEP_07).

## Benodigde wijzigingen

### A. BatchContext aanpassen

**Bestand:** `Bat/Execution/BatchContext.cs`

```csharp
public class BatchContext
{
    // ... bestaand ...

    // SETLOCAL/ENDLOCAL stack
    public Stack<EnvironmentSnapshot> SetLocalStack { get; } = new();
}
```

### B. IContext uitbreiden (als nog niet gedaan in STEP_07)

```csharp
IReadOnlyDictionary<char, string[]> GetAllDrivePaths();
void RestoreAllDrivePaths(Dictionary<char, string[]> paths);
```

`RestoreAllDrivePaths` vervangt alle per-drive paden in één keer (voor endlocal).

### C. SetlocalCommand

```csharp
public class SetlocalCommand : ICommand
{
    public Task<int> ExecuteAsync(IContext ctx, IReadOnlyList<IToken> args,
                                  BatchContext bc, IReadOnlyList<Redirection> redirects)
    {
        // Parse flags uit args
        var snapshot = new EnvironmentSnapshot(
            new Dictionary<string, string>(ctx.EnvironmentVariables),
            new Dictionary<char, string[]>(ctx.GetAllDrivePaths()
                .ToDictionary(kv => kv.Key, kv => kv.Value.ToArray())),
            ctx.DelayedExpansion,
            ctx.ExtensionsEnabled
        );
        bc.SetLocalStack.Push(snapshot);

        // Pas flags toe
        foreach (var arg in args.Select(a => a.ToString().ToUpperInvariant()))
        {
            if (arg == "ENABLEDELAYEDEXPANSION")  ctx.DelayedExpansion = true;
            if (arg == "DISABLEDELAYEDEXPANSION") ctx.DelayedExpansion = false;
            if (arg == "ENABLEEXTENSIONS")        ctx.ExtensionsEnabled = true;
            if (arg == "DISABLEEXTENSIONS")       ctx.ExtensionsEnabled = false;
        }
        return Task.FromResult(0);
    }
}
```

### D. EndlocalCommand

```csharp
public class EndlocalCommand : ICommand
{
    public Task<int> ExecuteAsync(IContext ctx, IReadOnlyList<IToken> args,
                                  BatchContext bc, IReadOnlyList<Redirection> redirects)
    {
        if (!bc.SetLocalStack.TryPop(out var snapshot))
            return Task.FromResult(0);  // Lege stack → noop

        ctx.EnvironmentVariables.Clear();
        foreach (var kv in snapshot.Variables)
            ctx.EnvironmentVariables[kv.Key] = kv.Value;

        ctx.RestoreAllDrivePaths(snapshot.DrivePaths);
        ctx.DelayedExpansion = snapshot.DelayedExpansion;
        ctx.ExtensionsEnabled = snapshot.ExtensionsEnabled;

        return Task.FromResult(0);
    }
}
```

## TDD — Stap voor stap

### Fase 1: EnvironmentSnapshot testen

**Bestand:** `Bat.UnitTests/SetlocalTests.cs`

#### Test 1.1: Snapshot bewaart environment variables

```csharp
[Fact]
public async Task Setlocal_SnapshotsEnvironmentVariables()
{
    var ctx = CreateContext(envVars: new() { ["X"] = "before" });
    var bc = new BatchContext();

    await new SetlocalCommand().ExecuteAsync(ctx, [], bc, []);
    ctx.EnvironmentVariables["X"] = "inside";

    Assert.Single(bc.SetLocalStack);
    Assert.Equal("before", bc.SetLocalStack.Peek().Variables["X"]);
}
```

#### Test 1.2: Endlocal herstelt environment variables

```csharp
[Fact]
public async Task Endlocal_RestoresEnvironmentVariables()
{
    var ctx = CreateContext(envVars: new() { ["X"] = "before" });
    var bc = new BatchContext();

    await new SetlocalCommand().ExecuteAsync(ctx, [], bc, []);
    ctx.EnvironmentVariables["X"] = "inside";

    await new EndlocalCommand().ExecuteAsync(ctx, [], bc, []);

    Assert.Equal("before", ctx.EnvironmentVariables["X"]);
    Assert.Empty(bc.SetLocalStack);
}
```

#### Test 1.3: Endlocal op lege stack is noop

```csharp
[Fact]
public async Task Endlocal_EmptyStack_IsNoop()
{
    var ctx = CreateContext(envVars: new() { ["X"] = "val" });
    var bc = new BatchContext();

    // Geen exception, geen fout
    await new EndlocalCommand().ExecuteAsync(ctx, [], bc, []);

    Assert.Equal("val", ctx.EnvironmentVariables["X"]);
}
```

#### Test 1.4: Geneste setlocal / endlocal

```csharp
[Fact]
public async Task Setlocal_Nested_RestoresEachLevel()
{
    var ctx = CreateContext(envVars: new() { ["X"] = "level0" });
    var bc = new BatchContext();

    await new SetlocalCommand().ExecuteAsync(ctx, [], bc, []);
    ctx.EnvironmentVariables["X"] = "level1";

    await new SetlocalCommand().ExecuteAsync(ctx, [], bc, []);
    ctx.EnvironmentVariables["X"] = "level2";

    // Eerste endlocal: terug naar level1
    await new EndlocalCommand().ExecuteAsync(ctx, [], bc, []);
    Assert.Equal("level1", ctx.EnvironmentVariables["X"]);

    // Tweede endlocal: terug naar level0
    await new EndlocalCommand().ExecuteAsync(ctx, [], bc, []);
    Assert.Equal("level0", ctx.EnvironmentVariables["X"]);
}
```

#### Test 1.5: SetLocal bewaart per-drive paden

```csharp
[Fact]
public async Task Setlocal_SnapshotsDrivePaths()
{
    var fs = new TestFileSystem();
    var ctx = CreateContext(fs);
    ctx.SetPathForDrive('C', ["Users", "Bart"]);
    var bc = new BatchContext();

    await new SetlocalCommand().ExecuteAsync(ctx, [], bc, []);
    ctx.SetPathForDrive('C', ["Temp"]);

    await new EndlocalCommand().ExecuteAsync(ctx, [], bc, []);

    Assert.Equal(["Users", "Bart"], ctx.GetPathForDrive('C'));
}
```

#### Test 1.6: Setlocal EnableDelayedExpansion past vlag aan

```csharp
[Fact]
public async Task Setlocal_EnableDelayedExpansion_SetsFlagAndSnapshots()
{
    var ctx = CreateContext();
    ctx.DelayedExpansion = false;
    var bc = new BatchContext();
    var args = ParseTokens("EnableDelayedExpansion");

    await new SetlocalCommand().ExecuteAsync(ctx, args, bc, []);

    Assert.True(ctx.DelayedExpansion);
    Assert.False(bc.SetLocalStack.Peek().DelayedExpansion);  // snapshot heeft oude waarde
}
```

#### Test 1.7: Endlocal herstelt DelayedExpansion vlag

```csharp
[Fact]
public async Task Endlocal_RestoresDelayedExpansionFlag()
{
    var ctx = CreateContext();
    ctx.DelayedExpansion = false;
    var bc = new BatchContext();
    var args = ParseTokens("EnableDelayedExpansion");

    await new SetlocalCommand().ExecuteAsync(ctx, args, bc, []);
    Assert.True(ctx.DelayedExpansion);

    await new EndlocalCommand().ExecuteAsync(ctx, [], bc, []);
    Assert.False(ctx.DelayedExpansion);  // hersteld
}
```

### Fase 2: Batch exit ruimt SetLocalStack op

**Bestand:** `Bat.UnitTests/SetlocalTests.cs`

#### Test 2.1: Batch exit rolt resterende setlocals terug

```csharp
[Fact]
public async Task BatchExit_UnwindsSetLocalStack()
{
    var ctx = CreateContext(envVars: new() { ["X"] = "original" });
    var bc = new BatchContext();

    await new SetlocalCommand().ExecuteAsync(ctx, [], bc, []);
    ctx.EnvironmentVariables["X"] = "changed";

    // Simuleer batch-exit: BatchExecutor roept UnwindSetLocal aan
    BatchExecutor.UnwindSetLocal(ctx, bc);

    Assert.Equal("original", ctx.EnvironmentVariables["X"]);
    Assert.Empty(bc.SetLocalStack);
}
```

## Implementatie Volgorde

1. Schrijf alle tests (ze falen)
2. Maak/update `EnvironmentSnapshot` record met `DrivePaths` en flags
3. Voeg `SetLocalStack` toe aan `BatchContext` (als nog niet aanwezig)
4. Voeg `IContext.DelayedExpansion`, `IContext.ExtensionsEnabled` toe (zie STEP_01 design)
5. Voeg `IContext.RestoreAllDrivePaths()` toe
6. Implementeer `SetlocalCommand`
7. Implementeer `EndlocalCommand`
8. Voeg `BatchExecutor.UnwindSetLocal()` toe (loop + endlocal)
9. Roep `UnwindSetLocal` aan bij batch-exit
10. Registreer commando's in de dispatcher
11. Run alle tests → alles groen

## Referentie: /? output van echte SETLOCAL

```
Begins localization of environment changes in a batch file.
Environment changes made after SETLOCAL has been issued are local to the
batch file. ENDLOCAL must be issued to restore the previous settings.
When the end of a batch file is reached, an implied ENDLOCAL is issued for
any outstanding SETLOCAL commands issued by that batch file.

SETLOCAL [ENABLEEXTENSIONS | DISABLEEXTENSIONS]
         [ENABLEDELAYEDEXPANSION | DISABLEDELAYEDEXPANSION]

  ENABLEEXTENSIONS     Enable command extensions until the matching
                       ENDLOCAL command.
  DISABLEEXTENSIONS    Disable command extensions until the matching
                       ENDLOCAL command.
  ENABLEDELAYEDEXPANSION  Enable delayed environment variable
                       expansion until the matching ENDLOCAL command.
  DISABLEDELAYEDEXPANSION  Disable delayed environment variable
                       expansion until the matching ENDLOCAL command.
```

## Acceptance Criteria (Definition of Done)

- [ ] `EnvironmentSnapshot` record bevat Variables, DrivePaths, DelayedExpansion, ExtensionsEnabled
- [ ] `BatchContext.SetLocalStack` bestaat
- [ ] `IContext.DelayedExpansion` en `IContext.ExtensionsEnabled` zijn schrijfbaar
- [ ] `IContext.RestoreAllDrivePaths()` is geïmplementeerd
- [ ] `SetlocalCommand` snapt omgeving en past flags toe
- [ ] `EndlocalCommand` herstelt omgeving of doet noop op lege stack
- [ ] Geneste setlocal/endlocal werkt correct
- [ ] `BatchExecutor.UnwindSetLocal()` ruimt resterende stack op bij batch-exit
- [ ] Alle unit tests slagen
- [ ] Bestaande tests blijven slagen
