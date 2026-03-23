# STEP 03 - DosFileSystem Implementeren

**Doel:** Volledig werkend filesystem voor Windows met **C: → Z:** mapping (om te tonen dat virtuele drives werken).

## Context

IFileSystem is de abstractielaag voor alle bestandssysteem-operaties. De DOS implementatie delegeert naar `System.IO` maar mapt drives:
- **C: → Z:** (om virtueel filesystem zichtbaar te maken)
- Verder worden er geen mappings aangemaakt
- Verander de huidige drive in de context naar Z

Dit maakt het duidelijk dat Bat **geen native pad-namen** gebruikt maar alles via virtuele drives gaat.

## Test-First Aanpak

### Test File: `DosFileSystemTests.cs`

**Test Setup:**
```csharp
public class DosFileSystemTests : IDisposable
{
    private readonly string _testRoot;
    private readonly DosFileSystem _fs;
    
    public DosFileSystemTests()
    {
        // Creëer temp directory voor tests
        _testRoot = Path.Combine(Path.GetTempPath(), $"BatTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
        
        // DosFileSystem met C: → Z: mapping (testroot)
        _fs = new DosFileSystem(new Dictionary<char, string>
        {
            ['Z'] = _testRoot  // C: wordt gemapped naar test directory
        });
    }
    
    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }
}
```

**Test 1: FileExists**
```csharp
[Fact]
public void FileExists_ExistingFile_ReturnsTrue()
{
    // Arrange
    var nativePath = Path.Combine(_testRoot, "test.txt");
    File.WriteAllText(nativePath, "content");
    
    // Act
    var exists = _fs.FileExists('Z', ["test.txt"]);
    
    // Assert
    Assert.True(exists);
}

[Fact]
public void FileExists_NonExisting_ReturnsFalse()
{
    // Act
    var exists = _fs.FileExists('Z', ["notfound.txt"]);
    
    // Assert
    Assert.False(exists);
}
```

**Test 2: DirectoryExists**
```csharp
[Fact]
public void DirectoryExists_ExistingDir_ReturnsTrue()
{
    // Arrange
    var nativePath = Path.Combine(_testRoot, "testdir");
    Directory.CreateDirectory(nativePath);
    
    // Act
    var exists = _fs.DirectoryExists('Z', ["testdir"]);
    
    // Assert
    Assert.True(exists);
}
```

**Test 3: CreateDirectory**
```csharp
[Fact]
public void CreateDirectory_CreatesDirectory()
{
    // Act
    _fs.CreateDirectory('Z', ["newdir"]);
    
    // Assert
    var nativePath = Path.Combine(_testRoot, "newdir");
    Assert.True(Directory.Exists(nativePath));
}

[Fact]
public void CreateDirectory_Nested_CreatesParents()
{
    // Act
    _fs.CreateDirectory('Z', ["parent", "child", "grandchild"]);
    
    // Assert
    var nativePath = Path.Combine(_testRoot, "parent", "child", "grandchild");
    Assert.True(Directory.Exists(nativePath));
}
```

**Test 4: EnumerateEntries**
```csharp
[Fact]
public void EnumerateEntries_ReturnsFilesAndDirs()
{
    // Arrange
    Directory.CreateDirectory(Path.Combine(_testRoot, "dir1"));
    File.WriteAllText(Path.Combine(_testRoot, "file1.txt"), "");
    File.WriteAllText(Path.Combine(_testRoot, "file2.log"), "");
    
    // Act
    var entries = _fs.EnumerateEntries('Z', [], "*.*").ToList();
    
    // Assert
    Assert.Contains(entries, e => e.Name == "dir1" && e.IsDirectory);
    Assert.Contains(entries, e => e.Name == "file1.txt" && !e.IsDirectory);
    Assert.Contains(entries, e => e.Name == "file2.log" && !e.IsDirectory);
}
```

**Test 5: Wildcard filtering**
```csharp
[Fact]
public void EnumerateEntries_Wildcard_Filters()
{
    // Arrange
    File.WriteAllText(Path.Combine(_testRoot, "test.txt"), "");
    File.WriteAllText(Path.Combine(_testRoot, "test.log"), "");
    File.WriteAllText(Path.Combine(_testRoot, "other.txt"), "");
    
    // Act
    var entries = _fs.EnumerateEntries('Z', [], "test.*").ToList();
    
    // Assert
    Assert.Equal(2, entries.Count);
    Assert.Contains(entries, e => e.Name == "test.txt");
    Assert.Contains(entries, e => e.Name == "test.log");
}
```

**Test 6: CopyFile**
```csharp
[Fact]
public void CopyFile_CopiesContent()
{
    // Arrange
    var sourcePath = Path.Combine(_testRoot, "source.txt");
    File.WriteAllText(sourcePath, "test content");
    
    // Act
    _fs.CopyFile('Z', ["source.txt"], 'Z', ["dest.txt"], overwrite: false);
    
    // Assert
    var destPath = Path.Combine(_testRoot, "dest.txt");
    Assert.True(File.Exists(destPath));
    Assert.Equal("test content", File.ReadAllText(destPath));
}
```

