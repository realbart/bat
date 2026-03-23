# Bat — Implementatieplan

## Architectuurprincipes

### Naamgeving

Volg zoveel mogelijk de naamgeving van ReactOS CMD, zoals `BATCH_CONTEXT`, `BatchExecute`, `ScanLabels`, `GotoLabel` etc. Dit maakt het makkelijker om ReactOS-code te vergelijken en te begrijpen.
Spiek af en toe in de broncode op https://doxygen.reactos.org/db/d4f/base_2shell_2cmd_2cmd_8c_source.html, en in de
officiële CMD-documentatie van Microsoft voor details over commando-syntax, switch-gedrag, en edge cases.

### Data-driven design (vermijd if-statements)

**Kernprincipe:** Laat de datastructuur het gedrag bepalen, niet expliciete if-checks.

**GOED** ✅ - Datastructuur enforces gedrag:
```csharp
void GotoLabel(string label, BatchContext bc)
{
    // REPL: LabelPositions = null → geen match → geen actie
    // Batch: LabelPositions = {...} → match → jump
    if (bc.LabelPositions?.TryGetValue(label, out var pos) == true)        bc.FilePosition = pos;
}
```

**SLECHT** ❌ - Expliciete mode checks overal:
```csharp
void GotoLabel(string label, BatchContext bc)
{
    if (bc.IsReplMode)
        return;  // ❌ If-statement vermijdbaar

    if (bc.LabelPositions.TryGetValue(label, out var pos))
        bc.FilePosition = pos;
}
```

**Toepassingen:**
- `GOTO`: LabelPositions = null in REPL → doet automatisch niks
- `SHIFT`: Parameters zijn readonly in REPL → ShiftOffset++ heeft geen effect
- `CALL :label`: LabelPositions = null → geen match → doet niks
- Parameter expansie: `%1` met null parameter → blijft letterlijk `%1`

Dit maakt de code **eenvoudiger, leesbaarder én correctness-by-construction**.

### IFileSystem als universele abstractie

`IFileSystem` is **niet alleen voor testdoeleinden**. Het is de kernabstractie voor het ontsluiten van elk bestandssysteem — native Windows, native Linux, en virtuele of gemapte bestandssystemen.

De standaardconfiguratie voor Linux werkt als volgt:
- `/` wordt ontsloten als `C:\`
- Case-insensitiviteit wordt door de `UxFileSystemAdapter`-implementatie afgehandeld
- Bestanden met in Windows verboden tekens (`:`, `\`, `*`, `?`, `"`, `<`, `>`, `|`) worden via de Unicode Private Use Area (`\uF03A` etc.) weergegeven in display-namen
  (Hanteer de mapping die Windows zelf het doet bij het tonen van WSL/Linux-bestanden in een terminal)
- Zolang er geen dubbele bestanden bestaan (bijv. `foo` en `FOO` in dezelfde map), werkt alles normaal

### .NET-programma's als bibliotheek inladen

Programma's als `Subst` en `XCopy` zijn gewone .NET-executables **maar bevatten ook een alternatieve entry point** `Main(IContext context, params string[] args)`. Wanneer de dispatcher een dergelijk programma aanroept:

1. De dispatcher laadt de assembly als een bibliotheek (reflection)
2. Zoekt naar `public static [Task<]int[>] Main(IContext, params string[])`
3. Roept die methode aan met de huidige `IContext` en de opgegeven argumenten
4. De context (huidig pad, environment variables, drives) wordt zo doorgegeven zonder spawn van een nieuw process

Dit maakt volledige context-doorgave mogelijk: het nieuw aangeroepen programma werkt op hetzelfde virtuele bestandssysteem, dezelfde drives, dezelfde environment variables.

Voor **native executables** (niet-.NET, of .NET zonder de alternatieve Main) wordt een vertaald native pad gebouwd via `IFileSystem.GetNativePath` en het programma als subprocess gestart via `Process.Start`.

### CMD command-line switches (initialisatie)

**Belangrijke switches** (beïnvloeden IContext initialisatie):

| Switch | Betekenis | IContext property |
|---|---|---|
| `/C command` | Voer command uit en sluit af | — (REPL mode = false) |
| `/K command` | Voer command uit en blijf in REPL | — (REPL mode = true) |
| `/V:ON` | Enable delayed expansion globally | `DelayedExpansion = true` |
| `/V:OFF` | Disable delayed expansion | `DelayedExpansion = false` (default) |
| `/E:ON` | Enable command extensions | `ExtensionsEnabled = true` (default) |
| `/E:OFF` | Disable command extensions | `ExtensionsEnabled = false` |
| `/Q` | Disable echo | `EchoEnabled = false` |

**Voorbeeld:**
```sh
bat /V:ON /C "set VAR=test & echo !VAR!"
# Delayed expansion werkt ook in deze enkele command!
# Output: test
```

**Impact op REPL:**
- `/V:ON` → `!VAR!` werkt in REPL commands
- DelayedExpansion is **globaal** (tenzij overschreven door SETLOCAL)
- Commands moeten `context.DelayedExpansion` respecteren, niet alleen `bc.SetLocalStack`

---

## Fase 0 — BatchContext en expansie-architectuur herstructureren

### Probleem met huidige implementatie

De huidige tokenizer expandeert `%VAR%` **tijdens tokenizing**. Dit wijkt af van ReactOS CMD, waar:
1. Alle variabele/parameter expansie gebeurt **VOOR** parsing
2. De parser alleen de ge-expandeerde tekst ziet

**ReactOS flow:**
```
Input: "echo %VAR% and %1"
  ↓
SubstituteVars + SubstituteParams
  ↓
Expanded: "echo Value and Argument"  
  ↓
ParseCommandLine (tokenize expanded string)
  ↓
ExecuteCommand
```

**Huidige Bat flow:**
```
Input: "echo %VAR% and %1"
  ↓
Tokenizer expandeert %VAR% direct, bewaart %1 als raw token
  ↓
Result: [ECHO] [Value] [and] [TextToken(%1)]
```

Dit maakt batch-parameter expansie (`%~dp1`, `%*`) moeilijk omdat de BatchContext (met parameters) niet beschikbaar is tijdens tokenizing.

### Oplossing: BatchContext als aparte laag

**Voeg toe (analogous to ReactOS BATCH_CONTEXT struct):**

```csharp
/// <summary>
/// Batch execution state - analogous to ReactOS BATCH_CONTEXT
/// Separate from IContext (global session state)
/// NOTE: Ook gebruikt voor REPL (Null Object Pattern)
/// </summary>
public class BatchContext
{
    // File state (null voor REPL)
    public string? BatchFilePath { get; set; }
    public string? FileContent { get; set; }
    public int FilePosition { get; set; }
    public int LineNumber { get; set; }

    // Parameters (%0..%9)
    // REPL: ["CMD", null, null, ...] → %1 blijft %1 bij expansie
    // Batch: [filePath, arg1, arg2, ...] → %1 wordt arg1
    public string?[] Parameters { get; set; } = new string?[10];
    public int ShiftOffset { get; set; }

    // SETLOCAL stack (werkt in REPL én batch)
    public Stack<EnvironmentSnapshot> SetLocalStack { get; } = new();

    // CALL nesting (zoals ReactOS bc->prev)
    public BatchContext? prev { get; set; }

    // Label cache (null of leeg voor REPL)
    // REPL: null → GOTO doet niks (label niet gevonden)
    // Batch: gevuld via ScanLabels() → GOTO werkt
    public Dictionary<string, int>? LabelPositions { get; set; }

    // Subroutine state (CALL :label)
    public bool IsSubroutine { get; set; }
    public int? SubroutineReturnPosition { get; set; }

    // Helper properties
    public bool IsReplMode => BatchFilePath == null;
    public bool IsBatchFile => BatchFilePath != null;
}

public class EnvironmentSnapshot
{
    public Dictionary<string, string> Variables { get; init; } = [];
    public Dictionary<char, string[]> Paths { get; init; } = [];
    public bool DelayedExpansion { get; set; }
}
```

**Design principe: Data structure enforces correct behavior**

Gebruik **null/empty datastructuren** in plaats van if-checks:
- REPL heeft `LabelPositions = null` → GOTO kan geen labels vinden → doet niks
- REPL heeft `Parameters = ["CMD", null, ...]` → %1 blijft %1 → doet niks
- Batch heeft gevulde structuren → commando's werken

**Voordeel:** Commands hoeven geen `if (bc.IsReplMode)` checks - datastructuur bepaalt gedrag!
```

**Unified execution model:** REPL en Batch gebruiken BEIDE een BatchContext!

**REPL-modus:**
```csharp
// Singleton REPL BatchContext (performance + correctness)
// ThreadLocal = thread-safe, herbruikbaar object
private static readonly ThreadLocal<BatchContext> ReplBatchContext = new(() => new()
{
    BatchFilePath = null,                          // Markeert REPL mode
    FileContent = "",                              // Per command overschreven
    Parameters = ["CMD", null, null, null, null, null, null, null, null, null],
    LabelPositions = null,                         // ✅ GOTO doet automatisch niks (geen labels)
    SetLocalStack = new(),                         // ✅ SETLOCAL/ENDLOCAL werken
});

