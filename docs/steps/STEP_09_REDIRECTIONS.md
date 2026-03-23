# STEP 09 - Piping + Bestandsredirectie

**Doel:** `>`, `>>`, `<`, `|`, `2>`, `2>&1` daadwerkelijk uitvoeren.

## Context

De parser herkent al alle redirectie-tokens en bouwt de AST correct — ze zijn opgeslagen als `Redirection`-records op de AST-nodes. **Deze stap voegt de runtime-uitvoering toe**: de executor moet deze redirecties omzetten in echte stream-koppelingen voordat een commando wordt aangeroepen.

Dit is **infrastructuur**, niet één commando. Na deze stap werken `echo hello > out.txt`, `type in.txt | find "x"`, en `somecommand 2>nul`.

## Hoe redirecties nu in de AST zitten

```csharp
// Redirection record (bestaat al)
public record Redirection(
    RedirectionKind Kind,    // Output, OutputAppend, Input, Error, ErrorToOutput, OutputToError
    IToken? Target           // Bestandsnaam of "&1" / "&2"
);

// CommandNode heeft:
IReadOnlyList<Redirection> Redirections { get; }
```

## Wat er geïmplementeerd moet worden

### A. Redirectie-context opzetten vóór ExecuteAsync

De `Dispatcher` of `ExecutionContext` wikkelt elke commanduitvoering in een `using`-blok dat:
1. Streams opent (bestanden, pipes)
2. `IConsole` vervangt door een `RedirectedConsole` die naar die streams schrijft/leest
3. Streams sluit na afloop

```csharp
public async Task<int> ExecuteWithRedirections(
    IContext ctx, IConsole console,
    ICommandNode node, ICommand command)
{
    using var redirections = RedirectionContext.Create(node.Redirections, ctx);
    var redirectedConsole = redirections.Apply(console);
    return await command.ExecuteAsync(ctx, node.Arguments, ctx.CurrentBatch!, redirectedConsole);
}
```

### B. Redirectie-typen

| Syntax | Kind | Gedrag |
|---|---|---|
| `> file` | `Output` | Stdout → file (overschrijven, creëren als niet bestaat) |
| `>> file` | `OutputAppend` | Stdout → file (toevoegen) |
| `< file` | `Input` | Stdin → file (lezen) |
| `2> file` | `Error` | Stderr → file (overschrijven) |
| `2>> file` | `ErrorAppend` | Stderr → file (toevoegen) |
| `2>&1` | `ErrorToOutput` | Stderr → zelfde als stdout |
| `1>&2` | `OutputToError` | Stdout → zelfde als stderr |
| `> nul` | `Output` + `nul` | Weggooi-stream (NUL device) |
| `2> nul` | `Error` + `nul` | Stderr weggoooien |

### C. Piping (`|`)

Een `PipelineNode` koppelt de stdout van het linker-commando aan de stdin van het rechter-commando.

```csharp
// PipelineNode bestaat al in AST
// Implementatie:
public async Task ExecutePipeline(PipelineNode node, IContext ctx, IConsole console)
{
    var pipe = new Pipe();  // System.IO.Pipelines of MemoryStream
    
    // Voer linker commando uit met stdout → pipe.Writer
    var leftConsole = console.WithOutput(pipe.Writer);
    var leftTask = ExecuteNode(node.Left, ctx, leftConsole);
    
    // Voer rechter commando uit met stdin ← pipe.Reader
    var rightConsole = console.WithInput(pipe.Reader);
    var rightTask = ExecuteNode(node.Right, ctx, rightConsole);
    
    await Task.WhenAll(leftTask, rightTask);
}
```

**Opmerking:** Pipes moeten asynchroon werken om deadlocks te voorkomen (beide kanten moeten tegelijk kunnen lezen/schrijven).

## IConsole uitbreiden

```csharp
public interface IConsole
{
    TextReader In  { get; }
    TextWriter Out { get; }
    TextWriter Error { get; }
    
    // NIEUW — voor redirectie:
    IConsole WithOutput(TextWriter newOut);
    IConsole WithError(TextWriter newError);
    IConsole WithInput(TextReader newIn);
}
```

## TDD — Stap voor stap

**Bestand:** `Bat.UnitTests/RedirectionTests.cs`

### Test 1: `> file` schrijft stdout naar bestand

```csharp
[Fact]
public async Task Redirect_Output_WritesToFile()
{
    var fs = new TestFileSystem();
    var ctx = new TestContext(fs);
    var console = new TestConsole();

    await Execute(ctx, console, @"echo hello > C:\out.txt");

    Assert.Equal("hello", fs.ReadFile('C', ["out.txt"]).Trim());
    Assert.Empty(console.OutputLines);  // Niet op scherm
}
```

### Test 2: `>> file` voegt toe aan bestand

