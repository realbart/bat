using Bat.Context.Ux;
using Bat.Console;
using Bat.Context;
using Bat.Execution;
using Bat.Tokens;
using Bat.Commands;
using Context;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tree;

namespace Bat.UnitTests;

[TestClass]
public class TreeExclusionTests
{
    private string _testRoot;

    [TestInitialize]
    public void Setup()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"BatTreeTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, true);
    }

    [TestMethod]
    public async Task Tree_DoesNotRecurseIntoSymlinks()
    {
        if (OperatingSystem.IsWindows()) return;

        // Structure:
        // _testRoot/
        //   realDir/
        //     secretFile.txt
        //   symlinkDir -> realDir

        var realDir = Path.Combine(_testRoot, "realDir");
        Directory.CreateDirectory(realDir);
        File.WriteAllText(Path.Combine(realDir, "secretFile.txt"), "shhh");

        var symlinkDir = Path.Combine(_testRoot, "symlinkDir");
        File.CreateSymbolicLink(symlinkDir, realDir);

        var fs = new UxFileSystemAdapter(new Dictionary<char, string> { ['Z'] = _testRoot });
        var console = new TestConsole();
        var ctx = new UxContextAdapter(fs, console);
        ctx.SetCurrentDrive('Z');
        ctx.SetPath('Z', []);

        // Run TREE /F (to see files)
        var spec = ArgumentSpec.From([new BuiltInCommandAttribute("tree") { Flags = "F" }]);
        var args = ArgumentSet.Parse([Token.Text("/F")], spec);
        
        await Tree.Program.Main(ctx, args);

        var output = string.Join("\n", console.OutLines);
        
        // Should contain symlinkDir and realDir
        Assert.IsTrue(output.Contains("realDir"), "Output should contain realDir");
        Assert.IsTrue(output.Contains("symlinkDir"), "Output should contain symlinkDir");
        
        // Should contain secretFile.txt under realDir
        Assert.IsTrue(output.Contains("secretFile.txt"), "Output should contain secretFile.txt");
        
        // We need to check if secretFile.txt appears TWICE (once for realDir, once for symlinkDir)
        // or if it only appears once.
        var count = System.Text.RegularExpressions.Regex.Matches(output, "secretFile.txt").Count;
        Assert.AreEqual(1, count, "secretFile.txt should only appear once (under realDir), not under symlinkDir");
    }

    [TestMethod]
    public async Task Dir_Recursive_DoesNotRecurseIntoSymlinks()
    {
        if (OperatingSystem.IsWindows()) return;

        // Structure:
        // _testRoot/
        //   realDir/
        //     secretFile.txt
        //   symlinkDir -> realDir

        var realDir = Path.Combine(_testRoot, "realDir");
        Directory.CreateDirectory(realDir);
        File.WriteAllText(Path.Combine(realDir, "secretFile.txt"), "shhh");

        var symlinkDir = Path.Combine(_testRoot, "symlinkDir");
        File.CreateSymbolicLink(symlinkDir, realDir);

        var fs = new UxFileSystemAdapter(new Dictionary<char, string> { ['Z'] = _testRoot });
        var console = new TestConsole();
        var ctx = new UxContextAdapter(fs, console);
        ctx.SetCurrentDrive('Z');
        ctx.SetPath('Z', []);

        var bc = new BatchContext { Context = ctx };
        var cmd = new DirCommand();

        // Run DIR /S /B
        var spec = ArgumentSpec.From([new BuiltInCommandAttribute("dir") { Flags = "S B" }]);
        var args = ArgumentSet.Parse([Token.Text("/S"), Token.Text("/B")], spec);

        await cmd.ExecuteAsync(args, bc, []);

        var output = string.Join("\n", console.OutLines);

        // Bare format with /S shows relative paths or full paths depending on how it was called.
        // In our case, it shows the entries in the current directory.
        Assert.IsTrue(output.Contains("secretFile.txt"), "Output should contain secretFile.txt");
        Assert.IsTrue(output.Contains("realDir"), "Output should contain realDir");
        Assert.IsTrue(output.Contains("symlinkDir"), "Output should contain symlinkDir");
        
        // It should NOT contain secretFile.txt UNDER symlinkDir.
        // If it followed the symlink, we might see "symlinkDir/secretFile.txt"
        Assert.IsFalse(output.Contains("symlinkDir/secretFile.txt") || output.Contains("symlinkDir\\secretFile.txt"), "Output should NOT contain symlinkDir/secretFile.txt");
    }

    [TestMethod]
    public async Task Tree_DoesNotRecurseIntoSymlinks_HiddenDir()
    {
        if (OperatingSystem.IsWindows()) return;

        // Structure:
        // _testRoot/
        //   .realDir/
        //     secretFile.txt
        //   .symlinkDir -> .realDir

        var realDir = Path.Combine(_testRoot, ".realDir");
        Directory.CreateDirectory(realDir);
        File.WriteAllText(Path.Combine(realDir, "secretFile.txt"), "shhh");

        var symlinkDir = Path.Combine(_testRoot, ".symlinkDir");
        File.CreateSymbolicLink(symlinkDir, realDir);

        var fs = new UxFileSystemAdapter(new Dictionary<char, string> { ['Z'] = _testRoot });
        var console = new TestConsole();
        var ctx = new UxContextAdapter(fs, console);
        ctx.SetCurrentDrive('Z');
        ctx.SetPath('Z', []);

        // Run TREE /F (to see files)
        var spec = ArgumentSpec.From([new BuiltInCommandAttribute("tree") { Flags = "F" }]);
        var args = ArgumentSet.Parse([Token.Text("/F")], spec);
        
        await Tree.Program.Main(ctx, args);

        var output = string.Join("\n", console.OutLines);
        
        // Should contain .symlinkDir and .realDir
        Assert.IsTrue(output.Contains(".realDir"), "Output should contain .realDir");
        Assert.IsTrue(output.Contains(".symlinkDir"), "Output should contain .symlinkDir");
        
        // Should contain secretFile.txt under .realDir
        Assert.IsTrue(output.Contains("secretFile.txt"), "Output should contain secretFile.txt");
        
        // Check if secretFile.txt appears TWICE (once for .realDir, once for .symlinkDir)
        var count = System.Text.RegularExpressions.Regex.Matches(output, "secretFile.txt").Count;
        Assert.AreEqual(1, count, "secretFile.txt should only appear once (under .realDir), not under .symlinkDir");
    }

    [TestMethod]
    public async Task Tree_DoesNotRecurseIntoRecursiveSymlinks()
    {
        if (OperatingSystem.IsWindows()) return;

        // Structure:
        // _testRoot/
        //   .c/
        //     Users -> _testRoot (loop!)

        var dotC = Path.Combine(_testRoot, ".c");
        Directory.CreateDirectory(dotC);

        var usersSymlink = Path.Combine(dotC, "Users");
        File.CreateSymbolicLink(usersSymlink, _testRoot);

        var fs = new UxFileSystemAdapter(new Dictionary<char, string> { ['Z'] = _testRoot });
        var console = new TestConsole();
        var ctx = new UxContextAdapter(fs, console);
        ctx.SetCurrentDrive('Z');
        ctx.SetPath('Z', [".c"]);

        // Run TREE
        var spec = ArgumentSpec.From([new BuiltInCommandAttribute("tree")]);
        var args = ArgumentSet.Parse([], spec);

        // We need a timeout for the test itself, but MSTest handles this if we use a Task and wait.
        // Or we can rely on the fact that if it fails, it will hang.
        var treeTask = Tree.Program.Main(ctx, args);
        var delayTask = Task.Delay(TimeSpan.FromSeconds(5));

        if (await Task.WhenAny(treeTask, delayTask) == delayTask)
        {
            Assert.Fail("TREE command timed out! It likely entered an infinite loop via symlink.");
        }

        await treeTask;

        var output = string.Join("\n", console.OutLines);
        
        // Should contain .c
        Assert.IsTrue(output.Contains(".c"), "Output should contain .c");
        
        // Should contain Users (the symlink)
        Assert.IsTrue(output.Contains("Users"), "Output should contain Users");
        
        // Should NOT contain a second .c level (which would happen if it followed Users back to root)
        // Actually, TREE output is relative to the start folder.
        // If it starts in .c, it shows:
        // Z:\.c
        // └───Users
        
        // If it follows Users, it would show:
        // Z:\.c
        // └───Users
        //     ├───.c
        //     │   └───Users
        //     ...
        
        var count = System.Text.RegularExpressions.Regex.Matches(output, "Users").Count;
        Assert.AreEqual(1, count, "Users symlink should only appear once.");
    }

    [TestMethod]
    public async Task Tree_DoesNotRecurseIntoRecursiveSymlinks_AbsoluteTarget()
    {
        if (OperatingSystem.IsWindows()) return;

        // Structure:
        // /tmp/root/
        //   .c/
        //     Users -> /tmp/root (loop!)

        var root = Path.Combine(Path.GetTempPath(), $"TreeRoot_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var dotC = Path.Combine(root, ".c");
            Directory.CreateDirectory(dotC);

            var usersSymlink = Path.Combine(dotC, "Users");
            // Gebruik een absoluut pad voor het target
            File.CreateSymbolicLink(usersSymlink, root);

            var fs = new UxFileSystemAdapter(new Dictionary<char, string> { ['Z'] = root });
            var console = new TestConsole();
            var ctx = new UxContextAdapter(fs, console);
            ctx.SetCurrentDrive('Z');
            ctx.SetPath('Z', [".c"]);

            // Run TREE
            var spec = ArgumentSpec.From([new BuiltInCommandAttribute("tree")]);
            var args = ArgumentSet.Parse([], spec);

            var treeTask = Tree.Program.Main(ctx, args);
            var delayTask = Task.Delay(TimeSpan.FromSeconds(5));

            if (await Task.WhenAny(treeTask, delayTask) == delayTask)
            {
                Assert.Fail("TREE command timed out! It likely entered an infinite loop via absolute symlink.");
            }

            await treeTask;

            var output = string.Join("\n", console.OutLines);
            var count = System.Text.RegularExpressions.Regex.Matches(output, "Users").Count;
            Assert.AreEqual(1, count, "Users symlink should only appear once.");
        }
        finally
        {
            try {
                var usersSymlink = Path.Combine(root, ".c", "Users");
                if (File.Exists(usersSymlink)) File.Delete(usersSymlink);
            } catch {}
            Directory.Delete(root, true);
        }
    }
}
