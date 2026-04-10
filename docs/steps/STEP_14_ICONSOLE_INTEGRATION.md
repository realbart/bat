# Stap 14.1: IConsole Integratie in IContext

## Doel

IConsole wordt een resource in IContext (zoals IFileSystem), en elke command execution krijgt een geïsoleerde context via `StartNew()`. Console wordt verwijderd uit BatchContext.

## Achtergrond

**Huidige problemen:**
- BatchContext heeft Console, maar Context niet → satellietapplicaties kunnen niet bij console
- Context wordt gedeeld tussen executions → niet thread-safe bij redirections
- Geen automatische line-ending conversie (`\r\n` → native) voor console output
- BatchContext naam suggereert "batch-only", maar wordt ook voor REPL gebruikt

**Conceptueel model:**
- Elke command execution krijgt een **geïsoleerde context** (deep copy)
- Bij redirections wordt alleen Console overschreven
- Later: optimalisatie via delta-dictionaries (copy-on-write)

## Scope

### 1. IContext uitbreiden

```csharp
public interface IContext
{
    IConsole Console { get; }

    /// <summary>
    /// Creates a new execution context for a command.
    /// Performs deep copy of state with optional console override.
    /// </summary>
    IContext StartNew(IConsole? console = null);

    // ... bestaande members
}
```

### 2. LineEndingConvertingWriter implementeren

Wrapper die automatisch `\r\n` converteert naar `Environment.NewLine`:

```csharp
namespace Bat.Console;

internal class LineEndingConvertingWriter(TextWriter inner) : TextWriter
{
    public override Encoding Encoding => inner.Encoding;

    public override void Write(char value) => inner.Write(value);

    public override void Write(string? value)
    {
        if (value != null)
            inner.Write(value.Replace("\r\n", Environment.NewLine));
    }

    public override Task WriteAsync(string? value)
    {
        if (value != null)
            return inner.WriteAsync(value.Replace("\r\n", Environment.NewLine));
        return Task.CompletedTask;
    }

    // Override alle Write* variants voor completeness
}
```

### 3. Console class aanpassen

```csharp
internal class Console : IConsole
{
    public TextWriter Out { get; } = new LineEndingConvertingWriter(System.Console.Out);
    public TextWriter Error { get; } = new LineEndingConvertingWriter(System.Console.Error);
    public TextReader In => System.Console.In;
    // ...
}
```

### 4. Context.StartNew() implementeren

```csharp
internal class Context : IContext
{
    private IConsole _console;
    public IConsole Console => _console;

    public IContext StartNew(IConsole? console = null)
    {
        // Deep copy (later: optimize met delta-dictionaries)
        var newContext = new DosContext(FileSystem)  // of UxContextAdapter
        {
            Console = console ?? _console,
            CurrentDrive = this.CurrentDrive,
            ErrorCode = this.ErrorCode,
            EchoEnabled = this.EchoEnabled,
            DelayedExpansion = this.DelayedExpansion,
            ExtensionsEnabled = this.ExtensionsEnabled,
            PromptFormat = this.PromptFormat,
            HistorySize = this.HistorySize
        };

        // Deep copy dictionaries
        foreach (var kv in EnvironmentVariables)
            newContext.EnvironmentVariables[kv.Key] = kv.Value;
        foreach (var kv in Macros)
            newContext.Macros[kv.Key] = kv.Value;
        foreach (var item in CommandHistory)
            newContext.CommandHistory.Add(item);
        foreach (var kv in GetAllDrivePaths())
            newContext.SetPath(kv.Key, kv.Value.ToArray());
        foreach (var item in DirectoryStack)
            newContext.DirectoryStack.Push(item);

        return newContext;
    }
}
```

### 5. BatchContext refactor: verwijder Console property

```csharp
internal class BatchContext
{
    public required IContext Context { get; set; }
    // Console property VERWIJDERD!

    // ... rest blijft hetzelfde (batch state)
}
```

### 6. Command execution flow

