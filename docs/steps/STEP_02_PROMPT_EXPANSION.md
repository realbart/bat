# STEP 02 - PROMPT Environment Variabele Expansie

**Doel:** Implementeer correcte command prompt generatie via %PROMPT% environment variable.

## Context

CMD gebruikt de **PROMPT environment variabele** om de prompt te bepamen. Default waarde: `$P$G`.

**Voorbeeld:**
```sh
C:\Users\Bart> echo %PROMPT%
$P$G

C:\Users\Bart> set PROMPT=$N$G
C:>

C:\Users\Bart> set PROMPT=$P$_$G
C:\Users\Bart
>
```

### Hoe werkt het?

1. REPL leest `%PROMPT%` environment variable
2. Expand alle `$X` codes naar concrete waarden
3. Toon als command prompt

### ReactOS implementatie

ReactOS CMD heeft `PrintPrompt()` functie in `prompt.c`:
- https://doxygen.reactos.org/d0/d07/prompt_8c_source.html

## PROMPT Codes (Volledige lijst)

| Code | Betekenis | Voorbeeld output |
|---|---|---|
| `$A` | & (ampersand) | `&` |
| `$B` | \| (pipe character) | `\|` |
| `$C` | ( (open haakje) | `(` |
| `$D` | Huidige datum | `Mon 01/15/2024` |
| `$E` | Escape code (ASCII 27) | ESC character |
| `$F` | ) (sluit haakje) | `)` |
| `$G` | > (greater-than sign) | `>` |
| `$H` | Backspace (wis vorig char) | (destructief) |
| `$L` | < (less-than sign) | `<` |
| `$M` | Remote name van current drive | (of empty) |
| `$N` | Current drive letter | `C:` |
| `$P` | Current drive and path | `C:\Users\Bart` |
| `$Q` | = (equals sign) | `=` |
| `$S` | Spatie | ` ` |
| `$T` | Current time | `14:30:45.12` |
| `$V` | Windows version | `Microsoft Windows [Version 10.0.19045.0]` |
| `$_` | CRLF (newline) | (nieuwe regel) |
| `$$` | $ (dollar sign) | `$` |
| `$+` | PUSHD depth (aantal +'s) | `++` (bij depth 2) |

**Niet-standaard maar nuttig:**
- Onbekende codes → letterlijk (bijv. `$X` → `$X`)

## Test-First Aanpak

### Test File: `PromptExpanderTests.cs`

**Test 1: Default prompt $P$G**
```csharp
[Fact]
public void ExpandPrompt_DefaultPG_ShowsPathAndGT()
{
    // Arrange
    var ctx = CreateContext(drive: 'C', path: ["Users", "Bart"]);
    ctx.EnvironmentVariables["PROMPT"] = "$P$G";
    
    // Act
    var prompt = PromptExpander.Expand(ctx);
    
    // Assert
    Assert.Equal("C:\\Users\\Bart>", prompt);
}
```

**Test 2: Multi-line prompt met $_**
```csharp
[Fact]
public void ExpandPrompt_Multiline_Works()
{
    // Arrange
    var ctx = CreateContext(drive: 'C', path: ["Users"]);
    ctx.EnvironmentVariables["PROMPT"] = "$P$_$G";
    
    // Act
    var prompt = PromptExpander.Expand(ctx);
    
    // Assert
    Assert.Equal("C:\\Users\r\n>", prompt);
}
```

**Test 3: Drive only ($N$G)**
```csharp
[Fact]
public void ExpandPrompt_DriveOnly()
{
    // Arrange
    var ctx = CreateContext(drive: 'D', path: ["Projects"]);
    ctx.EnvironmentVariables["PROMPT"] = "$N$G";
    
    // Act
    var prompt = PromptExpander.Expand(ctx);
    
    // Assert
    Assert.Equal("D:>", prompt);
}
```

**Test 4: Datum en tijd**
```csharp
[Fact]
public void ExpandPrompt_DateTime_Works()
{
    // Arrange
    var ctx = CreateContext();
    ctx.EnvironmentVariables["PROMPT"] = "$D $T";
    
    // Act
    var prompt = PromptExpander.Expand(ctx);
    
    // Assert
    Assert.Matches(@"^\w+ \d{2}/\d{2}/\d{4} \d{2}:\d{2}:\d{2}\.\d{2}$", prompt);
}
```

**Test 5: PUSHD depth ($+)**
```csharp
[Fact]
public void ExpandPrompt_PushDepth_ShowsPlusses()
{
    // Arrange
    var ctx = CreateContext();
    ctx.DirectoryStack.Push(('C', new[] { "Temp" }));
    ctx.DirectoryStack.Push(('C', new[] { "Windows" }));
    ctx.EnvironmentVariables["PROMPT"] = "$+$G";
    
    // Act
    var prompt = PromptExpander.Expand(ctx);
    
    // Assert
    Assert.Equal("++>", prompt);  // 2 pushes = ++
}
```

**Test 6: Escaped dollar ($$)**
```csharp
[Fact]
public void ExpandPrompt_EscapedDollar()
{
    // Arrange
    var ctx = CreateContext();
    ctx.EnvironmentVariables["PROMPT"] = "Cost: $$5$G";
    
    // Act
    var prompt = PromptExpander.Expand(ctx);
    
    // Assert
    Assert.Equal("Cost: $5>", prompt);
}
```

**Test 7: Onbekende codes blijven literal**
```csharp
[Fact]
public void ExpandPrompt_UnknownCode_RemainsLiteral()
{
    // Arrange
    var ctx = CreateContext();
    ctx.EnvironmentVariables["PROMPT"] = "$P$X$G";  // $X bestaat niet
    
    // Act
    var prompt = PromptExpander.Expand(ctx);
    
    // Assert
    Assert.Equal("C:\\>$X>", prompt);  // $X blijft literal
}
```

**Test 8: Geen PROMPT variable → gebruik default**
```csharp
[Fact]
public void ExpandPrompt_NoVariable_UsesDefault()
{
    // Arrange
    var ctx = CreateContext(drive: 'C', path: []);
    // Geen PROMPT variable gezet
    
    // Act
    var prompt = PromptExpander.Expand(ctx);
    
    // Assert
    Assert.Equal("C:\\>", prompt);  // Default $P$G
}
```

## Implementatie Stappen

### 2.1 PromptExpander class creëren

**Bestand:** `Bat/Execution/PromptExpander.cs`

```csharp
namespace Bat.Execution;

/// <summary>
/// Expands prompt format codes - analogous to ReactOS PrintPrompt()
/// </summary>
public static class PromptExpander
{
    private const string DefaultPrompt = "$P$G";
    
    public static string Expand(IContext context)
    {
        // Lees PROMPT environment variable (of default)
        var format = context.EnvironmentVariables.TryGetValue("PROMPT", out var val) 
            ? val 
            : DefaultPrompt;
        
        return ExpandPromptCodes(format, context);
    }
    
    private static string ExpandPromptCodes(string format, IContext ctx)
    {
        var result = new StringBuilder(format.Length * 2);
        
        for (int i = 0; i < format.Length; i++)
        {
            if (format[i] == '$' && i + 1 < format.Length)
            {
                var code = char.ToUpper(format[i + 1]);
                
                switch (code)
                {
                    case 'A': result.Append('&'); break;
                    case 'B': result.Append('|'); break;
                    case 'C': result.Append('('); break;
                    case 'D': result.Append(DateTime.Now.ToString("ddd MM/dd/yyyy")); break;
                    case 'E': result.Append((char)27); break;  // ESC
                    case 'F': result.Append(')'); break;
                    case 'G': result.Append('>'); break;
                    case 'H': result.Append('\b'); break;  // Backspace
                    case 'L': result.Append('<'); break;
                    case 'M': result.Append(GetRemoteName(ctx)); break;
                    case 'N': result.Append($"{ctx.CurrentDrive}:"); break;
                    case 'P': result.Append(ctx.CurrentPathDisplayName); break;
                    case 'Q': result.Append('='); break;
                    case 'S': result.Append(' '); break;
                    case 'T': result.Append(DateTime.Now.ToString("HH:mm:ss.ff")); break;
                    case 'V': result.Append("Microsoft Windows [Version 10.0.0]"); break;
                    case '_': result.Append(Environment.NewLine); break;
                    case '$': result.Append('$'); break;
                    case '+': result.Append(new string('+', ctx.DirectoryStack.Count)); break;
                    
                    default:
                        // Onbekende code → blijft literal
                        result.Append('$');
                        result.Append(format[i + 1]);
                        break;
                }
                
                i++;  // Skip next char
            }
            else
            {
                result.Append(format[i]);
            }
        }
        
        return result.ToString();
    }
    
    private static string GetRemoteName(IContext ctx)
    {
        // TODO: Implementeer remote name lookup (voor network drives)
        return string.Empty;
    }
}
```

### 2.2 Integreer in REPL

**Bestand:** `Bat/Console/Repl.cs`

Update `GetCommandAsync`:
```csharp
public async Task<ParsedCommand> GetCommandAsync(IContext context)
{
    do
    {
        var parser = new Parser(context);
        
        // Gebruik PromptExpander i.p.v. hardcoded prompt
        var prompt = PromptExpander.Expand(context);
        await console.Out.WriteAsync(prompt);
        
        parser.Append(await ReadLine(context));
        
        // Rest blijft hetzelfde...
    } while (true);
}
```

### 2.3 Initialiseer PROMPT in Context

**Bestand:** `Bat/Context/DosContext.cs` (of waar je context initialiseert)

```csharp
public DosContext(IFileSystem fileSystem) : base(fileSystem)
{
    // Initialiseer default environment variables
    EnvironmentVariables["PROMPT"] = "$P$G";  // Default CMD prompt
    EnvironmentVariables["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? "";
    // etc.
}
```

## Acceptance Criteria

- [ ] PromptExpander.Expand() bestaat
- [ ] Alle 17 prompt codes werken
- [ ] Default prompt is `$P$G` → `C:\>`
- [ ] `set PROMPT=$N$G` in REPL → prompt verandert naar `C:>`
- [ ] Multi-line prompt met `$_` werkt
- [ ] PUSHD depth `$+` toont correct aantal +'s
- [ ] Onbekende codes blijven literal
- [ ] 8+ unit tests slagen
- [ ] REPL toont correcte prompt (manual test)

## Manual Testing

Start Bat en test:
```sh
Z:\> set PROMPT=$N$G
Z:>

Z:> set PROMPT=$P$_$T $G
Z:\
14:30:45.12 >

Z:> set PROMPT=[$P] $G
[Z:\] >
```

## Geschatte Tijd

1-2 uur (relatief eenvoudig, maar veel test cases)

## Referenties

- **ReactOS PrintPrompt:** https://doxygen.reactos.org/d0/d07/prompt_8c_source.html
- **Microsoft PROMPT docs:** https://learn.microsoft.com/windows-server/administration/windows-commands/prompt
- **IMPLEMENTATION_PLAN.md:** Fase 0 (PROMPT expansie sectie)
