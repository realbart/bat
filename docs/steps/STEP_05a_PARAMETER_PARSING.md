# STEP_05a: Parameter Parsing — `ArgumentSet` class

## Motivatie

Elke command doet nu dit als eerste stap:

```csharp
string args = string.Concat(arguments.Select(t => t.Raw)).TrimStart();
var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
```

Twee problemen:

1. **Quoted paths breken**: `dir "mijn map" /B` split geeft `["\"mijn", "map\"", "/B"]`
2. **Boilerplate**: elke command reimplementeert switch-parsing

## Doel

`IReadOnlyList<IToken>` in `ICommand.ExecuteAsync` vervangen door een `ArgumentSet` object dat:

- Tokens correct splits (quotes en escaped spaces gerespecteerd)
- `HasFlag`, `GetValue`, `GetValues`, `Positionals`, `FullArgument` exposeert
- De ruwe tokenlijst beschikbaar houdt voor commands die die nodig hebben (ECHO, SET)
- `/? ` automatisch detecteert

## ReactOS referentie

ReactOS CMD heeft **geen** equivalent gedeeld data-structuur. Elk command in `cmd_dir.c`,
`cmd_cd.c` etc. parst zelf zijn argument-string met `GetNextArgument()` / handmatige loops.
Er is geen `ARGUMENTS` struct. We definiëren dus onze eigen API, namen in .NET-stijl.

Referentie: https://doxygen.reactos.org/dir_b985591bf7ce7fa90b55f1035a6cc4ab.html

## Wijzigingen

### 1. `BuiltInCommandAttribute` — voeg `Flags` en `Options` toe

Multi-letter names zijn mogelijk (bijv. `"CD"`) en worden gescheiden door spaties in de string.

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
internal class BuiltInCommandAttribute(string name) : Attribute
{
    public string Name { get; } = name;

    // Spatie-gescheiden flag-namen (boolean, geen waarde). Bijv.: "B W L S P Q"
    public string Flags { get; init; } = "";

    // Spatie-gescheiden option-namen (nemen een waarde na : of als volgend woord). Bijv.: "A O T"
    public string Options { get; init; } = "";
}
```

Voorbeelden:

```csharp
[BuiltInCommand("dir",   Flags = "B W L S P Q N", Options = "A O T")]
[BuiltInCommand("cd",    Flags = "D")]
[BuiltInCommand("chdir", Flags = "D")]
[BuiltInCommand("exit",  Flags = "B")]
[BuiltInCommand("set",   Flags = "A P")]
[BuiltInCommand("echo")]   // geen flags/options — alles is FullArgument
```

### 2. `ArgumentSpec` — nieuw record

Wordt één keer per command-type berekend bij startup.

```csharp
internal record ArgumentSpec(
    FrozenSet<string> Flags,   // opgegeven Flags, uppercase
    FrozenSet<string> Options) // opgegeven Options, uppercase
{
    public static readonly ArgumentSpec Empty =
        new(FrozenSet<string>.Empty, FrozenSet<string>.Empty);

    // Bouw een gecombineerde spec van alle BuiltInCommandAttribute op één klasse
    public static ArgumentSpec From(IEnumerable<BuiltInCommandAttribute> attrs);
}
```

### 3. `BuiltInCommandRegistry` — sla `ArgumentSpec` op per command-type

De registry berekent de spec bij startup (via `BuildRegistry`) en nooit daarna. Geen
reflection per aanroep.

```csharp
internal static class BuiltInCommandRegistry
{
    // per type: (Func<ICommand> factory, ArgumentSpec spec)
    private record Entry(Type CommandType, ArgumentSpec Spec);
    private static readonly FrozenDictionary<string, Entry> Entries = BuildRegistry();