**Test 7: DeleteFile**
```csharp
[Fact]
public void DeleteFile_RemovesFile()
{
    // Arrange
    var filePath = Path.Combine(_testRoot, "delete_me.txt");
    File.WriteAllText(filePath, "");
    
    // Act
    _fs.DeleteFile('Z', ["delete_me.txt"]);
    
    // Assert
    Assert.False(File.Exists(filePath));
}
```

**Test 8: ReadAllText / OpenRead**
```csharp
[Fact]
public void ReadAllText_ReturnsContent()
{
    // Arrange
    var filePath = Path.Combine(_testRoot, "content.txt");
    File.WriteAllText(filePath, "Hello World");
    
    // Act
    var content = _fs.ReadAllText('Z', ["content.txt"]);
    
    // Assert
    Assert.Equal("Hello World", content);
}
```

**Test 9: GetNativePath (voor Process.Start)**
```csharp
[Fact]
public void GetNativePath_ConvertsVirtualToNative()
{
    // Act
    var nativePath = _fs.GetNativePath('Z', ["Users", "test.txt"]);
    
    // Assert
    var expected = Path.Combine(_testRoot, "Users", "test.txt");
    Assert.Equal(expected, nativePath);
}
```

**Test 10: Invalid drive throws**
```csharp
[Fact]
public void InvalidDrive_ThrowsException()
{
    // Act & Assert
    Assert.Throws<DriveNotFoundException>(() => 
        _fs.FileExists('X', ["test.txt"])
    );
}
```

## Implementatie Stappen

### 3.1 Update IFileSystem interface

**Bestand:** `Context/IFileSystem.cs`

```csharp
public interface IFileSystem
{
    // Existing
    string GetFullPathDisplayName(char drive, string[] path);
    
    // NEW - File operations
    bool FileExists(char drive, string[] path);
    bool DirectoryExists(char drive, string[] path);
    void CreateDirectory(char drive, string[] path);
    void DeleteFile(char drive, string[] path);
    void DeleteDirectory(char drive, string[] path, bool recursive);
    
    // NEW - Enumeration
    IEnumerable<(string Name, bool IsDirectory)> EnumerateEntries(
        char drive, string[] path, string pattern);
    
    // NEW - File I/O
    Stream OpenRead(char drive, string[] path);
    Stream OpenWrite(char drive, string[] path, bool append);
    string ReadAllText(char drive, string[] path);
    void WriteAllText(char drive, string[] path, string content);
    
    // NEW - File operations
    void CopyFile(char sourceDrive, string[] sourcePath, 
                  char destDrive, string[] destPath, bool overwrite);
    void MoveFile(char sourceDrive, string[] sourcePath,
                  char destDrive, string[] destPath);
    void RenameFile(char drive, string[] path, string newName);
    
    // NEW - Metadata
    FileAttributes GetAttributes(char drive, string[] path);
    void SetAttributes(char drive, string[] path, FileAttributes attributes);
    long GetFileSize(char drive, string[] path);
    DateTime GetLastWriteTime(char drive, string[] path);
    
    // NEW - Path conversion (voor Process.Start)
    string GetNativePath(char drive, string[] path);
}
```

### 3.2 DosFileSystem implementatie

**Bestand:** `Bat/Context/DosFileSystem.cs`

```csharp
namespace Bat.Context;

public class DosFileSystem : IFileSystem
{
    private readonly Dictionary<char, string> _driveMapping;
    
    // Constructor met C: → Z: mapping
    public DosFileSystem(string? rootPath = null)
    {
        _driveMapping = new Dictionary<char, string>
        {
            ['Z'] = rootPath ?? @"C:\"  // Map C: naar Z: (of custom root)
        };
        
        // Voeg andere drives toe als ze bestaan
        foreach (var drive in DriveInfo.GetDrives())
        {
            var letter = char.ToUpper(drive.Name[0]);
            if (letter != 'C' && !_driveMapping.ContainsKey(letter))
                _driveMapping[letter] = drive.RootDirectory.FullName;
        }
    }
    
    public string GetNativePath(char drive, string[] path)
    {
        if (!_driveMapping.TryGetValue(char.ToUpper(drive), out var root))
            throw new DriveNotFoundException($"Drive {drive}: not found");
        
        return Path.Combine([root, .. path]);
    }
    
    public bool FileExists(char drive, string[] path)
    {
        var nativePath = GetNativePath(drive, path);
        return File.Exists(nativePath);
    }
    
    public bool DirectoryExists(char drive, string[] path)
    {
        var nativePath = GetNativePath(drive, path);
        return Directory.Exists(nativePath);
    }
    
    public void CreateDirectory(char drive, string[] path)
    {
        var nativePath = GetNativePath(drive, path);
        Directory.CreateDirectory(nativePath);  // Creates parents automatically
    }
    
    public IEnumerable<(string Name, bool IsDirectory)> EnumerateEntries(
        char drive, string[] path, string pattern)
    {
        var nativePath = GetNativePath(drive, path);
        
        if (!Directory.Exists(nativePath))
            yield break;
        
        foreach (var entry in Directory.EnumerateFileSystemEntries(nativePath, pattern))
        {
            var name = Path.GetFileName(entry);
            var isDir = Directory.Exists(entry);
            yield return (name, isDir);
        }
    }
    
    public string ReadAllText(char drive, string[] path)
    {
        var nativePath = GetNativePath(drive, path);
        return File.ReadAllText(nativePath);
    }
    
    public void CopyFile(char srcDrive, string[] srcPath, 
                        char dstDrive, string[] dstPath, bool overwrite)
    {
        var srcNative = GetNativePath(srcDrive, srcPath);
        var dstNative = GetNativePath(dstDrive, dstPath);
        File.Copy(srcNative, dstNative, overwrite);
    }
    
    // ... Implementeer alle andere methods als doorgeefluik naar System.IO
}
```