```csharp
// In CommandExecutor / BatchExecutor:
var execContext = context.StartNew();  // zonder redirections

// Met redirections:
var redirectedConsole = RedirectionHandler.Apply(...);
var execContext = context.StartNew(console: redirectedConsole);

// Execute command:
var batchContext = new BatchContext { Context = execContext };
await command.ExecuteAsync(args, batchContext, redirections);
```

### 7. Commands updaten

Alle commands:
```csharp
// OUD:
await batchContext.Console.Out.WriteLineAsync("hello");

// NIEUW:
await batchContext.Context.Console.Out.WriteLineAsync("hello");
```

### 8. Satellietapplicaties updaten

Tree en Subst gebruiken nu `context.Console` in plaats van `System.Console`:

```csharp
// Tree/Program.cs OUD:
await System.Console.Out.WriteLineAsync($"{rootPrefix}{rootDisplay}");

// NIEUW:
await context.Console.Out.WriteLineAsync($"{rootPrefix}{rootDisplay}");
```

## Line-ending strategie

Conform copilot-instructions:
- **Commands schrijven:** Altijd `\r\n` (DOS conventie)
- **Console output:** `\r\n` → `Environment.NewLine` (via LineEndingConvertingWriter)
- **File redirection:** `\r\n` behouden (StreamWriter met `NewLine = "\r\n"`)
- **Bij lezen:** Tolerant voor alle line breaks

## Test strategie

1. **Unit tests voor LineEndingConvertingWriter**
   - Test `Write("\r\n")` → schrijft native line ending
   - Test `Write("foo\r\nbar")` → converteert correct

2. **Context.StartNew() tests**
   - Deep copy werkt (wijzigingen in child niet zichtbaar in parent)
   - Console override werkt
   - Alle state wordt gekopieerd

3. **Integration tests**
   - `echo hello` → output heeft native line ending
   - `echo hello > file.txt` → file heeft `\r\n`
   - Commands kunnen `batchContext.Context.Console` gebruiken

4. **Satellietapplicatie tests**
   - Tree gebruikt `context.Console`
   - Output heeft correcte line endings
   - Redirections werken: `tree > file.txt`

## Acceptance criteria

- ✅ IContext heeft Console property
- ✅ IContext heeft StartNew(IConsole?) method
- ✅ LineEndingConvertingWriter converteert `\r\n` → native voor console
- ✅ File redirections behouden `\r\n`
- ✅ BatchContext.Console property is VERWIJDERD
- ✅ Commands gebruiken `batchContext.Context.Console`
- ✅ Geen directe `System.Console.Out/Error` toegang in command code
- ✅ Tree en Subst gebruiken `context.Console`
- ✅ Alle bestaande tests blijven slagen
- ✅ Thread-safe: elke execution heeft eigen context instance
- ✅ `System.Console.Title` en `.OutputEncoding` mogen nog direct (zijn state, geen I/O)

## Implementatie volgorde

1. Maak LineEndingConvertingWriter class
2. Update Console class om wrapper te gebruiken
3. Voeg Console property toe aan IContext interface
4. Implementeer StartNew() in Context base class
5. Update DosContext en UxContextAdapter constructors
6. Verwijder BatchContext.Console property
7. Update alle Commands om `batchContext.Context.Console` te gebruiken
8. Update command executors om StartNew() aan te roepen
9. Update Tree om `context.Console` te gebruiken
10. Update Subst om `context.Console` te gebruiken
11. Verwijder overige directe System.Console.Out/Error toegang
12. Test en verifieer alle tests slagen

## Later: Stap 14.2 - Delta-based optimalisatie (optioneel)

Na stap 14.1 werkt, kan de deep copy geoptimaliseerd worden met delta-dictionaries (copy-on-write):

```csharp
internal class DeltaDictionary<TKey, TValue> : IDictionary<TKey, TValue>
{
    private readonly IReadOnlyDictionary<TKey, TValue> _parent;
    private readonly Dictionary<TKey, TValue> _delta = new();

    public TValue this[TKey key]
    {
        get => _delta.TryGetValue(key, out var v) ? v : _parent[key];
        set => _delta[key] = value;  // Copy-on-write
    }
}
```

Dit is een **performance optimalisatie** - de functionaliteit blijft identiek.

