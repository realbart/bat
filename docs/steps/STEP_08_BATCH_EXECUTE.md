# STEP 08 - Batchbestanden Uitvoeren

**Doel:** Batch files (`.bat`, `.cmd`) uitvoeren vanaf de command prompt en via CALL.

## Context

Na stap 7 kan Bat .NET-executables laden via reflection. Nu wordt de batch execution engine zelf gebouwd. Dit is de kern van het hele systeem — na deze stap kun je `.bat`-bestanden uitvoeren.

Sleutelcommando's die hier geïmplementeerd worden:

| Commando | Beschrijving |
|---|---|
| `BatchExecutor` | De executie-engine (geen commando, maar infrastructuur) |
| `GOTO` | Spring naar een label in het batch-bestand |
| `CALL file.bat` | Roep een ander batch-bestand aan |
| `CALL :label` | Roep een subroutine aan in het huidige bestand |
| `SHIFT` | Schuif batch-parameters één positie op |
| `EXIT /B` | Verlaat het huidige batch-bestand (keert terug naar caller) |

## Architectuur

### BatchContext (uit STEP_01)

```
BatchContext
├── BatchFilePath : string?          ← null in REPL
├── FileContent   : string?          ← volledige bestandsinhoud
├── FilePosition  : int              ← huidige positie (byte offset)
├── LineNumber    : int              ← voor foutmeldingen
├── Parameters    : string?[10]      ← %0..%9
├── ShiftOffset   : int              ← SHIFT-teller
├── SetLocalStack : Stack<Snapshot>  ← SETLOCAL/ENDLOCAL
├── LabelPositions: Dictionary?      ← null in REPL → GOTO is no-op
└── prev          : BatchContext?    ← CALL-ketting (ReactOS: bc->prev)
```

### Uitvoeringsmodel

```
Bat.exe test.bat arg1 arg2
  ↓
BatchExecutor.ExecuteAsync(filePath, ["arg1","arg2"], parentContext)
  ↓
1. Laad bestand: FileSystem.ReadAllText(drive, path)
2. ScanLabels(): bouw LabelPositions dictionary
3. Maak BatchContext:
     Parameters = [filePath, "arg1", "arg2", null, ...]
     prev = parentContext.CurrentBatch
4. ctx.CurrentBatch = nieuwe BatchContext
5. Loop:
   a. ReadNextLine() → ruwe regel
   b. ExpandBatchParameters(line, bc)
   c. ExpandEnvironmentVariables(expanded, ctx)
   d. Parse(expanded) → AST
   e. Dispatcher.ExecuteCommandAsync(ctx, console, ast)
   f. Herhaal tot FilePosition >= FileContent.Length
6. UnwindSetLocal(ctx, bc)   ← resterende setlocals opruimen
7. ctx.CurrentBatch = bc.prev  ← herstel parent
```

### Oproep vanaf Bat.exe

```bash
bat test.bat arg1 arg2     # Roept BatchExecutor aan
bat /C test.bat            # Voer uit en sluit af (stap 12)
```

## ScanLabels

Labels in CMD: een regel die begint met `:labelname` (na whitespace).

```csharp
public static Dictionary<string, int> ScanLabels(string fileContent)
{
    var labels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    int pos = 0;
    while (pos < fileContent.Length)
    {
        int lineStart = pos;
        // Lees tot einde regel
        int lineEnd = fileContent.IndexOfAny(['\r', '\n'], pos);
        if (lineEnd < 0) lineEnd = fileContent.Length;
        
        var line = fileContent[pos..lineEnd].TrimStart();
        if (line.StartsWith(':') && line.Length > 1)
        {
            // Label: pak alles tot de eerste spatie/tab/punt-komma
            var labelEnd = line.IndexOfAny([' ', '\t', ';'], 1);
            var name = labelEnd < 0 ? line[1..] : line[1..labelEnd];
            if (!string.IsNullOrEmpty(name))
                labels.TryAdd(name, lineStart);
        }
        
        pos = lineEnd;
        if (pos < fileContent.Length && fileContent[pos] == '\r') pos++;
        if (pos < fileContent.Length && fileContent[pos] == '\n') pos++;
    }
    return labels;
}
```

**Speciaal label:** `:eof` (end of file) — altijd geldig, ook zonder expliciete declaratie. `GOTO :eof` springt naar het einde.

## GOTO

```
GOTO label

Directs CMD.EXE to a labeled line in a batch program.

  label   Specifies a text string used in the batch program as a label.

A label is identified by a colon preceding it on a separate line.
If command extensions are enabled, GOTO changes as follows:

GOTO :EOF  Transfers control to end of the current batch script file.
```

