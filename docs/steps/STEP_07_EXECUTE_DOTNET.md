# STEP 06 - .NET Library Executables (Doskey, XCopy, Subst)

**Doel:** Assembly.LoadFrom voor .NET programs met `Main(IContext, params string[])` library interface.

## Context

Sommige externe programma's (Doskey, XCopy, Subst) zijn **.NET assemblies** met een speciale entry point:

```csharp
// Normale standalone executable:
public static int Main()
{
    Console.WriteLine("This application needs Bat to run");
    return 1;
}

// Library interface (voor Bat):
public static Task<int> Main(IContext context, params string[] args)
{
    // Krijgt volledige context - ziet virtuele drives!
    return Task.FromResult(0);
}
```

**Voordelen:**
- ✅ Shared context (geen Process.Start overhead)
- ✅ Virtuele drives worden doorgegeven
- ✅ Environment variables zijn shared
- ✅ Sneller (geen process spawn)

**Fallback:** Als geen library interface → Process.Start (Step 5)

## Test-First Aanpak

### Test Assembly Setup

**Bestand:** `Bat.TestExecutables/TestLibraryCommand.cs`

```csharp
namespace Bat.TestExecutables;

using Context;

public static class TestLibraryCommand
{
    // Standalone entry point
    public static int Main()
    {
        Console.WriteLine("ERROR: Must be called through Bat");
        return 1;
    }
    
    // Library interface
    public static Task<int> Main(IContext context, params string[] args)
    {
        // Schrijf context info naar stdout (voor test verificatie)
        Console.WriteLine($"CurrentDrive: {context.CurrentDrive}");
        Console.WriteLine($"CurrentPath: {string.Join("/", context.CurrentPath)}");
        Console.WriteLine($"Args: {string.Join(" ", args)}");
        
        // Check dat we virtuele drive zien
        if (context.CurrentDrive == 'Z')
            Console.WriteLine("VirtualDrive: OK");
        
        return Task.FromResult(0);
    }
}
```

Build als executable: `Bat.TestExecutables.dll`

### Test File: `DotNetExecutableTests.cs`

**Test 1: Detect library interface**
```csharp
[Fact]
public void DetectLibraryInterface_FindsMethod()
{
    // Arrange
    var assemblyPath = GetTestAssemblyPath("Bat.TestExecutables.dll");
    var detector = new DotNetExecutableDetector();
    
    // Act
    var hasInterface = detector.HasLibraryInterface(assemblyPath);
    
    // Assert
    Assert.True(hasInterface);
}
```

**Test 2: Execute via library interface**
```csharp
[Fact]
public async Task Execute_LibraryInterface_SharesContext()
{
    // Arrange
    var ctx = CreateContext(drive: 'Z', path: ["Users"]);
    var assemblyPath = GetTestAssemblyPath("Bat.TestExecutables.dll");
    
    var executor = new DotNetExecutableExecutor();
    
    var output = new StringWriter();
    Console.SetOut(output);
    
    // Act
    var exitCode = await executor.ExecuteAsync(
        assemblyPath, 
        new[] { "arg1", "arg2" },
        ctx
    );
    
    // Assert
    Assert.Equal(0, exitCode);
    var result = output.ToString();
    Assert.Contains("CurrentDrive: Z", result);
    Assert.Contains("CurrentPath: Users", result);
    Assert.Contains("Args: arg1 arg2", result);
    Assert.Contains("VirtualDrive: OK", result);  // Ziet Z:!
}
```

**Test 3: Fallback naar Process.Start**
```csharp
[Fact]
public async Task Execute_NoLibraryInterface_FallsBackToProcess()
{
    // Arrange
    var ctx = CreateContext();
    
    // Reguliere .NET executable ZONDER IContext Main
    var assemblyPath = GetTestAssemblyPath("RegularApp.dll");
    
    var executor = new DotNetExecutableExecutor();
    var mockProcessRunner = new MockProcessRunner();
    mockProcessRunner.Setup("dotnet", new ProcessResult(0, "Output", ""));
    
    // Act
    var exitCode = await executor.ExecuteAsync(
        assemblyPath,
        [],
        ctx,
        fallbackRunner: mockProcessRunner
    );
    
    // Assert
    Assert.Equal(0, exitCode);
    // Verify Process.Start werd gebruikt (via mock)
}
```