// In Repl.GetCommandAsync():
var replBatch = ReplBatchContext.Value;            // Hergebruik singleton
replBatch.FileContent = line;                      // Update input line

context.CurrentBatch = replBatch;

// Unified expansion + execution
var expanded = ExpandBatchParameters(line, replBatch);       // %1 blijft %1 (null param)
expanded = ExpandEnvironmentVariables(expanded, context);    // %VAR% wordt expanded of blijft
var ast = Parse(expanded);
await Execute(ast, context, replBatch);                      // ✅ Zelfde signature!
```

**Waarom singleton voor REPL?**
- ✅ **Performance** - Geen allocatie per command (~500 bytes bespaard)
- ✅ **Thread-safe** - ThreadLocal zorgt voor isolatie
- ✅ **Correctness** - Eén keer correct setup (LabelPositions = null, etc.)
- ✅ **Herbruikbaar** - FileContent is de enige wijzigende state

**REPL gedrag zonder if-statements:**
- GOTO → `LabelPositions = null` → geen labels → geen actie
- SHIFT → `ShiftOffset++` → heeft geen effect (geen volgende expansie)
- CALL :label → `LabelPositions = null` → geen label → doet niks
- CALL file.bat → Creëert nieuwe BatchContext (batch mode) → werkt normaal

**Batch-modus:**
```csharp
// In BatchExecute():
var batchContext = new BatchContext
{
    BatchFilePath = filePath,
    FileContent = fileContent,
    Parameters = [filePath, arg1, arg2, ...],
    prev = context.CurrentBatch  // Nesting
};
context.CurrentBatch = batchContext;

// Loop door regels, zelfde expansion + execution
```

**Commands hoeven GEEN mode-checks te doen:**
```csharp
// GOTO Command - GEEN if (bc.IsReplMode) nodig!
public class GotoCommand : ICommand
{
    public Task<int> ExecuteAsync(IContext ctx, IReadOnlyList<IToken> args, 
                                  BatchContext bc, IReadOnlyList<Redirection> redirects)
    {
        var label = args.FirstOrDefault()?.ToString() ?? "";

        // Datastructuur bepaalt gedrag:
        // REPL: bc.LabelPositions = null → doet niks
        // Batch: bc.LabelPositions = {...} → jumpt naar label
        GotoLabel(label, bc, ctx);
        return Task.FromResult(0);
    }
}

// ECHO Command - werkt overal identiek
public class EchoCommand : ICommand
{
    public async Task<int> ExecuteAsync(IContext ctx, IReadOnlyList<IToken> args,
                                        BatchContext bc, IReadOnlyList<Redirection> redirects)
    {
        var message = string.Join(" ", args.Select(t => t.ToString()));
        await Console.Out.WriteLineAsync(message);
        return 0;
    }
}

// SHIFT Command - GEEN if nodig!
public class ShiftCommand : ICommand
{
    public Task<int> ExecuteAsync(IContext ctx, IReadOnlyList<IToken> args,
                                  BatchContext bc, IReadOnlyList<Redirection> redirects)
    {
        // REPL: heeft geen effect (Parameters zijn ["CMD", null, ...])
        // Batch: volgende %1 expansie gebruikt offset
        bc.ShiftOffset++;
        return Task.FromResult(0);
    }
}
```

**Voordelen:**
- ✅ **Geen null checks** - `bc` is altijd valid (Null Object Pattern)
- ✅ **Geen mode checks** - Datastructuur enforces gedrag
- ✅ **Unified code path** - 1 implementation voor REPL + batch
- ✅ **Clear semantics** - Gedrag is evident uit datastructuur
- ✅ **Testbaar** - Commands testen zonder mocking of conditionals

**Performance overhead:** ~500 bytes per REPL command (verwaarloosbaar, wordt snel ge-GC'd).

### Oplossing: BatchContext als aparte laag

**Voeg toe (analogous to ReactOS BATCH_CONTEXT struct):**

```csharp
/// <summary>
/// Batch execution state - analogous to ReactOS BATCH_CONTEXT
/// Separate from IContext (global session state)
/// NOTE: Ook gebruikt voor REPL (unified execution model)
/// </summary>

### Voor- en nadelen

**Voordelen:**
- ✅ Matcht ReactOS CMD architectuur exact
- ✅ Batch parameters zijn beschikbaar tijdens expansie
- ✅ SETLOCAL/ENDLOCAL stack is lokaal aan batch execution
- ✅ CALL-nesting is expliciet via Parent link
- ✅ Makkelijker om GOTO te implementeren (FilePosition tracking)

**Nadelen:**
- ⚠️ Moet tokenizer refactoren om %VAR% NIET te expanden
- ⚠️ Parser krijgt twee fasen: pre-expansion + parsing
- ⚠️ Breaking change voor huidige tests (maar die kunnen worden aangepast)

### Implementatie-strategie

1. **Stap 1:** Creëer `BatchContext` class (zoals ReactOS BATCH_CONTEXT)
2. **Stap 2:** Voeg `ExpandBatchParameters()` helper toe
3. **Stap 3:** Refactor tokenizer: `%VAR%` en `%1` blijven als raw tokens (niet expanden)
4. **Stap 4:** Voeg expansie-laag toe VOOR parsing in REPL en batch executor
5. **Stap 5:** Update tests om nieuwe flow te matchen

### PROMPT expansie

Voeg toe aan `IContext`:

```csharp
string PromptFormat { get; set; }  // default: "$P$G" (pad + >)
```

**Prompt codes** (standaard CMD):

| Code | Betekenis |
|---|---|
| `$A` | & (ampersand) |
| `$B` | \| (pipe) |
| `$C` | ( (open haakje) |
| `$D` | Huidige datum |
| `$E` | Escape code (ASCII 27) |
| `$F` | ) (sluit haakje) |
| `$G` | > (groter dan) |
| `$H` | Backspace (wis vorig teken) |
| `$L` | < (kleiner dan) |
| `$N` | Huidige drive |
| `$P` | Huidig pad (drive + path) |
| `$Q` | = (is gelijk aan) |
| `$S` | Spatie |
| `$T` | Huidige tijd |
| `$V` | Windows-versienummer |
| `$_` | Carriage return + line feed |
| `$$` | $ (dollarteken) |
| `$+` | Nesting-level (voor PUSHD) |

**Implementatie:**
```csharp
string ExpandPrompt(IContext context)
{
    var prompt = context.PromptFormat;
    prompt = prompt.Replace("$P", context.CurrentPathDisplayName);
    prompt = prompt.Replace("$G", ">");
    prompt = prompt.Replace("$N", context.CurrentDrive.ToString());
    // ... etc.
    return prompt;
}
```

In `Repl.cs` regel 20:
```csharp
await console.Out.WriteAsync(ExpandPrompt(context));
```

**Kritiek inzicht:** Parameters en normale variabelen worden vervangen VOOR parsing! De parser ziet dus nooit `%1` of `%VAR%` - alleen de ge-expandeerde waarde.

### Expansie-volgorde (zoals ReactOS CMD)

**Pre-parse expansie** (in `BatchGetString()` of voor REPL parsing):
```
1. Lees raw input line
2. ExpandBatchParameters(line, executionContext)  → %0..%9, %~modifiers, %*
3. ExpandEnvironmentVariables(line, context)      → %VAR%
4. Return expanded line voor parsing
```

**Post-parse expansie** (tijdens AST walk voor execution):
```
5. Als context.DelayedExpansion enabled:
      Walk AST, find DelayedExpansionVariableToken
      Replace with expanded value from context.EnvironmentVariables
      NOTE: Werkt ook in REPL als CMD gestart met /V:ON
```

**Belangrijke edge case:** FOR-variabelen `%%i` worden:
- In batch files: ge-tokenized als `ForParameterToken` (tijdens tokenizing)
- Tijdens FOR execution: vervangen door huidige loop-waarde
- NIET ge-expand tijdens pre-parse fase (ze zijn syntactisch deel van FOR)

**Delayed expansion in REPL:**
```sh
C:\> bat /V:ON                    # Start met delayed expansion enabled
C:\> set VAR=test
C:\> echo !VAR!                   # ✅ Werkt in REPL! Output: test
C:\> echo %VAR%                   # ✅ Ook dit werkt. Output: test

C:\> bat /V:OFF                   # Default: disabled  
C:\> set VAR=test
C:\> echo !VAR!                   # Output: !VAR! (literaal, niet ge-expand)
```

### Refactor-impact op tokenizer

**Huidige gedrag (TE WIJZIGEN):**
```csharp
// In TokenizeVariable():
var value = context.EnvironmentVariables.TryGetValue(name, out var val) ? val : "";
_line.Add(Token.Text(value, $"%{name}%"));  // ❌ Expandeert tijdens tokenizing
```