```csharp
[Fact]
public async Task Redirect_OutputAppend_AppendsToFile()
{
    var fs = new TestFileSystem();
    fs.WriteFile('C', ["out.txt"], "line1\r\n");
    var ctx = new TestContext(fs);
    var console = new TestConsole();

    await Execute(ctx, console, @"echo line2 >> C:\out.txt");

    var content = fs.ReadFile('C', ["out.txt"]);
    Assert.Contains("line1", content);
    Assert.Contains("line2", content);
}
```

### Test 3: `< file` leest stdin uit bestand

```csharp
[Fact]
public async Task Redirect_Input_ReadsFromFile()
{
    var fs = new TestFileSystem();
    fs.WriteFile('C', ["in.txt"], "hello from file");
    var ctx = new TestContext(fs);
    var console = new TestConsole();

    await Execute(ctx, console, @"set /p X= < C:\in.txt");

    Assert.Equal("hello from file", ctx.EnvironmentVariables["X"]);
}
```

### Test 4: `2> nul` onderdrukt stderr

```csharp
[Fact]
public async Task Redirect_ErrorToNul_SuppressesErrors()
{
    var ctx = new TestContext();
    var console = new TestConsole();

    // Een commando dat een foutmelding schrijft
    await Execute(ctx, console, @"cd nonexistent 2>nul");

    Assert.Empty(console.ErrorLines);
}
```

### Test 5: `2>&1` stuurt stderr naar stdout

```csharp
[Fact]
public async Task Redirect_ErrorToOutput_MergesStreams()
{
    var ctx = new TestContext();
    var console = new TestConsole();

    await Execute(ctx, console, @"cd nonexistent 2>&1");

    // Foutmelding staat nu in stdout, niet stderr
    Assert.NotEmpty(console.OutputLines);
    Assert.Empty(console.ErrorLines);
}
```

### Test 6: Pipe koppelt stdout naar stdin

```csharp
[Fact]
public async Task Pipe_ConnectsOutputToInput()
{
    var ctx = new TestContext();
    var console = new TestConsole();

    // Stel dat FindCommand zoekt in stdin
    await Execute(ctx, console, @"echo hello world | find ""world""");

    Assert.Single(console.OutputLines);
    Assert.Contains("world", console.OutputLines[0]);
}
```

### Test 7: `> nul` onderdrukt stdout

```csharp
[Fact]
public async Task Redirect_OutputToNul_SuppressesOutput()
{
    var ctx = new TestContext();
    var console = new TestConsole();

    await Execute(ctx, console, @"echo hello > nul");

    Assert.Empty(console.OutputLines);
}
```

### Test 8: Gecombineerde redirectie

```csharp
[Fact]
public async Task Redirect_Combined_WorksCorrectly()
{
    var fs = new TestFileSystem();
    var ctx = new TestContext(fs);
    var console = new TestConsole();

    // Stdout naar bestand, stderr weggooien
    await Execute(ctx, console, @"echo hello > C:\out.txt 2>nul");

    Assert.Equal("hello", fs.ReadFile('C', ["out.txt"]).Trim());
    Assert.Empty(console.OutputLines);
    Assert.Empty(console.ErrorLines);
}
```

## Implementatie Volgorde

1. Schrijf alle tests (rood)
2. Breid `IConsole` uit met `WithOutput`, `WithError`, `WithInput`
3. Implementeer `NulWriter` (weggooi-TextWriter)
4. Implementeer `RedirectionContext` (opent streams, bouwt redirected console)
5. Koppel `RedirectionContext` in de dispatcher-uitvoering
6. Implementeer pipe-uitvoering in `PipelineNode`-handler (asynchroon!)
7. Run alle tests → groen

## Aandachtspunten

- **NUL device:** op Windows is `nul` (case-insensitief) de weggooi-stream; op Unix `/dev/null`
- **Pipe deadlock:** gebruik `PipeWriter`/`PipeReader` of aparte threads zodat producer niet blokkeert op consumer
- **Bestandspaden in redirectie:** worden opgelost t.o.v. de huidige directory in `IContext`
- **Volgorde van redirecties telt:** `2>&1 > file` is anders dan `> file 2>&1` (CMD-semantiek)

## Acceptance Criteria (Definition of Done)

- [ ] `echo hello > out.txt` schrijft naar bestand (niet naar scherm)
- [ ] `echo hello >> out.txt` voegt toe aan bestand
- [ ] `type in.txt | find "x"` koppelt pipes asynchroon
- [ ] `2>nul` onderdrukt stderr
- [ ] `2>&1` stuurt stderr naar stdout
- [ ] `> nul` onderdrukt stdout
- [ ] NUL device werkt op Windows en Unix
- [ ] Geen deadlocks bij pipes
- [ ] Alle bestaande tests slagen nog steeds