    public static ArgumentSpec GetSpec(string commandName);
    public static Type? GetCommandType(string commandName);
    public static bool IsBuiltInCommand(string commandName);
}
```

### 4. `IBuiltInCommandToken` — voeg `Spec` property toe

Zodat de dispatcher de spec kan ophalen zonder een tweede registry-lookup.

```csharp
internal interface IBuiltInCommandToken : IToken
{
    ICommand CreateCommand();
    ArgumentSpec Spec { get; }
}

internal class BuiltInCommandToken<TCmd>(string value) : TokenBase(value), IBuiltInCommandToken
    where TCmd : ICommand, new()
{
    // Eenmalig berekend per gesloten generieke instantie
    private static readonly ArgumentSpec _spec = ArgumentSpec.From(
        typeof(TCmd).GetCustomAttributes<BuiltInCommandAttribute>());

    public ICommand CreateCommand() => new TCmd();
    public ArgumentSpec Spec => _spec;
}
```

### 5. `IArgumentSet` — interface in Context-project

Staat in het `Context`-project (gedeelde interfaces) zodat externe executables
(stap 6/7) hetzelfde type kunnen ontvangen als ingebouwde commands.

```csharp
public interface IArgumentSet
{
    string FullArgument { get; }
    IReadOnlyList<string> Positionals { get; }
    bool IsHelpRequest { get; }
    bool HasFlag(char name);
    bool HasFlag(string name);
    string[] GetValues(char name);
    string[] GetValues(string name);
    string? GetValue(char name);
    string? GetValue(string name);
}
```

### 6. `Arguments` — implementatie in Bat-project

Implementeert `IArgumentSet`.

```csharp
internal sealed class Arguments : IArgumentSet
{
    /// <summary>
    /// Volledige argument-tekst na de command-naam, leading whitespace getrimd.
    /// Gebruikt door ECHO en SET /A die de hele rest als input nemen.
    /// </summary>
    public string FullArgument { get; }

    /// <summary>
    /// Positionele argumenten (niet-switch woorden), quotes al gestript.
    /// </summary>
    public IReadOnlyList<string> Positionals { get; }

    /// <summary>
    /// True als /? het enige argument was.
    /// </summary>
    public bool IsHelpRequest { get; }

    // ── Flags ─────────────────────────────────────────────────────────────

    public bool HasFlag(char name);
    public bool HasFlag(string name);

    // ── Options ───────────────────────────────────────────────────────────

    /// <summary>
    /// Geeft alle waarden voor deze option-naam. Bijv. /M:a /M:b → ["a", "b"].
    /// Geeft [] als de option niet aanwezig was.
    /// </summary>
    public string[] GetValues(char name);
    public string[] GetValues(string name);

    /// <summary>
    /// Geeft de enige waarde, of null als de option niet aanwezig was.
    /// Geeft de eerste waarde als er meerdere zijn.
    /// </summary>
    public string? GetValue(char name);
    public string? GetValue(string name);

    // ── Fabriek ───────────────────────────────────────────────────────────