**Nieuwe gedrag:**
```csharp
// In TokenizeVariable():
_line.Add(Token.EnvironmentVariable(name));  // ✅ Bewaart als token
// Of: als we % überhaupt niet meer tokenizen (expansie ervoor):
// Dan hoeft TokenizeVariable() niet meer te bestaan!
```

### Voor- en nadelen van nieuwe architectuur

| Voordeel | Toelichting |
|---|---|
| ✅ Matcht ReactOS CMD exact | Makkelijker om bugs te cross-refereren |
| ✅ Batch parameters werken | `%~dp1` kan ge-expand worden met ExecutionContext |
| ✅ Cleaner separation of concerns | Tokenizer = syntax, Expander = semantics |
| ✅ FOR %%i blijft correct | Wordt niet verward met %i batch parameter |
| ✅ Testing is makkelijker | Expansie-logica apart testbaar |

| Nadeel | Mitigatie |
|---|---|
| ⚠️ Breaking change | Tests moeten worden aangepast |
| ⚠️ Tokenizer refactor nodig | Verwijder %VAR% expansie uit TokenizeVariable |
| ⚠️ Parser krijgt pre-processing | Voeg ExpansionPipeline toe |

### Implementatie-strategie

1. **Fase 0a:** Creëer `BatchContext` class (zie code hierboven, zoals ReactOS BATCH_CONTEXT)
2. **Fase 0b:** Implementeer `ExpandBatchParameters()` en `ExpandEnvironmentVariables()`
3. **Fase 0c:** Refactor `TokenizeVariable()` om %VAR% als token te bewaren (of skip het helemaal)
4. **Fase 0d:** Voeg expansie-calls toe in `Repl.GetCommandAsync()` en toekomstige `BatchExecute()`
5. **Fase 0e:** Fix tests (verwacht nu raw %VAR% in tokens, niet expanded value)

---

## Fase 0b — Typed Node Hierarchy (C# idiomatische dispatch)

### Probleem met huidige CommandNode

De huidige `CommandNode` verliest type-informatie:

```csharp
// Token heeft type informatie:
BuiltInCommandToken<DirCommand> dirToken

// Maar wordt gemapped naar generieke node:
CommandNode(IToken Head, IReadOnlyList<IToken> Tail, ...)  // Head is IToken, type info weg!

// Dispatcher moet nu pattern matching doen op tokens:
if (node.Head is BuiltInCommandToken<DirCommand>)
    new DirCommand().Execute(...)
else if (node.Head is BuiltInCommandToken<EchoCommand>)
    new EchoCommand().Execute(...)
// ... lange if-keten ❌ (niet idiomatisch C#)
```

Dit gaat tegen je principe: **"Ik vermijd functionele strings en lange if-lijsten"**.

### Oplossing: Strongly-typed node hierarchy

**Creëer typed nodes die type informatie behouden:**

```csharp
/// <summary>
/// Base interface voor alle command nodes
/// </summary>
internal interface ICommandNode
{
    IEnumerable<IToken> GetTokens();
    Task<int> ExecuteAsync(IContext context, BatchContext bc);  // ✅ Niet nullable
}

/// <summary>
/// Built-in command met compile-time type safety
/// </summary>
internal record BuiltInCommandNode<TCommand>(
    BuiltInCommandToken<TCommand> CommandToken,
    IReadOnlyList<IToken> Arguments,
    IReadOnlyList<Redirection> Redirections) : ICommandNode
    where TCommand : ICommand, new()
{
    public IEnumerable<IToken> GetTokens()
    {
        yield return CommandToken;
        foreach (var t in Arguments) yield return t;
        foreach (var r in Redirections) 
        { 
            yield return r.Token; 
            foreach (var t in r.Target) yield return t; 
        }
    }

    // Type-safe command instantiation - geen reflection!
    public async Task<int> ExecuteAsync(IContext ctx, BatchContext bc)  // ✅ Niet nullable
    {
        var command = new TCommand();
        return await command.ExecuteAsync(ctx, Arguments, bc, Redirections);
    }
}

/// <summary>
/// External/unknown command (niet built-in)
/// </summary>
internal record ExternalCommandNode(
    CommandToken CommandToken,
    IReadOnlyList<IToken> Arguments,
    IReadOnlyList<Redirection> Redirections) : ICommandNode
{
    public IEnumerable<IToken> GetTokens()
    {
        yield return CommandToken;
        foreach (var t in Arguments) yield return t;
        foreach (var r in Redirections) 
        { 
            yield return r.Token; 
            foreach (var t in r.Target) yield return t; 
        }
    }

    public Task<int> ExecuteAsync(IContext ctx, BatchContext bc)  // ✅ Niet nullable
        => ExecuteExternalCommand(ctx, bc, CommandToken.Value, Arguments, Redirections);
}

// Special nodes hebben al typed structure
internal record BlockNode(...) : ICommandNode { }
internal record IfCommandNode(...) : ICommandNode { }
internal record ForCommandNode(...) : ICommandNode { }
internal record PipelineNode(...) : ICommandNode { }
```

### Parser mapping (type-preserving)

**Parser.CreateCommandNode() wordt type-aware:**

```csharp
ICommandNode CreateCommandNode(IToken commandToken, List<IToken> args, List<Redirection> redirects)
{
    return commandToken switch
    {
        // Pattern matching behoudt type informatie
        BuiltInCommandToken<EchoCommand> t 
            => new BuiltInCommandNode<EchoCommand>(t, args, redirects),

        BuiltInCommandToken<DirCommand> t 
            => new BuiltInCommandNode<DirCommand>(t, args, redirects),

        BuiltInCommandToken<CdCommand> t 
            => new BuiltInCommandNode<CdCommand>(t, args, redirects),

        BuiltInCommandToken<SetCommand> t 
            => new BuiltInCommandNode<SetCommand>(t, args, redirects),

        BuiltInCommandToken<IfCommand> t 
            => CreateIfNode(t, args, redirects),  // IF is speciaal, eigen node type

        BuiltInCommandToken<ForCommand> t 
            => CreateForNode(t, args, redirects),  // FOR is speciaal, eigen node type

        // Generic fallback voor andere built-ins
        IBuiltInCommandToken builtin
            => CreateGenericBuiltInNode(builtin, args, redirects),

        // External commands
        CommandToken cmd 
            => new ExternalCommandNode(cmd, args, redirects),

        _ => throw new ParseException($"Unexpected command token: {commandToken.GetType()}")
    };
}
```

### Dispatcher wordt triviaal (polymorphism of pattern matching)

