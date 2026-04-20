using Bat.Context.Ux;
using Bat.Commands;
using Bat.Execution;
using Context;

namespace Bat.UnitTests;

[TestClass]
public class UnixDirDisplayTests
{
    private string _testRoot;

    [TestInitialize]
    public void Setup()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"BatDirTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, true);
    }

    [TestMethod]
    public void EnumerateEntries_DetectsSymlinks()
    {
        if (OperatingSystem.IsWindows()) return;

        var targetDir = Path.Combine(_testRoot, "targetDir");
        Directory.CreateDirectory(targetDir);
        var symlinkDir = Path.Combine(_testRoot, "symlinkDir");
        File.CreateSymbolicLink(symlinkDir, targetDir);

        var targetFile = Path.Combine(_testRoot, "targetFile.txt");
        File.WriteAllText(targetFile, "hello");
        var symlinkFile = Path.Combine(_testRoot, "symlinkFile.txt");
        File.CreateSymbolicLink(symlinkFile, targetFile);

        var fs = new UxFileSystemAdapter(new() { ['Z'] = _testRoot });
        var entries = fs.EnumerateEntries('Z', [], "*").ToList();

        var dirLink = entries.FirstOrDefault(e => e.Name == "symlinkDir");
        var fileLink = entries.FirstOrDefault(e => e.Name == "symlinkFile.txt");

        Assert.IsNotNull(dirLink, "symlinkDir not found");
        Assert.IsNotNull(fileLink, "symlinkFile.txt not found");

        Assert.IsTrue(dirLink.Attributes.HasFlag(FileAttributes.ReparsePoint), "dirLink should have ReparsePoint attribute");
        Assert.IsTrue(fileLink.Attributes.HasFlag(FileAttributes.ReparsePoint), "fileLink should have ReparsePoint attribute");
        
        Assert.IsTrue(dirLink.IsDirectory, "dirLink should be reported as directory");
        Assert.IsFalse(fileLink.IsDirectory, "fileLink should NOT be reported as directory");
    }

    [TestMethod]
    public async Task DirCommand_ShowsCorrectLabels()
    {
        if (OperatingSystem.IsWindows()) return;

        var targetDir = Path.Combine(_testRoot, "targetDir");
        Directory.CreateDirectory(targetDir);
        var symlinkDir = Path.Combine(_testRoot, "symlinkDir");
        File.CreateSymbolicLink(symlinkDir, targetDir);

        var targetFile = Path.Combine(_testRoot, "targetFile.txt");
        File.WriteAllText(targetFile, "hello");
        var symlinkFile = Path.Combine(_testRoot, "symlinkFile.txt");
        File.CreateSymbolicLink(symlinkFile, targetFile);

        // We can't easily create a real mount point without sudo, 
        // but we can check if "/" is detected as <JUNCTION> if we map it.
        
        var fs = new UxFileSystemAdapter(new() { ['Z'] = _testRoot, ['R'] = "/" });
        var console = new TestConsole();
        var ctx = new UxContextAdapter(fs, console);
        ctx.SetCurrentDrive('Z');
        ctx.SetPath('Z', []);

        var bc = new BatchContext { Context = ctx };
        var cmd = new DirCommand();

        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(), bc, []);

        var output = string.Join("\n", console.OutLines);
        
        Assert.IsTrue(output.Contains("<SYMLINKD>"), "Output should contain <SYMLINKD>");
        Assert.IsTrue(output.Contains("<SYMLINK>"), "Output should contain <SYMLINK>");
        
        // Check junction (mount point)
        ctx.SetCurrentDrive('R');
        ctx.SetPath('R', []);
        
        // Enumerate "/" might be slow and large, but we only need the entry for some mount point if it exists
        // Actually, we can just check if EnumerateEntries on R: (which is /) returns something with <JUNCTION>
        // But / contains many things. Let's try to find a known mount point.
        
        var entries = fs.EnumerateEntries('R', [], "*").ToList();
        var mountEntry = entries.FirstOrDefault(e => e.Attributes.HasFlag(FileAttributes.Offline));
        
        if (mountEntry.Name != null) {
             var console2 = new TestConsole();
             var ctx2 = new UxContextAdapter(fs, console2);
             ctx2.SetCurrentDrive('R');
             ctx2.SetPath('R', []);
             var bc2 = new BatchContext { Context = ctx2 };
             await cmd.ExecuteAsync(TestArgs.For<DirCommand>(), bc2, []);
             output = string.Join("\n", console2.OutLines);
             Assert.IsTrue(output.Contains("<JUNCTION>"), "Output should contain <JUNCTION> for mount points");
        }
    }
}
