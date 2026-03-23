# STEP 04 - Minimale Werkende REPL

**Doel:** Eerste interactieve sessie — je kunt typen en output zien.

## Context

Na stap 1 (design repair) is `ICommand.ExecuteAsync` gedefinieerd maar retourneert de dispatcher altijd `true` zonder iets te doen. Na stap 3 werkt `IFileSystem`. **Stap 4 maakt de REPL voor het eerst interactief.**

De commando's in deze stap zijn bewust simpel gekozen: ze vereisen minimale infrastructuur maar tonen direct dat de keten parser → dispatcher → command werkt.

**Commando's in deze stap:**

| Commando | Reden |
|---|---|
| `ECHO` | Het "hello world" van een CLI; vereist vrijwel niets |
| `SET` | Bewijst dat environment variables werken; vereist `IContext` |
| `REM` | Triviale no-op; dekt commentaarregels in batch |
| `EXIT` | Sluit Bat; vereist exitcode-doorgave |
| `CLS` | Scherm wissen; dekt console-interactie |

## Vereiste wijzigingen

### A. Dispatcher implementeren

De dispatcher krijgt een `ParsedCommand` (AST) en roept de juiste `ICommand.ExecuteAsync` aan.

```csharp
public class Dispatcher : IDispatcher
{
    private readonly IReadOnlyDictionary<string, ICommand> _commands;

    public Dispatcher()
    {
        _commands = new Dictionary<string, ICommand>(StringComparer.OrdinalIgnoreCase)
        {
            ["ECHO"]   = new EchoCommand(),
            ["SET"]    = new SetCommand(),
            ["REM"]    = new RemCommand(),
            ["EXIT"]   = new ExitCommand(),
            ["CLS"]    = new ClsCommand(),
        };
    }

    public async Task<bool> ExecuteCommandAsync(IContext context, IConsole console, ParsedCommand command)
    {
        // Route to ICommand
        // Return false when EXIT is called (stops REPL loop)
    }
}
```

### B. ECHO

```
ECHO [ON | OFF]
ECHO [message]

Displays messages, or turns command echoing on or off.

  ECHO ON    Turns echo on  (default)
  ECHO OFF   Turns echo off
  ECHO.      Prints empty line
```

Gedrag:
- `ECHO` (geen args) → toon `ECHO is on.` of `ECHO is off.`
- `ECHO ON` / `ECHO OFF` → zet `IContext.EchoEnabled`
- `ECHO.` (punt direct na ECHO, geen spatie) → lege regel
- `ECHO message` → toon message (inclusief meerdere spaties)

### C. SET

```
SET [variable=[string]]
SET [variable]
SET /A expression
SET /P variable=[promptString]

  variable    Specifies the environment-variable name.
  string      Specifies a series of characters to assign to the variable.
  expression  Specifies an arithmetic expression.
```

Gedrag:
- `SET` (geen args) → toon alle variabelen alphabetisch gesorteerd
- `SET X` → toon alle variabelen die beginnen met X
- `SET X=waarde` → stel variabele in
- `SET X=` → verwijder variabele
- `SET /A X=2+3` → reken uit en sla op (ondersteun: `+`, `-`, `*`, `/`, `%`, `&`, `|`, `^`, `~`, `<<`, `>>`)
- `SET /P X=Voer een waarde in: ` → lees input van gebruiker

### D. REM

```
REM [comment]

Records comments (remarks) in a batch file or CONFIG.SYS.
```

Gedrag: no-op, retourneert altijd 0.

### E. EXIT

```
EXIT [/B] [exitCode]

Quits the CMD.EXE program (command interpreter) or the current batch script.

  /B          Specifies to exit the current batch script instead of CMD.EXE.
              If executed outside of a batch script, it will quit CMD.EXE.
  exitCode    Specifies a numeric number. If /B is specified, sets ERRORLEVEL
              to that number. If quitting CMD.EXE, sets the process exit code
              with that number.
```

Gedrag:
- `EXIT` → dispatcher retourneert `false` → REPL-loop stopt
- `EXIT 1` → zet exit code op 1
- `EXIT /B` in REPL → zelfde als `EXIT` (buiten batch context)
- `EXIT /B` in batch → verlaat alleen de batch, niet Bat zelf

### F. CLS

```
CLS

Clears the screen.
```

Gedrag: stuur ANSI escape code `\x1b[2J\x1b[H` naar stdout (werkt op moderne Windows terminals).

## TDD — Stap voor stap

**Bestand:** `Bat.UnitTests/DispatcherTests.cs`

### Test 1: Dispatcher routes ECHO naar EchoCommand

```csharp
[Fact]
public async Task Dispatcher_RoutesEchoCommand()
{
    var console = new TestConsole();
    var ctx = new TestContext();
    var dispatcher = new Dispatcher(console);

    var cmd = Parser.Parse(ctx, "echo hello world");
    await dispatcher.ExecuteCommandAsync(ctx, console, cmd);

    Assert.Equal("hello world", console.OutputLines.Single());
}
```

### Test 2: ECHO zonder args toont status