### 3.3 DriveNotFoundException exception

**Bestand:** `Bat/Exceptions/DriveNotFoundException.cs`

```csharp
namespace Bat.Exceptions;

public class DriveNotFoundException : Exception
{
    public DriveNotFoundException(string message) : base(message) { }
}
```

### 3.4 Update Context om DosFileSystem te gebruiken

**Bestand:** `Bat/Context/DosContext.cs`

```csharp
public class DosContext : Context
{
    public DosContext() : base(new DosFileSystem())
    {
        // Initialize met Z: als "C:" drive
        CurrentDrive = 'Z';
        
        // Default environment
        EnvironmentVariables["PROMPT"] = "$P$G";
    }
}
```

## Implementatie Volgorde

1. Update `IFileSystem` interface met alle methods
2. Creëer `DriveNotFoundException`
3. Implementeer `DosFileSystem.GetNativePath()` + test
4. Implementeer `FileExists` + test
5. Implementeer `DirectoryExists` + test
6. Implementeer `CreateDirectory` + test
7. Implementeer `DeleteFile` + test
8. Implementeer `DeleteDirectory` + test
9. Implementeer `EnumerateEntries` + test
10. Implementeer `CopyFile` + test
11. Implementeer `MoveFile` + test
12. Implementeer `RenameFile` + test
13. Implementeer `OpenRead` + test
14. Implementeer `OpenWrite` + test
15. Implementeer `ReadAllText` + test
16. Implementeer `WriteAllText` + test
17. Implementeer `GetAttributes` + test
18. Implementeer `SetAttributes` + test
19. Implementeer `GetFileSize` + test
20. Implementeer `GetLastWriteTime` + test
21. Update `DosContext` om `DosFileSystem` te gebruiken
22. Run alle tests (20+ tests)

## Acceptance Criteria

- [ ] Alle IFileSystem methods geïmplementeerd
- [ ] C: is gemapped naar Z: (zichtbaar in prompts)
- [ ] FileExists('Z', ["Windows"]) werkt
- [ ] CreateDirectory maakt subdirectories
- [ ] EnumerateEntries met wildcards werkt
- [ ] GetNativePath('Z', ["test.txt"]) → native pad
- [ ] Invalid drive gooit DriveNotFoundException
- [ ] 20+ unit tests slagen

## Manual Testing

Start Bat en verifieer:
```sh
Z:\> cd Users
Z:\Users>

Z:\> dir
(toont Z:\Users directory - maar dit is eigenlijk C:\Users)

Z:\> dir C:\
The system cannot find the drive specified.
(Want C: is niet gemapped - alleen Z:)
```

## Geschatte Tijd

2-3 uur (veel methodes, maar meeste zijn simpele doorgeefluiken)

## Referenties

- **System.IO docs:** https://learn.microsoft.com/dotnet/api/system.io
- **FileInfo/DirectoryInfo:** https://learn.microsoft.com/dotnet/api/system.io.fileinfo
- **IMPLEMENTATION_PLAN.md:** Fase 4 (IFileSystem uitbreiden)

## Extra: Drive Mapping Configuratie

Hou er rekening mee dat de drive mapping later in de command line van bat kan worden uitgevoerd,
of met een aparte speciale utility.

```csharp
// In Program.cs:
var driveMapping = new Dictionary<char, string>
{
    ['Z'] = @"C:\",  // Windows C: als Z:
    ['D'] = @"D:\",  // Indien aanwezig
};

var fs = new DosFileSystem(driveMapping);
var context = new DosContext(fs);
```

Dit maakt het later makkelijk om Unix mapping toe te voegen (Step 8+).