**Test 4: Test met echte Doskey.dll**
```csharp
[Fact]
public async Task Execute_Doskey_Works()
{
    // Arrange
    var ctx = CreateContext(drive: 'Z', path: []);
    
    // Find Doskey.dll in solution
    var doskeyPath = FindProjectOutput("Doskey");
    
    var executor = new DotNetExecutableExecutor();
    
    var output = new StringWriter();
    Console.SetOut(output);
    
    // Act
    var exitCode = await executor.ExecuteAsync(doskeyPath, [], ctx);
    
    // Assert
    Assert.Equal(0, exitCode);
    Assert.Contains("Doskey Main (through", output.ToString());
}
```

**Test 5: Test met XCopy.dll**
```csharp
[Fact]
public async Task Execute_XCopy_SeesVirtualDrives()
{
    // Arrange
    var ctx = CreateContext(drive: 'Z', path: []);
    CreateFile(ctx.FileSystem, 'Z', ["source.txt"], "content");
    
    var xcopyPath = FindProjectOutput("XCopy");
    var executor = new DotNetExecutableExecutor();
    
    // Act
    var exitCode = await executor.ExecuteAsync(
        xcopyPath,
        new[] { @"Z:\source.txt", @"Z:\dest.txt" },
        ctx
    );
    
    // Assert
    Assert.Equal(0, exitCode);
    Assert.True(ctx.FileSystem.FileExists('Z', ["dest.txt"]));  // XCopy via IContext!
}
```

## Implementatie Stappen

### 6.1 DotNetExecutableDetector

**Bestand:** `Bat/Execution/DotNetExecutableDetector.cs`

```csharp
public class DotNetExecutableDetector
{
    public bool HasLibraryInterface(string assemblyPath)
    {
        try
        {
            var assembly = Assembly.LoadFrom(assemblyPath);
            return FindLibraryEntryPoint(assembly) != null;
        }
        catch
        {
            return false;
        }
    }
    
    public MethodInfo? FindLibraryEntryPoint(Assembly assembly)
    {
        // Zoek: public static [Task<]int[>] Main(IContext, params string[])
        foreach (var type in assembly.GetTypes())
        {
            var method = type.GetMethod("Main", 
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(IContext), typeof(string[]) },
                null
            );
            
            if (method != null)
            {
                var returnType = method.ReturnType;
                if (returnType == typeof(int) || 
                    returnType == typeof(Task<int>))
                {
                    return method;
                }
            }
        }
        
        return null;
    }
}
```

### 6.2 DotNetExecutableExecutor

**Bestand:** `Bat/Execution/DotNetExecutableExecutor.cs`

```csharp
public class DotNetExecutableExecutor
{
    private readonly DotNetExecutableDetector _detector = new();
    
    public async Task<int> ExecuteAsync(
        string assemblyPath,
        string[] args,
        IContext ctx,
        IProcessRunner? fallbackRunner = null)
    {
        // 1. Probeer laden als assembly
        Assembly assembly;
        try
        {
            assembly = Assembly.LoadFrom(assemblyPath);
        }
        catch
        {
            // Geen .NET assembly → fallback
            if (fallbackRunner != null)
                return await FallbackToProcess(assemblyPath, args, ctx, fallbackRunner);

            return 1;  // Laden mislukt en geen fallback → error exit code
        }
        
        // 2. Zoek library entry point
        var entryPoint = _detector.FindLibraryEntryPoint(assembly);

        if (entryPoint == null)
        {
            // Geen library interface → fallback
            if (fallbackRunner != null)
                return await FallbackToProcess(assemblyPath, args, ctx, fallbackRunner);

            return 1;  // Geen entry point en geen fallback → error exit code
        }

        // 3. Invoke met IContext
        var result = entryPoint.Invoke(null, new object[] { ctx, args });

        // 4. Handle Task<int> of int
        if (result is Task<int> task)
            return await task;
        else if (result is int exitCode)
            return exitCode;
        else
            return 1;  // Onverwacht return type → error exit code
    }
    
    private async Task<int> FallbackToProcess(string assemblyPath, string[] args, 
                                               IContext ctx, IProcessRunner runner)
    {
        // dotnet exec {assembly} {args}
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"exec \"{assemblyPath}\" {string.Join(" ", args)}",
            WorkingDirectory = ctx.FileSystem.GetNativePath(ctx.CurrentDrive, ctx.CurrentPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        
        var result = await runner.RunAsync(startInfo);
        
        await Console.Out.WriteAsync(result.StandardOutput);
        await Console.Error.WriteAsync(result.StandardError);
        
        return result.ExitCode;
    }
}
```

### 6.3 Update ExternalCommandExecutor

**Bestand:** `Bat/Execution/ExternalCommandExecutor.cs`