```csharp
[Fact]
public async Task Echo_NoArgs_ShowsStatus()
{
    var (ctx, console) = Setup();
    await Execute(ctx, console, "echo");
    Assert.Equal("ECHO is on.", console.OutputLines.Single());
}
```

### Test 3: ECHO OFF / ON

```csharp
[Fact]
public async Task Echo_Off_SetsContext()
{
    var (ctx, console) = Setup();
    await Execute(ctx, console, "ECHO OFF");
    Assert.False(ctx.EchoEnabled);
}

[Fact]
public async Task Echo_On_SetsContext()
{
    var ctx = CreateContext(echoEnabled: false);
    var console = new TestConsole();
    await Execute(ctx, console, "ECHO ON");
    Assert.True(ctx.EchoEnabled);
}
```

### Test 4: ECHO. geeft lege regel

```csharp
[Fact]
public async Task Echo_Dot_PrintsEmptyLine()
{
    var (ctx, console) = Setup();
    await Execute(ctx, console, "echo.");
    Assert.Equal("", console.OutputLines.Single());
}
```

### Test 5: SET zonder args toont alle variabelen gesorteerd

```csharp
[Fact]
public async Task Set_NoArgs_ShowsAllVariables()
{
    var ctx = CreateContext(envVars: new() { ["Z"] = "last", ["A"] = "first" });
    var console = new TestConsole();
    await Execute(ctx, console, "set");

    Assert.Equal("A=first", console.OutputLines[0]);
    Assert.Equal("Z=last", console.OutputLines[1]);
}
```

### Test 6: SET X=waarde stelt variabele in

```csharp
[Fact]
public async Task Set_Assign_SetsVariable()
{
    var (ctx, console) = Setup();
    await Execute(ctx, console, "SET MYVAR=hello");
    Assert.Equal("hello", ctx.EnvironmentVariables["MYVAR"]);
}
```

### Test 7: SET X= verwijdert variabele

```csharp
[Fact]
public async Task Set_EmptyValue_RemovesVariable()
{
    var ctx = CreateContext(envVars: new() { ["X"] = "val" });
    var console = new TestConsole();
    await Execute(ctx, console, "SET X=");
    Assert.False(ctx.EnvironmentVariables.ContainsKey("X"));
}
```

### Test 8: SET /A rekent uit

```csharp
[Theory]
[InlineData("SET /A X=2+3",   "5")]
[InlineData("SET /A X=10-4",  "6")]
[InlineData("SET /A X=3*4",   "12")]
[InlineData("SET /A X=10/3",  "3")]   // integer division
[InlineData("SET /A X=10%%3", "1")]   // modulo (doubled % in test string)
public async Task Set_Arithmetic_ComputesResult(string command, string expected)
{
    var (ctx, console) = Setup();
    await Execute(ctx, console, command);
    Assert.Equal(expected, ctx.EnvironmentVariables["X"]);
}
```

### Test 9: EXIT retourneert false

```csharp
[Fact]
public async Task Exit_ReturnsFalse()
{
    var (ctx, console) = Setup();
    var cmd = Parser.Parse(ctx, "exit");
    var result = await dispatcher.ExecuteCommandAsync(ctx, console, cmd);
    Assert.False(result);
}
```

### Test 10: EXIT met code

```csharp
[Fact]
public async Task Exit_WithCode_SetsExitCode()
{
    var (ctx, console) = Setup();
    await Execute(ctx, console, "exit 42");
    Assert.Equal(42, ctx.ErrorCode);
}
```

### Test 11: REM doet niets

```csharp
[Fact]
public async Task Rem_DoesNothing()
{
    var (ctx, console) = Setup();
    await Execute(ctx, console, "rem dit is een commentaar");
    Assert.Empty(console.OutputLines);
    Assert.Equal(0, ctx.ErrorCode);
}
```

## Implementatie Volgorde

1. Schrijf alle tests (rood)
2. Maak `TestConsole` en `TestContext` helper-klassen
3. Implementeer `Dispatcher` met routing-logica
4. Implementeer `RemCommand` (1 regel)
5. Implementeer `EchoCommand`
6. Implementeer `SetCommand` (zonder /A eerst, dan /A)
7. Implementeer `ExitCommand`
8. Implementeer `ClsCommand`
9. Registreer alle commands in Dispatcher
10. Run alle tests → groen

## Acceptance Criteria (Definition of Done)

- [ ] Dispatcher routes commando's correct op basis van naam (case-insensitive)
- [ ] `echo hello` → `hello`
- [ ] `echo.` → lege regel
- [ ] `ECHO OFF` / `ECHO ON` werkt
- [ ] `set X=foo` stelt variabele in
- [ ] `set /a X=2+3` werkt (operators: + - * / % & | ^ ~ << >>)
- [ ] `set /p` leest input van console
- [ ] `rem` is no-op
- [ ] `exit` stopt REPL-loop
- [ ] `exit 1` stopt met exit code 1
- [ ] `cls` wist scherm
- [ ] Alle bestaande tests slagen nog steeds