Gedrag:
- Zoek `label` op in `bc.LabelPositions`
- Zet `bc.FilePosition` op de positie van het label
- Als label niet gevonden: `"The system cannot find the batch label specified - label"`
- In REPL (`bc.LabelPositions == null`): no-op (data-driven design)
- `GOTO :eof` → zet `FilePosition` naar einde bestand

## CALL

```
CALL [drive:][path]filename [batch-parameters]
CALL :label [arguments]

Calls one batch program from another.

  batch-parameters   Specifies any command-line information required by the
                     batch program.
  :label             Specifies that you want to transfer control to the label
                     specified in the current batch program.
```

### CALL file.bat

1. Bouw nieuw `BatchContext` met:
   - `BatchFilePath` = opgelost pad van het batch-bestand
   - `Parameters` = [filePath, arg1, arg2, ...]
   - `prev` = huidige `ctx.CurrentBatch`
2. Voer `BatchExecutor.ExecuteAsync()` recursief uit
3. Herstel `ctx.CurrentBatch = bc.prev`

### CALL :label

1. Zoek label op in **huidige** `bc.LabelPositions`
2. Sla huidige `FilePosition` op als terugkeeradres (via een nieuw `BatchContext` met `prev`)
3. Zet `FilePosition` naar het label
4. Aan het einde (` GOTO :eof` of `EXIT /B`): herstel `FilePosition`

**Maximum nesting:** 16 niveaus (zoals echte CMD). Daarna: `"Batch file nesting too deep"`.

## SHIFT

```
SHIFT [/n]

Changes the position of replaceable parameters in a batch file.

  /n    Starts shifting at the nth argument, where n is between zero
        and eight, and can only decrease the number of arguments.
```

Gedrag:
- `SHIFT` → `bc.ShiftOffset++`
- `SHIFT /2` → schuift alleen parameters 2 en hoger op
- Alle volgende `ExpandBatchParameters()` aanroepen gebruiken `ShiftOffset`

## EXIT /B

```
EXIT [/B] [exitCode]

  /B    Exits the current batch script only, not CMD.EXE.
```

Gedrag in batch:
- `EXIT /B` → beëindig `BatchExecutor`-aanroep (zet `FilePosition` naar einde)
- `EXIT /B 1` → idem + stel `ctx.ErrorCode = 1` in
- `EXIT` (zonder /B) in batch → beëindig ook de outer REPL

## TDD — Stap voor stap

**Bestand:** `Bat.UnitTests/BatchExecuteTests.cs`

### Test 1: ScanLabels vindt labels

```csharp
[Fact]
public void ScanLabels_FindsLabels()
{
    var content = ":start\r\necho hi\r\n:end\r\necho done";
    var labels = BatchExecutor.ScanLabels(content);

    Assert.Equal(2, labels.Count);
    Assert.True(labels.ContainsKey("start"));
    Assert.True(labels.ContainsKey("end"));
}
```

### Test 2: ScanLabels is case-insensitive

```csharp
[Fact]
public void ScanLabels_IsCaseInsensitive()
{
    var content = ":MyLabel\r\necho hi";
    var labels = BatchExecutor.ScanLabels(content);
    Assert.True(labels.ContainsKey("mylabel"));
    Assert.True(labels.ContainsKey("MYLABEL"));
}
```

### Test 3: Eenvoudig batchbestand uitvoeren

```csharp
[Fact]
public async Task Execute_SimpleBatch_RunsLines()
{
    var fs = new TestFileSystem();
    fs.WriteFile('C', ["test.bat"], "echo line1\r\necho line2");
    var ctx = new TestContext(fs);
    var console = new TestConsole();

    await BatchExecutor.ExecuteAsync('C', ["test.bat"], [], ctx, console);

    Assert.Equal(["line1", "line2"], console.OutputLines);
}
```

### Test 4: Batch met parameters

```csharp
[Fact]
public async Task Execute_Batch_ExpandsParameters()
{
    var fs = new TestFileSystem();
    fs.WriteFile('C', ["greet.bat"], "echo Hello %1");
    var ctx = new TestContext(fs);
    var console = new TestConsole();

    await BatchExecutor.ExecuteAsync('C', ["greet.bat"], ["World"], ctx, console);

    Assert.Equal("Hello World", console.OutputLines.Single());
}
```

### Test 5: GOTO springt naar label