Voeg .NET detection toe:
```csharp
public async Task<int> ExecuteAsync(ExternalCommandNode node, IContext ctx, BatchContext bc)
{
    // 1. PATH lookup
    var executablePath = FindInPath(node.CommandToken.Value, ctx);
    
    if (executablePath == null)
    {
        await Console.Error.WriteLineAsync($"Command not found: {node.CommandToken.Value}");
        return 1;
    }
    
    // 2. Check if .NET executable
    if (Path.GetExtension(executablePath).Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(executablePath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
    {
        var dotnetExecutor = new DotNetExecutableExecutor();
        var args = node.Arguments.Select(t => t.ToString()).ToArray();
        
        try
        {
            // Probeer library interface
            return await dotnetExecutor.ExecuteAsync(executablePath, args, ctx, _processRunner);
        }
        catch (BadImageFormatException)
        {
            // Geen .NET assembly → fallback naar native
        }
    }
    
    // 3. Fallback: native executable via Process.Start
    return await ExecuteNative(executablePath, node, ctx, bc);
}
```

## Integration Test

**Test 6: End-to-end met Doskey**
```csharp
[Fact]
public async Task Integration_Doskey_ExecutesViaLibrary()
{
    // Arrange
    var ctx = CreateDosContext(drive: 'Z', path: []);
    var dispatcher = new Dispatcher();
    
    // Build command: "doskey"
    var parser = new Parser(ctx);
    parser.Append("doskey test=echo Hello");
    var command = parser.ParseCommand();
    
    var output = new StringWriter();
    Console.SetOut(output);
    
    // Act
    var exitCode = await dispatcher.Execute(command, ctx, ctx.CurrentBatch);
    
    // Assert
    Assert.Equal(0, exitCode);
    var result = output.ToString();
    Assert.Contains("Doskey Main", result);
    Assert.Contains("context", result.ToLower());
}
```

## Acceptance Criteria

- [ ] DotNetExecutableDetector vindt library entry point
- [ ] ExecuteAsync roept library Main aan met IContext
- [ ] Context wordt correct doorgegeven (drive, path, env vars)
- [ ] Doskey.dll werkt via library interface
- [ ] XCopy.dll werkt via library interface
- [ ] Subst.dll werkt via library interface
- [ ] Fallback naar Process.Start werkt (reguliere .NET apps)
- [ ] 5+ unit tests slagen
- [ ] Integration test met Doskey slaagt

## Manual Testing

Build solution en test:
```sh
Z:\> doskey
Doskey Main (through Bat.Context.DosContext)

Z:\> doskey /h
(Doskey help, via library interface)

Z:\> subst
(Toont huidige subst mappings - none)
```

**Verifieer dat IContext wordt doorgegeven:**
- Doskey ziet Z: als current drive
- XCopy kan virtuele paths gebruiken
- Subst manipuleert FileSystem.Substs

## Geschatte Tijd

2-3 uur (reflection is tricky, maar goed testbaar)

## Referenties

- **Assembly.LoadFrom:** https://learn.microsoft.com/dotnet/api/system.reflection.assembly.loadfrom
- **MethodInfo.Invoke:** https://learn.microsoft.com/dotnet/api/system.reflection.methodbase.invoke
- **Doskey source:** `Doskey/Program.cs` (al aanwezig in solution)
- **IMPLEMENTATION_PLAN.md:** Fase 1.3 (Externe .NET-programma's)

## Uitdagingen

**Async detection:**
Entry point kan zijn:
- `int Main(IContext, params string[])`
- `Task<int> Main(IContext, params string[])`

Beide moeten worden ondersteund.

**Assembly loading:**
- Assembly.LoadFrom kan falen (BadImageFormatException) → fallback naar Process.Start
- Dependency resolution kan falen
- Graceful fallback naar Process.Start

## Bonus: Test met alle drie

**Test 7: Integration met Doskey, XCopy, Subst**
```csharp
[Theory]
[InlineData("doskey", "Doskey Main")]
[InlineData("xcopy", "XCopy")]  // Moet output aanpassen
[InlineData("subst", "Subst")]  // Moet output aanpassen
public async Task Execute_AllLibraryCommands_Work(string command, string expectedOutput)
{
    // Arrange
    var ctx = CreateDosContext();
    var dispatcher = new Dispatcher();
    
    var parser = new Parser(ctx);
    parser.Append(command);
    var node = parser.ParseCommand();
    
    var output = new StringWriter();
    Console.SetOut(output);
    
    // Act
    var exitCode = await dispatcher.Execute(node, ctx, ctx.CurrentBatch);
    
    // Assert
    Assert.Equal(0, exitCode);
    Assert.Contains(expectedOutput, output.ToString());
}
```

Dit verifieert dat **alle drie** library executables werken!
