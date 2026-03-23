# STEP 01 - Repareer Ontwerpkeuzes

**Doel:** Fix fundamentele architectuurkeuzes om CMD-compatibel te worden.

## Context

De huidige implementatie heeft enkele ontwerpkeuzes die botsen met hoe ReactOS CMD werkt:

### Probleem 1: Variable expansion tijdens tokenizing
De tokenizer expandeert `%VAR%` al tijdens het tokenizen. Dit moet gebeuren **VOOR** tokenizing.

### Probleem 2: ToString() toont raw input
`ToString()` moet de input tonen **na** parameter-expansie (zoals CMD echo doet).

### Probleem 3: Geen BatchContext
Er is geen BatchContext struct zoals ReactOS `BATCH_CONTEXT` heeft.

### Probleem 4: Generieke CommandNode
Type informatie gaat verloren bij mapping naar `CommandNode(IToken Head, ...)`.

## Test-First Aanpak

### Tests schrijven (VOOR implementatie):

**Test 1: Parameter expansion gebeurt voor parsing**
```csharp
[Fact]
public void ParameterExpansion_HappensBeforeParsing()
{
    // Arrange
    var bc = new BatchContext { Parameters = ["test.bat", "arg1", "arg2"] };
    
    // Act
    var expanded = ExpandBatchParameters("echo %1 and %2", bc);
    
    // Assert
    Assert.Equal("echo arg1 and arg2", expanded);
}
```

**Test 2: Niet-gevonden parameters blijven literaal**
```csharp
[Fact]
public void UnsetParameter_RemainsLiteral()
{
    // Arrange
    var bc = new BatchContext { Parameters = ["test.bat", null, null] };
    
    // Act
    var expanded = ExpandBatchParameters("echo %1 and %2", bc);
    
    // Assert
    Assert.Equal("echo %1 and %2", expanded);  // Blijft %1, niet ""
}
```

**Test 3: Environment variable expansion**
```csharp
[Fact]
public void EnvironmentVariable_GetsExpanded()
{
    // Arrange
    var ctx = new Context();
    ctx.EnvironmentVariables["TEST"] = "value";
    
    // Act
    var expanded = ExpandEnvironmentVariables("echo %TEST%", ctx);
    
    // Assert
    Assert.Equal("echo value", expanded);
}
```

**Test 4: Niet-gevonden variabelen blijven literaal**
```csharp
[Fact]
public void UnsetVariable_RemainsLiteral()
{
    // Arrange
    var ctx = new Context();
    
    // Act
    var expanded = ExpandEnvironmentVariables("echo %NOTFOUND%", ctx);
    
    // Assert
    Assert.Equal("echo %NOTFOUND%", expanded);  // Blijft literal
}
```

**Test 5: Typed node hierarchy**
```csharp
[Fact]
public void Parser_CreatesTypedNodes()
{
    // Arrange
    var parser = new Parser();
    
    // Act
    var node = parser.ParseCommand("echo hello");
    
    // Assert
    Assert.IsType<BuiltInCommandNode<EchoCommand>>(node);
}
```

**Test 6: REPL BatchContext singleton**
```csharp
[Fact]
public void ReplBatchContext_IsSingleton()
{
    // Arrange & Act
    var bc1 = ReplBatchContext.Value;
    var bc2 = ReplBatchContext.Value;
    
    // Assert
    Assert.Same(bc1, bc2);  // Zelfde instance
    Assert.Null(bc1.BatchFilePath);  // REPL mode
    Assert.Equal("CMD", bc1.Parameters[0]);
}
```

## Implementatie Stappen

### 1.1 BatchContext class creëren

**Bestand:** `Bat/Execution/BatchContext.cs`

