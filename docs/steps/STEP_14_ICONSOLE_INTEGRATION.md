# Stap 14: IConsole Integratie in IContext

## Doel

Alle console-toegang moet via IConsole lopen (niet direct via `System.Console`), en IConsole moet beschikbaar zijn via IContext voor satellietapplicaties.

## Achtergrond

Momenteel:
- BatchContext heeft IConsole (goed voor redirections)
- Sommige code gebruikt nog steeds `System.Console` direct (bijv. TITLE, PAUSE, Tree)
- Satellietapplicaties kunnen niet bij de console via IContext
- Line-ending conversie (`\r\n` → `Environment.NewLine`) gebeurt niet automatisch

## Problemen met huidige aanpak

1. **Satellietapplicaties**: Tree.exe gebruikt `System.Console.Out.WriteLineAsync` direct
2. **Inconsistentie**: Commands gebruiken mix van `batchContext.Console` en `System.Console`
3. **Line endings**: Geen automatische `\r\n` → native conversie voor console output
4. **Testability**: Code die `System.Console` gebruikt is moeilijker te testen

## Scope

### 1. IContext uitbreiden met IConsole

```csharp
public interface IContext
{
    IConsole Console { get; }
    // ... bestaande members
}
```

### 2. LineEndingConvertingWriter implementeren

Wrapper die automatisch `\r\n` converteert naar `Environment.NewLine`:

```csharp
internal class LineEndingConvertingWriter(TextWriter inner) : TextWriter
{
    public override Encoding Encoding => inner.Encoding;
    
    public override void Write(string? value)
    {
        if (value != null)
            inner.Write(value.Replace("\r\n", Environment.NewLine));
    }
    
    // Override alle Write variants...
}
```

### 3. Console implementatie aanpassen

```csharp
internal class Console : IConsole
{
    public TextWriter Out { get; } = new LineEndingConvertingWriter(System.Console.Out);
    public TextWriter Error { get; } = new LineEndingConvertingWriter(System.Console.Error);
    // ...
}
```

### 4. Verwijder directe System.Console toegang

Zoek en vervang alle:
- `System.Console.Out` → `context.Console.Out`
- `System.Console.Error` → `context.Console.Error`  
- `System.Console.Title` → blijft (is state, geen I/O)
- `System.Console.OutputEncoding` → blijft (is state)

**Betrokken bestanden:**
- `Tree/Program.cs` - gebruikt `System.Console.Out` en `System.Console.OutputEncoding`
- `Bat/Commands/TitleCommand.cs` - `System.Console.Title` (mag blijven)
- `Bat/Commands/PauseCommand.cs` - moet `batchContext.Console` gebruiken (al correct)

### 5. Satellietapplicaties updaten

Tree moet IContext.Console gebruiken in plaats van System.Console.Out:
```csharp
await context.Console.Out.WriteLineAsync($"{rootPrefix}{rootDisplay}");
```

## Conventie

**Line-ending strategie** (uit copilot-instructions):
- Commands schrijven altijd `\r\n` (DOS conventie)
- Bij console output: `\r\n` → `Environment.NewLine` (via LineEndingConvertingWriter)
- Bij file redirection: `\r\n` behouden (StreamWriter met `NewLine = "\r\n"`)
- Bij lezen: tolerant voor alle line breaks

## Test strategie

1. **Unit tests voor LineEndingConvertingWriter**
   - Test `Write("\r\n")` → schrijft native line ending
   - Test `Write("foo\r\nbar")` → converteert correct
   
2. **Integration tests**
   - `echo hello` → output heeft native line ending
   - `echo hello > file.txt` → file heeft `\r\n`

3. **Satellietapplicatie tests**
   - Tree gebruikt IContext.Console
   - Output heeft correcte line endings

## Acceptance criteria

- ✅ IContext heeft Console property
- ✅ LineEndingConvertingWriter converteert `\r\n` → native voor console
- ✅ File redirections behouden `\r\n`
- ✅ Geen directe `System.Console.Out/Error` toegang in command code
- ✅ Tree en andere satellietapplicaties gebruiken `context.Console`
- ✅ Alle bestaande tests blijven slagen
- ✅ `System.Console.Title` en `.OutputEncoding` mogen nog direct worden gebruikt (zijn state, geen I/O)

## Implementatie volgorde

1. Maak LineEndingConvertingWriter
2. Update Console class om wrapper te gebruiken
3. Voeg Console property toe aan IContext
4. Update DosContext en UxContextAdapter
5. Fix Tree om context.Console te gebruiken
6. Verwijder overige directe System.Console.Out/Error toegang
7. Test en verifieer
