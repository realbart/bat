# STEP 05 - Native Executables Uitvoeren

**Doel:** Process.Start voor externe programma's (notepad.exe, cmd.exe, git.exe, etc.)

## Context

Als een command niet built-in is (ECHO, DIR, CD), moet Bat het uitvoeren als **externe executable**:

1. Zoek programma in PATH
2. Vertaal virtuele paths (Z:\) naar native paths (C:\)
3. Start als subprocess via Process.Start
4. Capture output/error
5. Wacht op completion
6. Return exit code

### ReactOS Implementatie

ReactOS CMD heeft `Execute()` functie in `cmd.c`:
- PATH lookup via `SearchForExecutable()`
- Process spawn via `CreateProcess()`

## Test-First Aanpak

### Test File: `ExternalCommandTests.cs`

**Test Setup met Mock Process:**
```csharp
public class ExternalCommandTests
{
    // Interface voor testbare process execution
    public interface IProcessRunner
    {
        Task<ProcessResult> RunAsync(string fileName, string arguments, string workingDir);
    }
    
    public record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
    
    // Mock voor tests
    private class MockProcessRunner : IProcessRunner
    {
        private readonly Dictionary<string, ProcessResult> _responses = new();
        
        public void Setup(string fileName, ProcessResult result)
        {
            _responses[fileName.ToLower()] = result;
        }
        
        public Task<ProcessResult> RunAsync(string fileName, string arguments, string workingDir)
        {
            var key = Path.GetFileName(fileName).ToLower();
            return Task.FromResult(_responses.TryGetValue(key, out var result) 
                ? result 
                : new ProcessResult(0, "", ""));
        }
    }
}
```

**Test 1: Execute simple command**
```csharp
[Fact]
public async Task Execute_SimpleCommand_RunsProcess()
{
    // Arrange
    var ctx = CreateContext();
    var mockRunner = new MockProcessRunner();
    mockRunner.Setup("notepad.exe", new ProcessResult(0, "", ""));
    
    var executor = new ExternalCommandExecutor(mockRunner);
    var node = new ExternalCommandNode(
        Token.Command("notepad"),
        [Token.Text("test.txt")],
        []
    );
    
    // Act
    var exitCode = await executor.ExecuteAsync(node, ctx, ctx.CurrentBatch);
    
    // Assert
    Assert.Equal(0, exitCode);
    // Verify mockRunner was called with correct args
}
```

**Test 2: PATH lookup**
```csharp
[Fact]
public async Task Execute_PathLookup_FindsExecutable()
{
    // Arrange
    var ctx = CreateContext();
    ctx.EnvironmentVariables["PATH"] = @"Z:\Windows\System32;Z:\Windows";
    
    // Create mock executables in virtual filesystem
    CreateFile(ctx.FileSystem, 'Z', ["Windows", "System32", "cmd.exe"], "");
    
    var mockRunner = new MockProcessRunner();
    mockRunner.Setup("cmd.exe", new ProcessResult(0, "Hello", ""));
    
    var executor = new ExternalCommandExecutor(mockRunner);
    var node = new ExternalCommandNode(Token.Command("cmd"), [], []);
    
    // Act
    var exitCode = await executor.ExecuteAsync(node, ctx, ctx.CurrentBatch);
    
    // Assert
    Assert.Equal(0, exitCode);
    // Verify cmd.exe was found in PATH
}
```

**Test 3: Exit code propagatie**
```csharp
[Fact]
public async Task Execute_NonZeroExit_PropagatesExitCode()
{
    // Arrange
    var ctx = CreateContext();
    var mockRunner = new MockProcessRunner();
    mockRunner.Setup("fail.exe", new ProcessResult(42, "", "Error occurred"));
    
    var executor = new ExternalCommandExecutor(mockRunner);
    var node = new ExternalCommandNode(Token.Command("fail.exe"), [], []);
    
    // Act
    var exitCode = await executor.ExecuteAsync(node, ctx, ctx.CurrentBatch);
    
    // Assert
    Assert.Equal(42, exitCode);
    Assert.Equal(42, ctx.ErrorCode);
}
```

**Test 4: Working directory is native path**
```csharp
[Fact]
public async Task Execute_WorkingDirectory_IsNativePath()
{
    // Arrange
    var ctx = CreateContext(drive: 'Z', path: ["Users", "Bart"]);
    
    var capturedWorkingDir = "";
    var mockRunner = new MockProcessRunner();
    // Mock captures working directory
    
    var executor = new ExternalCommandExecutor(mockRunner);
    var node = new ExternalCommandNode(Token.Command("test.exe"), [], []);
    
    // Act
    await executor.ExecuteAsync(node, ctx, ctx.CurrentBatch);
    
    // Assert
    Assert.Equal(@"C:\Users\Bart", capturedWorkingDir);  // Z: → C:
}
```