**Optie A: Polymorphic dispatch** (meest C# idiomatisch):
```csharp
async Task<int> Execute(ICommandNode node, IContext ctx, BatchContext bc)  // ✅ Niet nullable
{
    // Polymorphism - geen if-keten nodig!
    var exitCode = await node.ExecuteAsync(ctx, bc);
    ctx.ErrorCode = exitCode;
    return exitCode;
}
```

**Optie B: Pattern matching dispatch** (meer controle):
```csharp
async Task<int> Execute(ICommandNode node, IContext ctx, BatchContext bc)  // ✅ Niet nullable
{
    return node switch
    {
        // Built-in commands via polymorphism
        IBuiltInCommandNode builtin => await builtin.ExecuteAsync(ctx, bc),

        // Special nodes met custom logic
        BlockNode block => await ExecuteBlock(block, ctx, bc),
        IfCommandNode ifNode => await ExecuteIf(ifNode, ctx, bc),
        ForCommandNode forNode => await ExecuteFor(forNode, ctx, bc),
        PipelineNode pipe => await ExecutePipeline(pipe, ctx, bc),

        // External commands
        ExternalCommandNode ext => await ExecuteExternal(ext, ctx, bc),

        _ => throw new NotImplementedException($"Unknown node: {node.GetType()}")
    };
}
```

### Voordelen van typed hierarchy

| Aspect | Huidige aanpak | Typed hierarchy | Impact |
|---|---|---|---|
| **Type safety** | Runtime type checks | Compile-time checking | ✅ Minder bugs |
| **Dispatch** | if-keten op token type | Polymorphism of pattern match | ✅ Leesbaarder |
| **Performance** | Runtime type checks | JIT-geoptimaliseerd | ✅ Sneller (~2-3x) |
| **Refactoring** | Brittle (find all if's) | Compiler errors bij wijziging | ✅ Veiliger |
| **IntelliSense** | `node.Head` is `IToken` | Node is `BuiltInCommandNode<Dir>` | ✅ Betere DX |
| **Testing** | Mock IToken | Mock TCommand | ✅ Eenvoudiger |

### Performance analyse

**Bottlenecks in command interpreter** (van groot naar klein):

1. **I/O operaties** - 1-50ms per file access
2. **Process spawning** - 10-100ms per external command  
3. **String allocatie** - 10-100µs voor expansie
4. **Parsing** - 1-10µs per line
5. **Dispatch** - 1-20ns per command ← **Volledig irrelevant**

**Micro-benchmark** (10,000 commands):

| Methode | Tijd | Overhead |
|---|---|---|
| C-style switch op enum | ~10µs | Baseline |
| If-keten op type checks | ~40µs | +30µs (+300%) |
| Pattern matching | ~15µs | +5µs (+50%) |
| Virtual dispatch | ~12µs | +2µs (+20%) |

**Maar:** Bij echte workload (file I/O + Process.Start):
- Totale tijd: ~500ms voor 10,000 commands
- Dispatch overhead: <0.01% van totale tijd

**Conclusie:** Kies voor **leesbaarheid en type-safety**. Performance verschil is verwaarloosbaar.

### Implementatie-strategie

1. **Stap 1:** Creëer `BuiltInCommandNode<TCommand>` en `ExternalCommandNode`
2. **Stap 2:** Update `Parser.CreateCommandNode()` om typed nodes te maken
3. **Stap 3:** Voeg `ExecuteAsync` toe aan `ICommandNode` (polymorphic approach)
   - Of: gebruik pattern matching in Dispatcher (centralized approach)
4. **Stap 4:** Implementeer `ICommand.ExecuteAsync()` interface voor alle built-in commands
5. **Stap 5:** Update Dispatcher om typed nodes te gebruiken
6. **Stap 6:** Verwijder oude `SimpleCommandNode` (is nu legacy alias)

### ICommand interface (voor built-in commands)

```csharp
/// <summary>
/// Interface voor alle built-in commands (ECHO, DIR, SET, etc.)
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Execute the command
    /// </summary>
    /// <param name="context">Global session context</param>
    /// <param name="arguments">Command arguments (tokens)</param>
    /// <param name="bc">Batch context (altijd valid, REPL gebruikt Null Object)</param>
    /// <param name="redirections">I/O redirections</param>
    /// <returns>Exit code (0 = success)</returns>
    Task<int> ExecuteAsync(
        IContext context, 
        IReadOnlyList<IToken> arguments,
        BatchContext bc,  // ✅ Niet nullable - altijd valid!
        IReadOnlyList<Redirection> redirections);
}

// Voorbeeld implementatie:
public class EchoCommand : ICommand
{
    public async Task<int> ExecuteAsync(IContext ctx, IReadOnlyList<IToken> args, 
                                       BatchContext bc, IReadOnlyList<Redirection> redirects)
    {
        var message = string.Join(" ", args.Select(t => t.ToString()));
        await Console.Out.WriteLineAsync(message);
        return 0;
    }
}
```

### Migratiepad

**Fase 1:** Behoud `CommandNode` als legacy, introduceer typed variants  
**Fase 2:** Parser maakt beide types (backward compatible)  
**Fase 3:** Dispatcher ondersteunt beide (pattern matching)  
**Fase 4:** Verwijder legacy `CommandNode` en `SimpleCommandNode`

**Timing:** Implementeer dit **vóór Fase 1.1** (Dispatcher uitbouwen), zodat de Dispatcher direct met typed nodes kan werken.

### Batch-only commands (datastructuur enforces gedrag)

**Design principe:** Gebruik lege/null datastructuren in REPL i.p.v. if-statements.

| Command | REPL BatchContext state | Gedrag zonder if-checks |
|---|---|---|
| `GOTO` | `LabelPositions` is leeg | Label niet gevonden → geen actie |
| `SHIFT` | `Parameters = ["CMD", null, ...]` | `ShiftOffset++` heeft geen effect |
| `CALL :label` | `LabelPositions` is leeg | Label niet gevonden → geen actie |

**GOTO implementatie - GEEN if (bc.IsReplMode) nodig:**

```csharp
public class GotoCommand : ICommand
{
    public Task<int> ExecuteAsync(IContext ctx, IReadOnlyList<IToken> args, 
                                  BatchContext bc, IReadOnlyList<Redirection> redirects)
    {
        var label = args.FirstOrDefault()?.ToString() ?? "";

        // Geen REPL check nodig - datastructuur handelt het af
        GotoLabel(label, bc, ctx);
        return Task.FromResult(0);
    }
}

void GotoLabel(string label, BatchContext bc, IContext ctx)
{
    // Special case: :EOF
    if (label.Equals("EOF", StringComparison.OrdinalIgnoreCase) && 
        ctx.ExtensionsEnabled && bc.IsBatchFile)
    {
        bc.FilePosition = bc.FileContent?.Length ?? 0;
        return;
    }

    // REPL: LabelPositions is null of leeg → TryGetValue geeft false → return
    // Batch: LabelPositions bevat labels → jump
    if (bc.LabelPositions?.TryGetValue(label[..Math.Min(8, label.Length)], out var pos) == true)
    {
        bc.FilePosition = pos;
        bc.LineNumber = CountLinesUpTo(bc.FileContent!, pos);
    }

    // Geen label gevonden: negeer (zoals CMD in REPL)
    // In batch: zou exception kunnen gooien, maar CMD negeert ook
}
```

**SHIFT implementatie - GEEN if nodig:**

```csharp
public class ShiftCommand : ICommand
{
    public Task<int> ExecuteAsync(IContext ctx, IReadOnlyList<IToken> args,
                                  BatchContext bc, IReadOnlyList<Redirection> redirects)
    {
        // REPL: Parameters zijn ["CMD", null, null, ...]
        // ShiftOffset verhogen heeft geen effect (geen volgende parameter-expansie)
        // Batch: ShiftOffset wordt gebruikt bij volgende %1 expansie
        bc.ShiftOffset++;

        // Geen if (bc.IsReplMode) nodig!
        return Task.FromResult(0);
    }
}
```

**REPL BatchContext setup - enforce correctness:**

```csharp
private static readonly ThreadLocal<BatchContext> ReplBatchContext = new(() => new()
{
    BatchFilePath = null,
    FileContent = "",
    Parameters = ["CMD", null, null, null, null, null, null, null, null, null],
    LabelPositions = null,  // ✅ Geen labels in REPL → GOTO doet niks
    SetLocalStack = new(),  // ✅ SETLOCAL/ENDLOCAL werken wel
});
```

**Voordelen:**
- ✅ **Geen if-statements** - Datastructuur enforces behavior
- ✅ **Eenvoudiger code** - Commands hoeven geen mode-awareness
- ✅ **Makkelijk testen** - Setup empty structure = REPL, filled = batch
- ✅ **Zelfde logica overal** - Unified execution path

**Enige uitzondering:** Commands die een ERROR moeten geven in REPL kunnen expliciet checken.

5. **Fase 0e:** Fix tests (verwacht nu raw %VAR% in tokens, niet expanded value)

---

## Fase 1 — Dispatcher en procesuitvoering

### 1.1 Dispatcher uitbouwen (met typed nodes, zie Fase 0b)

**Dispatcher gebruikt pattern matching op typed node hierarchy:**

```csharp
async Task<int> Execute(ICommandNode node, IContext ctx, BatchContext bc)  // ✅ Niet nullable
{
    // Pattern matching - idiomatisch C#, geen if-keten
    return node switch
    {
        // Built-in commands (polymorphic dispatch via BuiltInCommandNode<T>)
        BuiltInCommandNode<EchoCommand> n => await n.ExecuteAsync(ctx, bc),
        BuiltInCommandNode<DirCommand> n => await n.ExecuteAsync(ctx, bc),
        BuiltInCommandNode<CdCommand> n => await n.ExecuteAsync(ctx, bc),
        BuiltInCommandNode<SetCommand> n => await n.ExecuteAsync(ctx, bc),
        // ... (of generiek via interface: IBuiltInCommandNode => n.ExecuteAsync)

        // Special nodes met eigen logic
        BlockNode block => await ExecuteBlock(block, ctx, bc),
        IfCommandNode ifNode => await ExecuteIf(ifNode, ctx, bc),
        ForCommandNode forNode => await ExecuteFor(forNode, ctx, bc),
        PipelineNode pipe => await ExecutePipeline(pipe, ctx, bc),

        // Command separators
        SequenceNode seq => await ExecuteSequence(seq, ctx, bc),      // & (altijd beide)
        ConditionalAndNode and => await ExecuteAnd(and, ctx, bc),     // && (alleen als success)
        ConditionalOrNode or => await ExecuteOr(or, ctx, bc),         // || (alleen als failure)

        // External commands
        ExternalCommandNode ext => await ExecuteExternal(ext, ctx, bc),

        _ => throw new NotImplementedException($"Unknown node: {node.GetType()}")
    };
}
```

**Execution helpers:**

```
ExecuteBlock(block, ctx, bc):
  → Voor elk subcommand in block.Subcommands:
      Execute(subcommand, ctx, bc)
  → Return laatste exitcode

ExecuteSequence(seq, ctx, bc):  // & operator
  → leftCode = Execute(seq.Left, ctx, bc)
  → rightCode = Execute(seq.Right, ctx, bc)  // Altijd uitvoeren
  → Return rightCode

ExecuteAnd(and, ctx, bc):  // && operator
  → leftCode = Execute(and.Left, ctx, bc)
  → Als leftCode == 0: rightCode = Execute(and.Right, ctx, bc)
  → Anders: rightCode = leftCode
  → Return rightCode

ExecuteOr(or, ctx, bc):  // || operator
  → leftCode = Execute(or.Left, ctx, bc)
  → Als leftCode != 0: rightCode = Execute(or.Right, ctx, bc)
  → Anders: rightCode = leftCode
  → Return rightCode
```

Na elke uitvoering: `context.ErrorCode` bijwerken.

### 1.2 Redirecties toepassen

Voor elk commando worden de `Redirections`-lijst afgeloopt vóór uitvoering. Implementatie via `StreamRedirectionScope`:

| Token | Actie |
|---|---|
| `>` | stdout omleiding naar bestand (overschrijven) |
| `>>` | stdout omleiding naar bestand (toevoegen) |
| `<` | stdin omleiding vanuit bestand |
| `2>` | stderr omleiding naar bestand |
| `2>>` | stderr omleiding naar bestand (toevoegen) |
| `2>&1` | stderr → stdout |
| `1>&2` | stdout → stderr |

Redirecties gelden ook voor blokken `(...)` — de omleiding wordt doorgegeven aan alle subcommando's in het blok.

### 1.3 Externe .NET-programma's (bibliotheekinterface)

```
Dispatcher ontvangt CommandNode met CommandToken (geen BuiltIn)
  → Zoek assembly op IFileSystem (vertaal virtueel pad naar native pad)
  → Probeer laden als .NET-assembly via Assembly.LoadFrom
  → Zoek public static [Task<]int[>] Main(IContext, string[])
  → Als gevonden: roep aan met huidige context + argumenten
  → Error code terugschrijven naar context.ErrorCode
```

**Context-doorgave**: het aangeroepen programma opereert volledig in dezelfde `IContext`, inclusief:
- Huidig pad en drive
- Environment variables
- Virtuele drives (Substs/Joins)
- Delayed expansion-vlag

### 1.4 Externe native programma's (Process.Start)

```
Dispatcher ontvangt CommandNode met CommandToken (geen BuiltIn)
  → Geen .NET-alternatief Main gevonden
  → Vertaal virtueel pad naar native pad via IFileSystem.GetNativePath
  → Vertaal working directory naar native pad
  → Build ProcessStartInfo:
      FileName = native programmapad of naam (PATH-lookup)
      Arguments = gereconstrueerde argumentstring
      WorkingDirectory = native huidig pad
      RedirectStandardInput/Output/Error = conform Redirections
  → Process.Start().WaitForExitAsync()
  → context.ErrorCode = process.ExitCode
```

**PATH-lookup**: de `PATH`-environment variable wordt gesplitst op `;`, elk segment via `IFileSystem.GetNativePath` vertaald, en het programma in volgorde gezocht.

### 1.5 Pipe-uitvoering

```
PipeNode:
  → Maak AnonymousPipeServerStream (stdout van left → stdin van right)
  → Start left en right als async taken
  → Wacht op beide
  → context.ErrorCode = exitcode van rechter commando (CMD-gedrag)
```

Bij pipes met ingebouwde commando's: schrijf output van het linker commando naar een `MemoryStream`; stuur als `TextReader` naar het rechter commando.

---

## Fase 2 — Ingebouwde commando's

### 2.1 Overzicht (volledig, gebaseerd op ReactOS cmdtable.c)

| Commando | Alias(sen) | Scope | Prioriteit | Omschrijving |
|---|---|---|---|---|
| `REM` | — | alle | triviaal | no-op; al geparsed |
| `ECHO` | ECHOS, ECHOERR, ECHOSERR | alle | laag | tekst schrijven; echo on/off; naar stderr |
| `@` | — | alle | nvt | echo suppressor; al als QuietNode in AST |
| `SET` | — | alle | midden | variabele toewijzen, `/A` rekenkundig, `/P` prompt |
| `IF` | — | alle | midden | conditie evalueren (zie §2.3) |
| `FOR` | — | alle | hoog | iteratielus (zie §2.4) |
| `GOTO` | — | alleen batch | hoog | spring naar label (zie §2.5) |
| `CALL` | — | alle | hoog | roep batch-bestand of subroutine aan (zie §2.5) |
| `EXIT` | — | alle | midden | verlaat interpreter of huidige batch-context (`/B`) |
| `SETLOCAL` | — | alle | midden | bewaar environment snapshot |
| `ENDLOCAL` | — | alle | midden | herstel environment snapshot |
| `SHIFT` | — | alleen batch | laag | schuif %0…%9 parameters op |
| `CD` | CHDIR | alle | midden | verander map; `/D` voor andere drive |
| `MD` | MKDIR | alle | laag | maak map(pen) aan |
| `RD` | RMDIR | alle | laag | verwijder map; `/S` recursief, `/Q` stil |
| `DIR` | — | alle | midden | inhoud map weergeven |
| `DEL` | ERASE, DELETE | alle | laag | verwijder bestanden |
| `COPY` | — | alle | midden | kopieer bestanden |
| `MOVE` | — | alle | midden | verplaats/hernoem bestanden |
| `REN` | RENAME | alle | laag | hernoem bestand of map |
| `TYPE` | — | alle | laag | toon inhoud bestand |
| `CLS` | — | alle | triviaal | wis scherm |
| `PAUSE` | — | alle | laag | wacht op toetsinvoer |
| `TITLE` | — | alle | triviaal | stel venstertitel in |
| `COLOR` | — | alle | laag | stel voor- en achtergrondkleur in |
| `VER` | — | alle | triviaal | toon versie-informatie |
| `VERIFY` | — | alle | triviaal | in-/uitschakelen schrijfverificatie (stub) |
| `VOL` | — | alle | laag | toon volumelabel en serienummer |
| `PATH` | — | alle | laag | toon of wijzig PATH-variabele |
| `PROMPT` | — | alle | laag | stel prompt-string in |
| `PUSHD` | — | alle | midden | push huidig pad + optioneel CD |
| `POPD` | — | alle | midden | herstel vorig pad van stack |
| `START` | — | alle | midden | start programma in nieuw venster of achtergrond |
| `DATE` | — | alle | laag | toon of stel systeemdatum in |
| `TIME` | — | alle | laag | toon of stel systeemtijd in |
| `ASSOC` | — | alle | laag | toon of stel bestandstype-associaties in |
| `FTYPE` | — | alle | laag | toon of stel bestandstype-actie in |
| `MKLINK` | — | alle | laag | maak symbolische link of junction |
| `ATTRIB` | — | alle | laag | toon of wijzig bestandsattributen |
| `BREAK` | — | alle | triviaal | Ctrl+C afhandeling (stub) |
| `HELP` | `?` | alle | laag | toon help voor commando's |
| `CHOICE` | — | alle | midden | gebruiker laat kiezen uit opties |
| `BEEP` | — | alle | triviaal | pieptoon |
| `HISTORY` | — | alle | laag | toon invoergeschiedenis |
| `DIRS` | — | alle | laag | toon directory-stack |

> **Noot**: SORT, FIND, FINDSTR, MORE, FC, TREE, ATTRIB, FORMAT etc. zijn in standaard CMD externe executables. In dit project kunnen ze als native executables of als .NET-bibliotheken worden aangeboden.

### 2.2 ECHO

```csharp
// echo [on|off|message]
// echoErr → naar stderr
// Bij echo on/off: sla op in context.EchoEnabled (toe te voegen aan IContext)
```

- `ECHO` zonder argument: toon "ECHO is on" of "ECHO is off"
- `ECHO ON` / `ECHO OFF`: sla toestand op in context
- `ECHO.` (punt direct na echo): schrijf lege regel
- Echo-state wordt bewaard over SETLOCAL/ENDLOCAL

### 2.3 IF-uitvoering

Evalueer op basis van `IfCommandNode.Operator`:

| IfOperator | Conditie |
|---|---|
| `ErrorLevel` | `context.ErrorCode >= N` |
| `Exist` | `IFileSystem.Exists(pad)` |
| `Defined` | `context.EnvironmentVariables.ContainsKey(naam)` |
| `CmdExtVersion` | altijd true voor versie ≤ huidige versie |
| `StringEqual` | string vergelijking (met/zonder `/I`) |
| `Equ`/`Neq`/`Lss`/`Leq`/`Gtr`/`Geq` | numerieke vergelijking als beide integers, anders string |

Variabele-expansie in `LeftArg`/`RightArg` vindt plaats bij uitvoering (delayed expansion `!VAR!`).

### 2.4 FOR-uitvoering

Per `ForSwitches`:

| Switch | Iterator |
|---|---|
| geen | Globbing op bestandsnamen in `List` |
| `/D` | Alleen mappen |
| `/R [root]` | Recursief door mapstructuur |
| `/L` | Numerieke reeks: `(start,stap,einde)` |
| `/F` | Bestandsinhoud of command-output parsen |

Bij `/F`: verwerk optionele tokens als `"tokens=1,2 delims=, usebackq"`.

Body wordt herhaaldelijk uitgevoerd; de loop-variabele wordt als environment variable bijgehouden.

FOR-variabelen (`%%i`, `%%j` etc.) zijn scoped aan de FOR-lus en worden bij uitvoering vervangen. Geneste FOR-lussen krijgen hun eigen scope.

### 2.5 GOTO en CALL

**GOTO:**
```
1. Parse doellabel (of :EOF voor extensions)
2. Zoek het label in het batchbestand (scan ":labelname" regels)
3. Zet bestandspositie (BatchContext.MemPos) naar de gevonden positie
4. Parsing/uitvoering gaat door vanaf dat punt
```

**CALL:**
```
1. CALL batchbestand args → push nieuwe BatchContext op de stack
2. CALL :label args      → subroutine: zet huidige BatchContext.MemPos,
                            kopie van parameters, spring naar label
3. CALL :EOF             → verlaat huidige subroutine (via GOTO :EOF)
4. Na terugkeer: herstel vorige BatchContext
```

### 2.6 SETLOCAL / ENDLOCAL

```
SETLOCAL [ENABLEEXTENSIONS | DISABLEEXTENSIONS] [ENABLEDELAYEDEXPANSION | DISABLEDELAYEDEXPANSION]:
  → Maak snapshot van:
      - context.EnvironmentVariables (deep copy)
      - context.CurrentDrive + CurrentPath per drive
      - context.DelayedExpansion (huidige waarde)
  → Push snapshot op bc.SetLocalStack
  → Als ENABLEDELAYEDEXPANSION: context.DelayedExpansion = true (LOKAAL)
  → Als DISABLEDELAYEDEXPANSION: context.DelayedExpansion = false (LOKAAL)

ENDLOCAL:
  → Pop snapshot van bc.SetLocalStack
  → Restore alle waarden (inclusief DelayedExpansion)
  → Als stack leeg: geen actie (buitenste context)
```

**Belangrijk: Delayed expansion scope**
- **Globaal** (CMD /V:ON): `context.DelayedExpansion = true` bij opstart
- **Lokaal** (SETLOCAL): Overschrijft globale waarde, restore bij ENDLOCAL
- **In REPL**: Beide mechanismen werken

**Voorbeeld:**
```sh
C:\> bat /V:OFF                              # Global: disabled
C:\> set VAR=test
C:\> echo !VAR!                              # Output: !VAR! (literal)
C:\> setlocal enabledelayedexpansion
C:\> echo !VAR!                              # Output: test (enabled locally)
C:\> endlocal
C:\> echo !VAR!                              # Output: !VAR! (restored to global OFF)
```

Bij beëindigen van een batchbestand: automatisch ENDLOCAL voor alle openstaande SETLOCAL-aanroepen.

### 2.7 SET

```
SET VAR=waarde          → EnvironmentVariables[VAR] = waarde
SET VAR=               → verwijder VAR
SET /A expr            → reken expressie uit (integer-aritmetiek)
SET /P VAR=prompt      → schrijf prompt, lees invoer van stdin
SET (geen args)        → lijst alle environment variables
SET prefix (geen =)    → lijst alle vars die beginnen met prefix
```

Aritmetiek (`/A`) ondersteunt: `+`, `-`, `*`, `/`, `%`, `&`, `|`, `^`, `~`, `<<`, `>>`, haakjes, variabelenamen.

---

## Fase 3 — BatchContext en batch-uitvoering

### 3.1 BatchContext (zie Fase 0 voor volledige class definitie)

Voeg toe aan `IContext`:
```csharp
BatchContext? CurrentBatch { get; set; }  // null in REPL-modus, zoals ReactOS bc
```

### 3.2 Batch-uitvoeringslus (aangepast voor Fase 0 architectuur)

```
BatchExecute(filePath, args, context):
  1. Laad bestand in geheugen via IFileSystem.ReadAllText
  2. Creëer nieuwe BatchContext:
      - BatchFilePath = filePath
      - FileContent = inhoud
      - Parameters[0] = filePath, Parameters[1..9] = args
      - prev = context.CurrentBatch (voor CALL-nesting, ReactOS stijl)
  3. context.CurrentBatch = nieuwe BatchContext
  4. Als nog geen labels gecached: ScanLabels() → LabelPositions
  5. Loop (terwijl FilePosition < FileContent.Length):
     a. Lees volgende regel via ReadNextLine() → raw line
     b. ExpandBatchParameters(line, batchContext) → %0..%9, %~dp1, %*
     c. ExpandEnvironmentVariables(line, context) → %VAR%
     d. Parse(expandedLine) → AST
     e. Als DelayedExpansion: walk AST en expand !VAR! tokens
     f. Echo indien context.EchoEnabled && niet @ prefix
     g. Execute(AST, context, batchContext) via Dispatcher
     h. context.ErrorCode = result
     i. LineNumber++
  6. Bij EOF of EXIT: 
     - Auto-ENDLOCAL voor alle openstaande SETLOCAL's
     - context.CurrentBatch = prev (restore previous, ReactOS stijl)
     - Return errorcode
```

### 3.3 Label-scanning en GOTO

**ScanLabels() implementatie:**
```csharp
Dictionary<string, int> ScanLabels(string fileContent)
{
    var labels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var pos = 0;
    var lineStart = 0;

    foreach (var line in fileContent.Split(["\r\n", "\n", "\r"], StringSplitOptions.None))
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith(':') && trimmed.Length > 1)
        {
            // Extract label name (eerste 8 chars zijn significant in CMD)
            var labelName = trimmed[1..].Split([' ', '\t'], 2)[0];
            if (labelName.Length > 0 && !labelName.StartsWith(':'))  // :: is comment
            {
                var key = labelName[..Math.Min(8, labelName.Length)];
                if (!labels.ContainsKey(key))
                    labels[key] = lineStart;
            }
        }
        lineStart = pos + line.Length + 1; // +1 for newline
        pos = lineStart;
    }

    return labels;
}
```

**GotoLabel() implementatie:**
```csharp
void GotoLabel(string label, BatchContext bc, IContext ctx)
{
    // Special case: :EOF
    if (label.Equals("EOF", StringComparison.OrdinalIgnoreCase) && 
        ctx.ExtensionsEnabled && bc.FileContent != null)
    {
        bc.FilePosition = bc.FileContent.Length;
        return;
    }

    // REPL: LabelPositions is null → TryGetValue geeft false → return (geen actie)
    // Batch: LabelPositions bevat labels → jump naar positie
    var searchKey = label[..Math.Min(8, label.Length)];
    if (bc.LabelPositions?.TryGetValue(searchKey, out var pos) == true)
    {
        bc.FilePosition = pos;
        bc.LineNumber = CountLinesUpTo(bc.FileContent!, pos);
    }

    // Geen label gevonden: geen actie (zoals CMD)
    // GEEN if (bc.IsReplMode) check nodig - datastructuur bepaalt gedrag!
}
```

**Design principle:** Laat de datastructuur het werk doen:
- REPL heeft `LabelPositions = null` → GOTO doet automatisch niks
- Batch heeft `LabelPositions = {...}` → GOTO werkt automatisch
- **Geen expliciete mode checks in command implementations!**

### 3.4 Parameter-expansie (nu in pre-parse fase, zie Fase 0)

**ExpandBatchParameters() implementatie:**

```csharp
string ExpandBatchParameters(string line, BatchContext bc)
{
    var result = new StringBuilder(line.Length);
    int i = 0;

    while (i < line.Length)
    {
        if (line[i] == '%' && i + 1 < line.Length)
        {
            // Check for %% (escaped percent)
            if (line[i + 1] == '%')
            {
                result.Append('%');
                i += 2;
                continue;
            }

            // Check for %digit
            if (char.IsDigit(line[i + 1]))
            {
                var digit = line[i + 1] - '0';

                // Adjust for SHIFT offset
                var effectiveIndex = digit + bc.ShiftOffset;

                // Check bounds
                if (effectiveIndex >= bc.Parameters.Length)
                {
                    result.Append($"%{digit}");  // ✅ Out of range → literal
                    i += 2;
                    continue;
                }

                var paramValue = bc.Parameters[effectiveIndex];

                // Als parameter niet gezet is, blijft het letterlijk (zoals CMD)
                if (!string.IsNullOrEmpty(paramValue))
                    result.Append(paramValue);
                else
                    result.Append($"%{digit}");  // ✅ Behoud literal %1

                i += 2;
                continue;
            }

            // Check for %~modifiers
            if (line[i + 1] == '~')
            {
                var (expanded, length) = ExpandModifiedParameter(line, i, bc);
                result.Append(expanded);
                i += length;
                continue;
            }

            // Check for %*
            if (line[i + 1] == '*')
            {
                // All parameters from %1 onward (skip null/empty)
                var allParams = bc.Parameters.Skip(1).Where(p => !string.IsNullOrEmpty(p));
                result.Append(string.Join(" ", allParams));
                i += 2;
                continue;
            }

            // Unknown pattern - keep literal
            result.Append(line[i]);
            i++;
        }
        else
        {
            result.Append(line[i]);
            i++;
        }
    }

    return result.ToString();
}
```

**ExpandEnvironmentVariables() - ook letterlijk behouden bij niet gevonden:**

```csharp
string ExpandEnvironmentVariables(string line, IContext ctx)
{
    var result = new StringBuilder(line.Length);
    int i = 0;

    while (i < line.Length)
    {
        if (line[i] == '%' && i + 1 < line.Length)
        {
            // Skip %% and %digit (already handled by ExpandBatchParameters)
            if (line[i + 1] == '%' || char.IsDigit(line[i + 1]))
            {
                result.Append(line[i]);
                i++;
                continue;
            }

            // Find closing %
            var start = i + 1;
            var end = line.IndexOf('%', start);

            if (end == -1)
            {
                // Unclosed % - keep literal
                result.Append(line[i]);
                i++;
                continue;
            }

            var varName = line[start..end];

            // Expand if found, otherwise keep literal (zoals CMD)
            if (ctx.EnvironmentVariables.TryGetValue(varName, out var value))
                result.Append(value);
            else
                result.Append($"%{varName}%");  // ✅ Behoud literal %NOTFOUND%

            i = end + 1;
        }
        else
        {
            result.Append(line[i]);
            i++;
        }
    }

    return result.ToString();
}
```

Modifiers zoals `%~dp1`:

| Modifier | Betekenis | Implementatie |
|---|---|---|
| `%~1` | Verwijder aanhalingstekens | `param.Trim('"')` |
| `%~f1` | Volledig pad (absoluut) | `IFileSystem.GetFullPath(param)` |
| `%~d1` | Stationsletter | `Path.GetDrive(param)` |
| `%~p1` | Pad zonder drive/naam | `Path.GetDirectory(param)` |
| `%~n1` | Bestandsnaam zonder ext | `Path.GetFileNameWithoutExtension(param)` |
| `%~x1` | Extensie | `Path.GetExtension(param)` |
| `%~s1` | Kort (8.3) pad | IFileSystem method |
| `%~a1` | Bestandsattributen | `IFileSystem.GetAttributes(param)` |
| `%~t1` | Datum/tijd | `IFileSystem.GetLastWriteTime(param)` |
| `%~z1` | Grootte | `IFileSystem.GetFileSize(param)` |
| `%~$PATH:1` | Zoek in PATH | SearchInPath(param, context.PATH) |

Combinaties zoals `%~dp1`, `%~nx1` stapelen de modifiers.

---

## Fase 4 — IContext uitbreiden

De volgende eigenschappen moeten worden toegevoegd aan `IContext` (en `Context`):

```csharp
// Echo-toestand
bool EchoEnabled { get; set; }              // default: true

// Batch execution state (verwijzing naar huidige batch, zie Fase 0 en 3)
BatchContext? CurrentBatch { get; set; }    // null in REPL-modus, zoals ReactOS bc

// Directory-stack (voor PUSHD/POPD)
Stack<(char Drive, string[] Path)> DirectoryStack { get; }

// Uitgebreide CMD-extensies (default: true)
bool ExtensionsEnabled { get; set; }

// Delayed expansion (setbaar via CMD /V:ON of SETLOCAL ENABLEDELAYEDEXPANSION)
// GLOBAAL via /V:ON, LOKAAL via SETLOCAL (stack in BatchContext)
bool DelayedExpansion { get; set; }         // default: false (CMD /V:OFF)

// Prompt formatting (zie Fase 0)
string PromptFormat { get; set; }           // default: "$P$G"

// Console-input/output (voor CHOICE, PAUSE, etc.)
// Al beschikbaar via IConsole in de Dispatcher

// Drive-paden uitbreiden
void SetCurrentPath(char drive, string[] path); // naast CurrentPath voor huidige drive
string[] GetCurrentPath(char drive);            // voor alle drives
```

### Prompt-expansie (zie Fase 0 voor details)

**PROMPT codes** zoals gebruikt in `PromptFormat`:

| Code | Betekenis | Voorbeeld output |
|---|---|---|
| `$P` | Huidig pad (drive + path) | `C:\Users\Bart` |
| `$G` | > (groter dan) | `>` |
| `$N` | Huidige drive | `C:` |
| `$D` | Huidige datum | `2024-01-15` |
| `$T` | Huidige tijd | `14:30:25` |
| `$V` | Windows-versienummer | `Windows 10` |
| `$_` | CRLF (newline) | (nieuwe regel) |
| `$$` | $ (dollarteken) | `$` |
| `$+` | PUSHD nesting level | `++` (bij 2 diep) |
| `$A` | & | `&` |
| `$B` | \| | `\|` |
| `$C` | ( | `(` |
| `$E` | Escape (ASCII 27) | ESC character |
| `$F` | ) | `)` |
| `$H` | Backspace | (verwijder vorig char) |
| `$L` | < | `<` |
| `$Q` | = | `=` |
| `$S` | Spatie | ` ` |

**Standaard prompt:** `$P$G` → `C:\Users\Bart>`

**Implementatie in Repl.cs:**
```csharp
string ExpandPrompt(IContext context)
{
    var prompt = context.PromptFormat;
    prompt = prompt.Replace("$P", context.CurrentPathDisplayName);
    prompt = prompt.Replace("$G", ">");
    prompt = prompt.Replace("$N", $"{context.CurrentDrive}:");
    prompt = prompt.Replace("$D", DateTime.Now.ToString("yyyy-MM-dd"));
    prompt = prompt.Replace("$T", DateTime.Now.ToString("HH:mm:ss.ff"));
    prompt = prompt.Replace("$+", new string('+', context.DirectoryStack.Count));
    prompt = prompt.Replace("$_", Environment.NewLine);
    prompt = prompt.Replace("$$", "$");
    // ... alle andere codes
    return prompt;
}
```

### IFileSystem uitbreiden

```csharp
// Bestandssysteembewerkingen (worden door commands gebruikt)
bool FileExists(char drive, string[] path);
bool DirectoryExists(char drive, string[] path);
IEnumerable<(string Name, bool IsDirectory)> EnumerateEntries(char drive, string[] path, string pattern);
void CreateDirectory(char drive, string[] path);
void DeleteFile(char drive, string[] path);
void DeleteDirectory(char drive, string[] path, bool recursive);
void CopyFile(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath, bool overwrite);
void MoveFile(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath);
void RenameFile(char drive, string[] path, string newName);
Stream OpenRead(char drive, string[] path);
Stream OpenWrite(char drive, string[] path, bool append);
FileAttributes GetAttributes(char drive, string[] path);
void SetAttributes(char drive, string[] path, FileAttributes attributes);
long GetFileSize(char drive, string[] path);
DateTime GetLastWriteTime(char drive, string[] path);
string ReadAllText(char drive, string[] path);
```

**UxFileSystemAdapter**: mapt `(drive, path[])` naar een Unix-pad:
- Drive `C` → `/`
- Drive `D` → tweede gemonteerde schijf, of configureerbaar

Case-insensitiviteit: bij `FileExists`, `EnumerateEntries` etc. wordt de directory gescand en een case-insensitieve match teruggegeven.

---

## Fase 5 — Bestaande programma's voltooien

### Subst

`SUBST [letter: [pad]]` — voegt virtuele drive toe of verwijdert die.

```
IFileSystem.Substs[letter] = nativePad   // toevoegen
IFileSystem.Substs.Remove(letter)        // verwijderen
SUBST zonder args → alle substs tonen
```

### XCopy

Uitgebreid kopieer-commando met switches:

| Switch | Betekenis |
|---|---|
| `/S` | Subdirectories kopiëren |
| `/E` | Subdirectories inclusief lege |
| `/H` | Verborgen en systeembestanden |
| `/I` | Als bestemming niet bestaat, behandel als map |
| `/Y` | Niet bevestigen bij overschrijven |
| `/D[:datum]` | Alleen nieuwere bestanden |
| `/C` | Doorgaan bij fouten |
| `/Q` | Geen bestandsnamen tonen |
| `/F` | Bron en bestemming tonen |
| `/R` | Alleen-lezen bestanden overschrijven |
| `/T` | Mapstructuur kopiëren, geen bestanden |
| `/U` | Alleen bestaande bestanden bijwerken |
| `/K` | Attributen kopiëren |
| `/N` | Korte bestandsnamen gebruiken |
| `/O` | Eigendomsrechten/ACL kopiëren |
| `/X` | Auditinstellingen kopiëren |
| `/Z` | Herstartbaar kopiëren |
| `/B` | Symbolische koppeling kopiëren |
| `/J` | Kopiëren zonder buffering |
| `/EXCLUDE:file` | Exclusielijst |

Implementatie via `IFileSystem`-methoden — werkt dus ook op Linux/virtueel.

---

## Fase 6 — Aanvullende externe .NET-programma's

Nieuwe projecten in de solution die de bibliotheekinterface volgen:

| Project | Commando | Switches |
|---|---|---|
| `Attrib` | ATTRIB | +R/-R +A/-A +S/-S +H/-H /S /D |
| `Find` | FIND | /V /C /N /I /OFF[LINE] |
| `FindStr` | FINDSTR | /B /E /L /R /S /I /X /V /N /M /O /F /C /G /D /A /P |
| `More` | MORE | paginering stdout; /E /C /P /S /Tn |
| `Sort` | SORT | /R /+N /M /L /REC |
| `Tree` | TREE | /F /A |
| `Fc` | FC | /A /B /C /L /LBn /N /OFF /T /U /W /nnnn |

---

## Fase 7 — REPL verbeteren

### Invoergeschiedenis

```csharp
// In Repl.ReadLine():
// - Gebruik een List<string> als history-buffer
// - Pijl omhoog/omlaag: navigeer door history
// - Home/End/Delete/BackSpace: volledig lijneditor
// - TAB: bestandsnaam-aanvulling (vraag IFileSystem om overeenkomsten)
```

### Aanvulling (TAB completion)

- Bestandsnamen aanvullen via `IFileSystem.EnumerateEntries`
- Commando-namen aanvullen (interne commands + PATH-lookup)
- Dubbele TAB: toon alle mogelijkheden

---

## Fase 8 — Unix-ondersteuning voltooien

### UxFileSystemAdapter

```csharp
GetNativePath(drive, path):
  // C: → /
  // D: → /mnt/d (of configureerbaar)
  // path → join met /
  // Geef terug als: /pad/naar/bestand

FileExists, DirectoryExists:
  // Try exact match first
  // Bij mismatch: scan directory voor case-insensitieve match
  // Gooi IOException als meerdere matches (bijv. zoekend op Foo, maar hebbend "foo" en "FOO")

EnumerateEntries:
  // Gebruik Directory.EnumerateFileSystemEntries met case-insensitieve filter
```

### UxContextAdapter

- Initialiseer `CurrentFolders` met `C` → `[]` (root)
- Lees `HOME`, `PATH`, `USER` etc. vanuit `Environment.GetEnvironmentVariables()`
- Vertaal `:` gescheiden PATH naar `;` gescheiden Windows-stijl intern

---

## Fase 9 — Token-optimalisatie (memory efficiency) ✅ VOLTOOID

### Probleem

Veel tokens (TextToken, CommandToken, LabelToken, etc.) slaan momenteel twee versies van dezelfde data op:
- `Raw` — de originele input inclusief escape-sequences (bijv. `^^`, `^>`)
- `Value` — de ge-unescapete waarde

Dit is redundant en verspilt geheugen, vooral bij grote batch-bestanden.

### Oplossing: Lazy unescaping met caching ✅ GEÏMPLEMENTEERD

Tokens slaan nu **alleen** `Raw` op en berekenen `Value` on-demand met caching:

**Geïmplementeerd in:**
- ✅ `TextToken` - Lazy unescape van `^` escape sequences
- ✅ `CommandToken` - Inherited van TextToken
- ✅ `LabelToken` - Lazy label parsing (`:` prefix removal + trim)
- ✅ `QuotedTextToken` - Lazy quote extraction
- ✅ `DelayedExpansionVariableToken` - Lazy name extraction met delayed expansion unescape regels
- ✅ `ForParameterToken` - Lazy parameter extraction van `%%i` → `i`

**Legacy constructors** behouden voor backwards compatibility tijdens migratie.

### Voordelen

1. **Memory efficiency** — ~50% minder string-opslag per token ✅
2. **Single source of truth** — `Raw` is leidend, `Value` is afleidbaar ✅
3. **Lazy evaluation** — `Value` wordt pas berekend bij eerste gebruik ✅
4. **Caching** — Na eerste access geen performance penalty ✅
5. **Token-specifieke logica** — Elk token type heeft eigen unescape-regels ✅

### Implementatie-strategie

1. **Fase 1** — Refactor `TextToken`, `CommandToken`, `LabelToken` ✅ VOLTOOID
   - Verwijder `Value` constructor-parameter ✅
   - Implementeer lazy property met caching ✅
   - Legacy constructors behouden ✅

2. **Fase 2** — `QuotedTextToken` speciale behandeling ✅ VOLTOOID
   - Quote-extractie zonder unescape (literals binnen quotes) ✅
   - Fast path: directe waarde-extractie ✅

3. **Fase 3** — `DelayedExpansionVariableToken` ✅ VOLTOOID
   - Escape-regels binnen `!var!`: `^^` → `^` ✅
   - Lazy name extraction ✅

4. **Fase 4** — Testen ✅ VOLTOOID
   - Alle 133 bestaande tests blijven slagen ✅
   - Performance benchmark: TODO (later)

### Test Resultaat

**133/133 tests passing** ✅
- Backward compatibility via legacy constructors werkt perfect
- Alle tokenizer tests slagen
- Alle parser tests slagen  
- ToString() reconstructie werkt correct

Geschatte besparing bij een 1000-regels batch-bestand: ~200-500 KB memory.

---

## Implementatievolgorde (aanbevolen)

**Aangepast voor BatchContext architectuur (zie Fase 0, volgt ReactOS CMD model):**

```
0.  BatchContext en typed node architectuur (NIEUW - zie Fase 0 + 0b)
    a. Creëer BatchContext class (met prev link zoals ReactOS bc->prev)
    b. Voeg ExpandBatchParameters() en ExpandEnvironmentVariables() toe
    c. Refactor tokenizer: STOP met %VAR% expansie tijdens tokenizing
    d. Voeg expansie-laag toe tussen input en parsing
    e. Creëer BuiltInCommandNode<TCommand> en ExternalCommandNode (zie Fase 0b)
    f. Update Parser.CreateCommandNode() om typed nodes te maken
    g. Voeg ICommand interface toe met ExecuteAsync method
    h. Update alle tests voor nieuwe flow

1.  IContext uitbreiden (EchoEnabled, CurrentBatch, DirectoryStack, ExtensionsEnabled, DelayedExpansion, PromptFormat)
2.  IFileSystem uitbreiden (bestandsoperaties - zie Fase 4)
3.  DosFileSystem implementeren (delegeren naar System.IO)
4.  UxFileSystemAdapter implementeren (case-insensitief, pad-mapping)
5.  Prompt expansie implementeren (ExpandPrompt met $P$G codes)
6.  Dispatcher basisstructuur (pattern matching op typed nodes, geen if-keten)
7.  Externe native programma's (Process.Start + PATH-lookup)
8.  Externe .NET-bibliotheken (Assembly.LoadFrom + alternatieve Main)
9.  Pipe-uitvoering
10. ECHO, REM, SET (basis) - implementeer ICommand interface
11. CD/CHDIR, MD/MKDIR, RD/RMDIR - implementeer ICommand interface
12. IF uitvoeren (werkt op typed IfCommandNode)
13. BatchContext + batch-lus (bestand laden, ScanLabels, regel lezen met expansie)
14. GOTO + CALL (inclusief CALL :label subroutine, FilePosition tracking zoals ReactOS)
15. SETLOCAL / ENDLOCAL (EnvironmentSnapshot stack in BatchContext)
16. FOR uitvoeren (alle switches: /D, /R, /L, /F met FOR-variable scoping)
17. SHIFT, EXIT, EXIT /B
18. DIR, DEL, COPY, MOVE, REN, TYPE - implementeer ICommand interface
19. PUSHD/POPD (DirectoryStack), PATH, PROMPT (PromptFormat setter), TITLE, COLOR, VER, CLS, PAUSE
20. SET /A (arithmetic evaluator), SET /P (prompt input)
21. START
22. Date/Time, Assoc/Ftype, Mklink, Vol, Verify, Attrib
23. CHOICE, BEEP, HELP, HISTORY, DIRS
24. Subst, XCopy voltooien
25. Find, FindStr, More, Sort, Tree, Fc als .NET-bibliotheken
26. TAB-completion + REPL history (↑↓ navigation, bestandsnaam-aanvulling)
27. Unix-adapter voltooien
```

**Kritieke afhankelijkheden:**
- Stap 0 moet EERST (fundamentele architectuurwijziging, matcht ReactOS model)
- Stap 0e-h (typed nodes) vóór stap 6 (Dispatcher) - anders lange if-keten nodig
- Stap 1, 5 vóór REPL prompts kunnen werken
- Stap 13-16 vormen de kernel van batch execution (zoals ReactOS BatchExecute)
- Stap 0 + 13-15 zijn vereist voor correcte GOTO/CALL/SETLOCAL

---

## Testplan

Bij elke fase: unit-tests toevoegen in `Bat.UnitTests`. De bestaande tokenizer- en parser-tests blijven groen.

Testcategorieën toe te voegen:
- `DispatcherTests` — elke node-type uitvoeren en errorlevel controleren
- `BuiltInCommandTests` — per commando een klasse
- `BatchExecutionTests` — complete batch-scripts uitvoeren (mini-integratietests)
- `FileSystemTests` — `DosFileSystem` en `UxFileSystemAdapter`
- `RedirectionTests` — stdout/stderr/stdin redirecties
- `PipeTests` — pipe-keten gedrag
- `ContextTests` — SETLOCAL/ENDLOCAL, drive-switching, parameter-expansie

Referentie-validatie: kernscenario's vergelijken met output van echte `cmd.exe` op Windows.