```csharp
[Fact]
public async Task Execute_Goto_JumpsToLabel()
{
    var content = "echo before\r\ngoto end\r\necho skipped\r\n:end\r\necho after";
    var fs = new TestFileSystem();
    fs.WriteFile('C', ["test.bat"], content);
    var ctx = new TestContext(fs);
    var console = new TestConsole();

    await BatchExecutor.ExecuteAsync('C', ["test.bat"], [], ctx, console);

    Assert.Equal(["before", "after"], console.OutputLines);
}
```

### Test 6: GOTO :eof beëindigt batch

```csharp
[Fact]
public async Task Execute_GotoEof_EndsBatch()
{
    var content = "echo before\r\ngoto :eof\r\necho skipped";
    // ... setup en assert: alleen "before" in output
}
```

### Test 7: CALL roept ander batchbestand aan

```csharp
[Fact]
public async Task Execute_Call_RunsOtherBatch()
{
    var fs = new TestFileSystem();
    fs.WriteFile('C', ["main.bat"], "call helper.bat\r\necho back");
    fs.WriteFile('C', ["helper.bat"], "echo from helper");
    var ctx = new TestContext(fs);
    var console = new TestConsole();

    await BatchExecutor.ExecuteAsync('C', ["main.bat"], [], ctx, console);

    Assert.Equal(["from helper", "back"], console.OutputLines);
}
```

### Test 8: CALL :label werkt als subroutine

```csharp
[Fact]
public async Task Execute_CallLabel_WorksAsSubroutine()
{
    var content = "call :sub\r\necho main\r\ngoto :eof\r\n:sub\r\necho sub\r\nexit /b";
    // ... assert: ["sub", "main"] in output
}
```

### Test 9: SHIFT verschuift parameters

```csharp
[Fact]
public async Task Execute_Shift_MovesParameters()
{
    var content = "echo %1\r\nshift\r\necho %1";
    var fs = new TestFileSystem();
    fs.WriteFile('C', ["test.bat"], content);
    var ctx = new TestContext(fs);
    var console = new TestConsole();

    await BatchExecutor.ExecuteAsync('C', ["test.bat"], ["first", "second"], ctx, console);

    Assert.Equal(["first", "second"], console.OutputLines);
}
```

### Test 10: EXIT /B verlaat alleen batch

```csharp
[Fact]
public async Task Execute_ExitB_ExitsBatchOnly()
{
    var fs = new TestFileSystem();
    fs.WriteFile('C', ["test.bat"], "echo before\r\nexit /b 5\r\necho after");
    var ctx = new TestContext(fs);
    var console = new TestConsole();

    await BatchExecutor.ExecuteAsync('C', ["test.bat"], [], ctx, console);

    Assert.Equal(["before"], console.OutputLines);
    Assert.Equal(5, ctx.ErrorCode);
    // REPL is nog actief (BatchExecutor is gestopt, niet de hele Bat)
}
```

### Test 11: Geneste CALL-nesting tot max 16

```csharp
[Fact]
public async Task Execute_NestingTooDeep_ReportsError()
{
    // Een batch die zichzelf 17x aanroept
    // Assert: foutmelding "Batch file nesting too deep"
}
```

## Implementatie Volgorde

1. Schrijf alle tests (rood)
2. Implementeer `ScanLabels` + tests groen
3. Implementeer lineaire `BatchExecutor` (zonder GOTO) + tests groen
4. Implementeer `GotoCommand` + tests groen
5. Implementeer `CallCommand` (extern bestand) + tests groen
6. Implementeer `CallCommand` (`:label` subroutine) + tests groen
7. Implementeer `ShiftCommand` + tests groen
8. Implementeer `Exit /B` gedrag
9. Registreer in dispatcher
10. Run alle tests → alles groen

## Acceptance Criteria (Definition of Done)

- [ ] `bat test.bat arg1 arg2` voert het batchbestand uit
- [ ] `%1` en `%2` worden correct geëxpandeerd uit batch-parameters
- [ ] `GOTO :eof` beëindigt het bestand
- [ ] `GOTO label` springt correct, onbekend label geeft foutmelding
- [ ] `CALL helper.bat` roept helper aan en keert terug
- [ ] `CALL :sub` werkt als subroutine met `EXIT /B`
- [ ] `SHIFT` verschuift `%1` naar de volgende parameter
- [ ] `EXIT /B 5` stopt batch en zet errorlevel op 5
- [ ] `EXIT /B` in REPL werkt als `EXIT`
- [ ] Maximum nesting 16 levels is afgedwongen
- [ ] SetLocalStack wordt automatisch afgewikkeld bij batch-exit
- [ ] Alle bestaande tests slagen nog steeds