**Test 5: Stdout redirection**
```csharp
[Fact]
public async Task Execute_StdoutRedirect_WritesToFile()
{
    // Arrange
    var ctx = CreateContext(drive: 'Z', path: []);
    var mockRunner = new MockProcessRunner();
    mockRunner.Setup("echo.exe", new ProcessResult(0, "Hello World", ""));
    
    var executor = new ExternalCommandExecutor(mockRunner);
    var redirects = new[]
    {
        new Redirection(Token.StdOutRedirection(">"), [Token.Text("output.txt")])
    };
    var node = new ExternalCommandNode(Token.Command("echo.exe"), [], redirects);
    
    // Act
    await executor.ExecuteAsync(node, ctx, ctx.CurrentBatch);
    
    // Assert
    var content = ctx.FileSystem.ReadAllText('Z', ["output.txt"]);
    Assert.Equal("Hello World", content);
}
```

**Test 6: Command not found**
```csharp
[Fact]
public async Task Execute_NotFound_ReturnsError()
{
    // Arrange
    var ctx = CreateContext();
    ctx.EnvironmentVariables["PATH"] = @"Z:\Windows";
    
    var executor = new ExternalCommandExecutor(new MockProcessRunner());
    var node = new ExternalCommandNode(Token.Command("notfound.exe"), [], []);
    
    var errorOutput = new StringWriter();
    Console.SetError(errorOutput);
    
    // Act
    var exitCode = await executor.ExecuteAsync(node, ctx, ctx.CurrentBatch);
    
    // Assert
    Assert.NotEqual(0, exitCode);
    Assert.Contains("not found", errorOutput.ToString().ToLower());
}
```

## Implementatie Stappen

### 5.1 IProcessRunner interface

**Bestand:** `Bat/Execution/IProcessRunner.cs`

```csharp
namespace Bat.Execution;

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessStartInfo startInfo);
}

public record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
```

### 5.2 RealProcessRunner implementatie

**Bestand:** `Bat/Execution/RealProcessRunner.cs`

```csharp
public class RealProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessStartInfo startInfo)
    {
        using var process = new Process { StartInfo = startInfo };
        
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        
        process.OutputDataReceived += (s, e) => 
        {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (s, e) => 
        {
            if (e.Data != null) errorBuilder.AppendLine(e.Data);
        };
        
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        await process.WaitForExitAsync();
        
        return new ProcessResult(
            process.ExitCode,
            outputBuilder.ToString(),
            errorBuilder.ToString()
        );
    }
}
```

### 5.3 ExternalCommandExecutor

**Bestand:** `Bat/Execution/ExternalCommandExecutor.cs`

```csharp
public class ExternalCommandExecutor
{
    private readonly IProcessRunner _processRunner;
    
    public ExternalCommandExecutor(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }
    
    public async Task<int> ExecuteAsync(ExternalCommandNode node, 
                                        IContext ctx, BatchContext bc)
    {
        // 1. PATH lookup
        var executablePath = FindInPath(node.CommandToken.Value, ctx);
        
        if (executablePath == null)
        {
            await Console.Error.WriteLineAsync(
                $"'{node.CommandToken.Value}' is not recognized as an internal or external command.");
            return 1;
        }
        
        // 2. Build arguments
        var arguments = string.Join(" ", node.Arguments.Select(t => QuoteIfNeeded(t.ToString())));
        
        // 3. Convert working directory to native path
        var workingDir = ctx.FileSystem.GetNativePath(ctx.CurrentDrive, ctx.CurrentPath);
        
        // 4. Setup ProcessStartInfo
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };
        
        // 5. Apply redirections (TODO: Step 5.5)
        ApplyRedirections(startInfo, node.Redirections, ctx);
        
        // 6. Execute
        var result = await _processRunner.RunAsync(startInfo);
        
        // 7. Write output (als niet redirected)
        if (!node.Redirections.Any(r => r.IsStdOut))
        {
            await Console.Out.WriteAsync(result.StandardOutput);
            await Console.Error.WriteAsync(result.StandardError);
        }
        
        return result.ExitCode;
    }
    
    private string? FindInPath(string command, IContext ctx)
    {
        // Als absoluut pad of relatief pad met \ : direct proberen
        if (command.Contains('\\') || command.Contains(':'))
        {
            // Parse as path
            var (drive, path) = ParsePath(command, ctx);
            var nativePath = ctx.FileSystem.GetNativePath(drive, path);
            return File.Exists(nativePath) ? nativePath : null;
        }
        
        // Add .exe/.com/.bat extensions als geen extensie
        var extensions = Path.HasExtension(command) 
            ? new[] { "" }
            : new[] { ".exe", ".com", ".bat", ".cmd" };
        
        // Zoek in current directory eerst
        foreach (var ext in extensions)
        {
            var fileName = command + ext;
            var nativePath = ctx.FileSystem.GetNativePath(ctx.CurrentDrive, 
                [.. ctx.CurrentPath, fileName]);
            
            if (File.Exists(nativePath))
                return nativePath;
        }
        
        // Zoek in PATH
        var pathVar = ctx.EnvironmentVariables.TryGetValue("PATH", out var p) ? p : "";
        foreach (var pathDir in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            // Parse pathDir als virtual path
            var (drive, dirPath) = ParsePath(pathDir, ctx);
            
            foreach (var ext in extensions)
            {
                var fileName = command + ext;
                var fullPath = [.. dirPath, fileName];
                var nativePath = ctx.FileSystem.GetNativePath(drive, fullPath);
                
                if (File.Exists(nativePath))
                    return nativePath;
            }
        }
        
        return null;  // Not found
    }
}
```