    public static Arguments Parse(IReadOnlyList<IToken> tokens, ArgumentSpec spec);
}
```

> **Waarom geen `Tokens` property?**
> Alle variabele-substitutie (`%VAR%`, `%1`, delayed expansion `!VAR!`) vindt
> plaats *vóórdat* het commando wordt aangeroepen. De tokens bevatten daarna
> geen informatie meer die niet ook al in `FullArgument` en `Positionals` zit.
> Commands hebben de ruwe tokens dus niet nodig.

#### Parse-algoritme

1. Splits de tokenlijst in "woorden" door runs van niet-`WhitespaceToken` tokens samen te
   voegen.
2. Elk woord wordt een `string`:
   - `TextToken` → `.Raw`
   - `QuotedTextToken` → `.Value` (buitenste quotes eraf)
3. Voor elk woord:
   - Precies `/?` → `IsHelpRequest = true`; stop verdere verwerking
   - Begint met `/` of `-` → switch:
     - Switch-naam = tekst tot aan `:` of einde woord, uppercase
     - Switch-naam in `spec.Flags` → markeer flag als aanwezig
     - Switch-naam in `spec.Options`:
       - Waarde = tekst na `:` in hetzelfde woord, **of**
         het eerstvolgende woord als er geen `:` aanwezig is (greedy: wordt verbruikt)
     - Onbekende switch → silently doorgelaten (bewaard in `UnknownSwitches` voor debugging)
   - Anders → voeg toe aan `Positionals`
4. `FullArgument` = tokens aaneengesloten, leading whitespace getrimd (onbewerkt)

### 7. `ICommand` interface — parameter-type wijzigt

`IReadOnlyList<IToken>` verdwijnt volledig uit het interface. Alle substitutie is
klaar vóór aanroep; commands werken uitsluitend met `Arguments`.

```csharp
internal interface ICommand
{
    Task<int> ExecuteAsync(
        Arguments arguments,
        BatchContext batchContext,
        IReadOnlyList<Redirection> redirections
    );
}
```

### 8. Dispatcher — maakt `Arguments` aan voor aanroep

De dispatcher heeft nog wél de tokens nodig om `Arguments.Parse()` aan te roepen,
maar geeft daarna alleen het `Arguments`-object door.

```csharp
private static Task<int> ExecuteCommandNodeAsync(BatchContext bc, CommandNode cmd)
{
    if (cmd.Head is IBuiltInCommandToken builtIn)
    {
        var rawArgs = cmd.Tail.SkipWhile(static t => t is WhitespaceToken).ToList();
        var args = Arguments.Parse(rawArgs, builtIn.Spec);
        return builtIn.CreateCommand().ExecuteAsync(args, bc, cmd.Redirections);
    }

    // externe commando's: stap 6
    return Task.FromResult(0);
}
```

## Migratie bestaande commands

| Command | Was | Wordt |
|---|---|---|
| `EchoCommand` | `string.Concat(t.Raw)` | `args.FullArgument` |
| `ExitCommand` | Handmatige `/B` check | `args.HasFlag("B")` |
| `CdCommand` | Handmatige `/D` check + split | `args.HasFlag("D")`, `args.Positionals.FirstOrDefault()` |
| `DirCommand` | `ParseOptions(string)` methode (250+ regels) | `args.HasFlag("B")`, `args.GetValue("O")`, etc. |
| `SetCommand` | Handmatige `/A` en `/P` checks | `args.HasFlag("A")`, `args.HasFlag("P")`, `args.FullArgument` voor de rest |
| `ClsCommand` | Geen args gebruikt | Geen wijziging inhoudelijk |
| `RemCommand` | Geen args gebruikt | Geen wijziging inhoudelijk |

## Test-strategie (TDD)

Unit test `Arguments.Parse()` direct met `ArgumentSpec`:

| Input | Verwacht |
|---|---|
| `/B /S "mijn map"` | `HasFlag('B')=true`, `HasFlag('S')=true`, `Positionals=["mijn map"]` |
| `/O:N` | `GetValue('O')="N"` |
| `/O N` (O is option) | `GetValue('O')="N"`, N niet in Positionals |
| `/A:HD` | `GetValue('A')="HD"` |
| `/A /B` (beide flags) | `HasFlag('A')=true`, `HasFlag('B')=true`, `Positionals=[]` |
| `/?` | `IsHelpRequest=true` |
| `"Program Files"` | `Positionals=["Program Files"]` |
| `/M:a /M:b` | `GetValues('M')=["a","b"]` |
| `GetValue('M')` met twee waarden | `"a"` (eerste waarde) |
| leeg | `Positionals=[]`, `FullArgument=""`, `IsHelpRequest=false` |

Daarna: alle bestaande 255 tests slagen na migratie.

## Acceptance criteria

- `Arguments.Parse` tests slagen
- `dir "mijn pad met spaties" /B` geeft correct de directory listing
- `cd "Program Files"` navigeert correct
- Alle bestaande tests blijven slagen na migratie van commands
- `DirCommand.ParseOptions` private methode verdwijnt; vervangen door `Arguments`-aanroepen