```csharp
namespace Bat.Execution;

/// <summary>
/// Batch execution state - analogous to ReactOS BATCH_CONTEXT
/// </summary>
public class BatchContext
{
    // File state (null voor REPL)
    public string? BatchFilePath { get; set; }
    public string? FileContent { get; set; }
    public int FilePosition { get; set; }
    public int LineNumber { get; set; }
    
    // Parameters (%0..%9)
    // REPL: ["CMD", null, ...] → %1 blijft %1
    // Batch: [filePath, arg1, ...] → %1 wordt arg1
    public string?[] Parameters { get; set; } = new string?[10];
    public int ShiftOffset { get; set; }
    
    // SETLOCAL stack
    public Stack<EnvironmentSnapshot> SetLocalStack { get; } = new();
    
    // CALL nesting (ReactOS naming: prev)
    public BatchContext? prev { get; set; }
    
    // Label cache (null voor REPL → GOTO doet niks)
    public Dictionary<string, int>? LabelPositions { get; set; }
    
    // Helpers
    public bool IsReplMode => BatchFilePath == null;
    public bool IsBatchFile => BatchFilePath != null;
}

public record EnvironmentSnapshot(
    Dictionary<string, string> Variables,
    Dictionary<char, string[]> Paths,
    bool DelayedExpansion
);
```

**Tests:**
- `BatchContextTests.cs` - constructie, properties, IsReplMode

### 1.2 Expansion functies implementeren

**Bestand:** `Bat/Execution/Expander.cs`

```csharp
namespace Bat.Execution;

public static class Expander
{
    public static string ExpandBatchParameters(string line, BatchContext bc)
    {
        // Implementatie zie IMPLEMENTATION_PLAN.md Fase 3.4
        // Let op: null parameters blijven literal %1
    }
    
    public static string ExpandEnvironmentVariables(string line, IContext ctx)
    {
        // Implementatie zie IMPLEMENTATION_PLAN.md
        // Let op: niet-gevonden vars blijven literal %NOTFOUND%
    }
}
```

**Tests:**
- `ExpanderTests.cs` - alle edge cases
  - Parameter expansion (inclusief SHIFT)
  - %~dp1 modifiers
  - %* all arguments
  - Literal preservation
  - Environment variable expansion
  - Nested %VAR% in %OTHERVAR%

### 1.3 Refactor Tokenizer - STOP met expansie

**Huidige situatie:** Tokenizer expandeert %VAR% tijdens tokenizing.  
**Nieuwe situatie:** Tokenizer krijgt **al ge-expandeerde tekst**.

**Wijzigingen:**
1. Verwijder `IContext` parameter uit `Tokenizer` constructor
2. Verwijder `TokenizeVariable()` method (of maak het legacy)
3. Update `Parser` om expansie te doen VOOR tokenizing

**Bestand:** `Bat/Console/Parser.cs`

```csharp
public ParsedCommand ParseCommand()
{
    // Expand VOOR tokenizing (als BatchContext beschikbaar)
    var expandedLine = _rawInput;
    
    if (_context.CurrentBatch != null)
    {
        expandedLine = Expander.ExpandBatchParameters(expandedLine, _context.CurrentBatch);
    }
    
    expandedLine = Expander.ExpandEnvironmentVariables(expandedLine, _context);
    
    // Nu pas tokenizen
    var tokenizer = new Tokenizer();
    tokenizer.Append(expandedLine);
    
    // Rest van parsing...
}
```

**Tests:**
- Alle bestaande 133 tests moeten BLIJVEN werken
- Mogelijk test aanpassingen voor nieuwe flow

### 1.4 Typed Node Hierarchy

**Nieuwe node types:**

**Bestand:** `Bat/Nodes/BuiltInCommandNode.cs`

```csharp
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
    
    public async Task<int> ExecuteAsync(IContext ctx, BatchContext bc)
    {
        var command = new TCommand();
        return await command.ExecuteAsync(ctx, Arguments, bc, Redirections);
    }
}
```

**Bestand:** `Bat/Nodes/ExternalCommandNode.cs`

```csharp
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
    
    public async Task<int> ExecuteAsync(IContext ctx, BatchContext bc)
    {
        // Wordt geïmplementeerd in Step 5
        throw new NotImplementedException("External command execution - see Step 5");
    }
}
```

**Tests:**
- `NodeTests.cs` - constructie, GetTokens()
- Type preservation tests

### 1.5 ICommand interface

**Bestand:** `Bat/Commands/ICommand.cs`

```csharp
namespace Bat.Commands;

public interface ICommand
{
    Task<int> ExecuteAsync(
        IContext context,
        IReadOnlyList<IToken> arguments,
        BatchContext bc,  // ✅ Niet nullable - altijd valid
        IReadOnlyList<Redirection> redirections
    );
}
```

### 1.6 REPL Singleton BatchContext

**Bestand:** `Bat/Execution/ReplBatchContext.cs`