### 5.4 Integratie in ExternalCommandNode

**Bestand:** `Bat/Nodes/ExternalCommandNode.cs`

Update `ExecuteAsync`:
```csharp
public async Task<int> ExecuteAsync(IContext ctx, BatchContext bc)
{
    // Use executor (wordt geïnjecteerd via Dispatcher)
    var executor = new ExternalCommandExecutor(new RealProcessRunner());
    return await executor.ExecuteAsync(this, ctx, bc);
}
```

### 5.5 Redirections implementeren

**Later:** Dit wordt uitgebreid in een volgende stap. Voor nu: basis redirection.

```csharp
private void ApplyRedirections(ProcessStartInfo startInfo, 
                               IReadOnlyList<Redirection> redirects, IContext ctx)
{
    foreach (var redir in redirects)
    {
        // > output.txt
        if (redir.Token is StdOutRedirectionToken)
        {
            var targetFile = redir.Target[0].ToString();
            var (drive, path) = ParsePath(targetFile, ctx);
            var nativePath = ctx.FileSystem.GetNativePath(drive, path);
            
            // TODO: Schrijf output naar bestand na process completion
        }
    }
}
```

## Acceptance Criteria

- [ ] PATH lookup werkt (zoekt in directories)
- [ ] Extension toevoegen (.exe, .com, .bat) werkt
- [ ] Working directory is native pad (Z:\ → C:\)
- [ ] Process execution werkt
- [ ] Exit code wordt correct doorgegeven
- [ ] Stdout/stderr worden getoond
- [ ] Command not found geeft foutmelding
- [ ] 6+ unit tests slagen
- [ ] Manual test: `notepad test.txt` werkt

## Manual Testing

**Vereist:** Windows machine met System32 in PATH

```sh
Z:\> notepad test.txt
(Notepad opent - blocking tot je sluit)

Z:\> cmd /c echo Hello World
Hello World

Z:\> cmd /c exit 42
Z:\> echo %ERRORLEVEL%
42

Z:\> notfound.exe
'notfound.exe' is not recognized as an internal or external command.
```

## Geschatte Tijd

2-3 uur (PATH lookup is complex, maar testbaar via mocks)

## Referenties

- **ReactOS Execute:** https://doxygen.reactos.org/db/d4f/base_2shell_2cmd_2cmd_8c_source.html#a500
- **ReactOS SearchForExecutable:** https://doxygen.reactos.org/db/d4f/base_2shell_2cmd_2cmd_8c_source.html#a650
- **Process.Start docs:** https://learn.microsoft.com/dotnet/api/system.diagnostics.process.start
- **ProcessStartInfo:** https://learn.microsoft.com/dotnet/api/system.diagnostics.processstartinfo

## Opmerkingen

**PATH extensions:**
CMD zoekt in deze volgorde: `.exe`, `.com`, `.bat`, `.cmd`

**PATHEXT variable:**
In echte CMD wordt `%PATHEXT%` gebruikt. Voor nu: hardcode `.exe;.com;.bat;.cmd`.

**Security:**
Geen shell execute (UseShellExecute = false) om te voorkomen dat Bat executable injection krijgt.