```csharp
namespace Bat.Execution;

public static class ReplBatchContext
{
    private static readonly ThreadLocal<BatchContext> _instance = new(() => new()
    {
        BatchFilePath = null,
        FileContent = "",
        Parameters = ["CMD", null, null, null, null, null, null, null, null, null],
        LabelPositions = null,  // GOTO doet niks in REPL
    });
    
    public static BatchContext Value => _instance.Value!;
    
    public static void UpdateLine(string line)
    {
        Value.FileContent = line;
    }
}
```

**Tests:**
- Singleton gedrag
- Thread isolation (verschillende threads = verschillende instances)

### 1.7 Update Context/IContext

**Bestand:** `Context/IContext.cs`

```csharp
public interface IContext
{
    char CurrentDrive { get; }
    string[] CurrentPath { get; }
    string CurrentPathDisplayName { get; }
    Dictionary<string, string> EnvironmentVariables { get; }
    int ErrorCode { get; set; }
    IFileSystem FileSystem { get; }
    
    // NIEUW:
    BatchContext? CurrentBatch { get; set; }  // Null alleen bij startup
    bool EchoEnabled { get; set; }
    bool DelayedExpansion { get; set; }  // CMD /V:ON
    bool ExtensionsEnabled { get; set; }
    string PromptFormat { get; set; }  // %PROMPT% env var
    Stack<(char Drive, string[] Path)> DirectoryStack { get; }
}
```

**Bestand:** `Bat/Context/Context.cs` - implementeer nieuwe properties

**Tests:**
- Property get/set tests
- Default values tests

## Acceptance Criteria (Definition of Done)

- [ ] BatchContext class bestaat en heeft alle velden
- [ ] ExpandBatchParameters() werkt en behoudt literals
- [ ] ExpandEnvironmentVariables() werkt en behoudt literals
- [ ] Tokenizer krijgt ge-expandeerde tekst (geen %VAR% meer)
- [ ] Parser roept expansion aan voor tokenizing
- [ ] BuiltInCommandNode<T> en ExternalCommandNode bestaan
- [ ] ICommand interface bestaat
- [ ] ReplBatchContext singleton werkt
- [ ] IContext heeft nieuwe properties
- [ ] Alle 133 bestaande tests slagen nog steeds
- [ ] Nieuwe expansion tests (10+) slagen allemaal

## Breaking Changes

**Tests die moeten worden aangepast:**

Alle tests die verwachten dat tokens **ge-expandeerde** waarden bevatten:
```csharp
// OUD (werkt niet meer):
var tokens = tokenizer.Tokenize("echo %VAR%", context);
Assert.Equal("value", tokens[1].Value);  // ❌ Verwacht expanded value

// NIEUW:
var expanded = Expander.ExpandEnvironmentVariables("echo %VAR%", context);
var tokens = tokenizer.Tokenize(expanded);
Assert.Equal("value", tokens[1].Value);  // ✅ Tokenizer krijgt expanded input
```

## Implementatie Volgorde

1. BatchContext class + tests
2. EnvironmentSnapshot record + tests
3. Expander.ExpandBatchParameters + tests (10+ cases)
4. Expander.ExpandEnvironmentVariables + tests (10+ cases)
5. ReplBatchContext singleton + tests
6. Update IContext interface
7. Update Context implementatie + tests
8. Refactor Parser om expansion toe te voegen
9. Refactor Tokenizer om %VAR% niet te expanden
10. Fix bestaande tests (verwachten nu expanded input)
11. BuiltInCommandNode<T> + tests
12. ExternalCommandNode + tests
13. ICommand interface
14. Run alle tests → moet 133+ slagen

**Geschatte tijd:** 4-6 uur (veel refactoring)

## Referenties

- **ReactOS BATCH_CONTEXT:** https://doxygen.reactos.org/d3/d0a/cmd_8h_source.html (line 40-50)
- **ReactOS SubstituteVars:** https://doxygen.reactos.org/d5/d32/batch_8c.html#a8d0
- **IMPLEMENTATION_PLAN.md:** Fase 0, Fase 0b

## Opmerking

Dit is een **grote refactor**. Neem de tijd om het goed te doen. TDD helpt om regressies te voorkomen.

**Na deze stap:** Alle fundamenten zijn gelegd voor correcte CMD compatibiliteit.
