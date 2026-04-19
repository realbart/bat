using System.Collections.Frozen;
using Bat.Commands;
using Bat.Console;
using Bat.Context;
using Bat.Context.Dos;
using Bat.Execution;
using Bat.Tokens;
using Context;

namespace Bat.UnitTests;

// ── FileSystem template-method subst tests ───────────────────────────────────

#if WINDOWS
[TestClass]
public class SubstFileSystemTests : IDisposable
#else
public class SubstFileSystemTests
#endif
{
    private readonly string _testRoot;
    private readonly DosFileSystem _fs;
    private bool _disposed;

    public SubstFileSystemTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"BatSubstFsTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
        _fs = new(new() { ['Z'] = _testRoot, ['C'] = @"C:\" });
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing && Directory.Exists(_testRoot)) Directory.Delete(_testRoot, recursive: true);
        _disposed = true;
    }

    public void Dispose() { Dispose(disposing: true); GC.SuppressFinalize(this); }

    // ── GetSubsts ────────────────────────────────────────────────────────────

    [TestMethod]
    public void GetSubsts_InitiallyEmpty()
    {
        Assert.AreEqual(0, _fs.GetSubsts().Count);
    }

    [TestMethod]
    public void AddSubst_ThenGetSubsts_ReturnsMapping()
    {
        _fs.AddSubst('Q', @"C:\Temp");
        var substs = _fs.GetSubsts();
        Assert.AreEqual(1, substs.Count);
        Assert.AreEqual(@"C:\Temp", substs['Q']);
    }

    [TestMethod]
    public void AddSubst_LowercaseDriveLetter_StoredAsUppercase()
    {
        _fs.AddSubst('q', @"C:\Temp");
        Assert.IsTrue(_fs.GetSubsts().ContainsKey('Q'));
        Assert.IsFalse(_fs.GetSubsts().ContainsKey('q'));
    }

    [TestMethod]
    public void RemoveSubst_ExistingDrive_RemovesIt()
    {
        _fs.AddSubst('Q', @"C:\Temp");
        _fs.RemoveSubst('Q');
        Assert.AreEqual(0, _fs.GetSubsts().Count);
    }

    [TestMethod]
    public void RemoveSubst_NonExistentDrive_DoesNotThrow()
    {
        _fs.RemoveSubst('Q');  // no error
    }

    // ── GetNativePath template method ────────────────────────────────────────

    [TestMethod]
    public void GetNativePath_SubstedDrive_RootPath_ReturnsSubstRoot()
    {
        _fs.AddSubst('Q', @"C:\Temp");
        Assert.AreEqual(@"C:\Temp", _fs.GetNativePath('Q', []));
    }

    [TestMethod]
    public void GetNativePath_SubstedDrive_WithSegments_CombinesPath()
    {
        _fs.AddSubst('Q', @"C:\Temp");
        Assert.AreEqual(@"C:\Temp\subdir\file.txt", _fs.GetNativePath('Q', ["subdir", "file.txt"]));
    }

    [TestMethod]
    public void GetNativePath_SubstedDrive_TrailingBackslashOnRoot_StillCombinesCorrectly()
    {
        _fs.AddSubst('Q', @"C:\Temp\");
        Assert.AreEqual(@"C:\Temp\subdir", _fs.GetNativePath('Q', ["subdir"]));
    }

    [TestMethod]
    public void GetNativePath_SubstedDrive_CaseInsensitiveDriveLetter()
    {
        _fs.AddSubst('Q', @"C:\Temp");
        Assert.AreEqual(_fs.GetNativePath('Q', []), _fs.GetNativePath('q', []));
    }

    [TestMethod]
    public void GetNativePath_UnsubstedDrive_DelegatesNormally()
    {
        var result = _fs.GetNativePath('Z', ["file.txt"]);
        Assert.AreEqual(Path.Combine(_testRoot, "file.txt"), result);
    }

    // ── TryGetNativePath ─────────────────────────────────────────────────────

    [TestMethod]
    public void TryGetNativePath_SubstedDrive_ReturnsTrueAndPath()
    {
        _fs.AddSubst('Q', @"C:\Temp");
        Assert.IsTrue(_fs.TryGetNativePath('Q', [], out var nativePath));
        Assert.AreEqual(@"C:\Temp", nativePath);
    }

    [TestMethod]
    public void TryGetNativePath_SubstedDrive_WithSegments_ReturnsCombinedPath()
    {
        _fs.AddSubst('Q', @"C:\Temp");
        Assert.IsTrue(_fs.TryGetNativePath('Q', ["sub"], out var nativePath));
        Assert.AreEqual(@"C:\Temp\sub", nativePath);
    }

    [TestMethod]
    public void TryGetNativePath_UnknownDrive_ReturnsFalse()
    {
        Assert.IsFalse(_fs.TryGetNativePath('X', [], out _));
    }

    [TestMethod]
    public void TryGetNativePath_UnsubstedDrive_KnownDrive_ReturnsTrue()
    {
        Assert.IsTrue(_fs.TryGetNativePath('Z', [], out var nativePath));
        Assert.AreEqual(_testRoot, nativePath);
    }

    // ── Volume serial (Windows-only: uses GetVolumeInformationW) ────────────
    //
    // Real Windows SUBST behaviour (confirmed by testing):
    //   subst Q: C:\Temp  →  vol Q:  shows the SAME serial as vol C:
    //
    // Our DosFileSystem behaviour:
    //   - Subst to drive root  (length == 3, e.g. "C:\")      → same serial as device
    //   - Subst to subdirectory (length > 3, e.g. "C:\Temp")  → HashCode.Combine(serial, path)
    //     This gives a stable, distinct identifier for each virtual mount point.

    [TestMethod]
    public void GetVolumeSerialNumber_SubstToRoot_SameAsUnderlyingDevice()
    {
        if (!OperatingSystem.IsWindows()) return;
        var fs = new DosFileSystem(new());
        fs.AddSubst('Q', @"C:\");
        Assert.AreEqual(fs.GetVolumeSerialNumber('C'), fs.GetVolumeSerialNumber('Q'));
    }

    [TestMethod]
    public void GetVolumeSerialNumber_SubstToSubdir_DifferentFromDevice()
    {
        if (!OperatingSystem.IsWindows()) return;
        var fs = new DosFileSystem(new());
        fs.AddSubst('Q', Path.GetTempPath().TrimEnd('\\'));
        Assert.AreNotEqual(fs.GetVolumeSerialNumber('C'), fs.GetVolumeSerialNumber('Q'));
    }

    // ── FileExists / DirectoryExists route through the subst ─────────────────

    [TestMethod]
    public void FileExists_ThroughSubst_ReturnsTrue()
    {
        var file = Path.Combine(_testRoot, "hello.txt");
        File.WriteAllText(file, "x");
        _fs.AddSubst('Q', _testRoot);
        Assert.IsTrue(_fs.FileExists('Q', ["hello.txt"]));
    }

    [TestMethod]
    public void DirectoryExists_ThroughSubst_ReturnsTrue()
    {
        var sub = Path.Combine(_testRoot, "sub");
        Directory.CreateDirectory(sub);
        _fs.AddSubst('Q', _testRoot);
        Assert.IsTrue(_fs.DirectoryExists('Q', ["sub"]));
    }
}

// ── Subst/Program.cs behaviour tests ─────────────────────────────────────────

[TestClass]
public class SubstProgramTests
{
    private static readonly ArgumentSpec SubstSpec =
        new(new HashSet<string> { "D" }.ToFrozenSet(), FrozenSet<string>.Empty);

    private static IArgumentSet Args(params IToken[] tokens) =>
        ArgumentSet.Parse(tokens, SubstSpec);

    private static (TestCommandContext ctx, TestFileSystem fs, StringWriter sw) Setup(char drive = 'C')
    {
        var fs = new TestFileSystem();
        var ctx = new TestCommandContext(fs);
        ctx.SetCurrentDrive(drive);
        ctx.SetPath(drive, []);
        return (ctx, fs, new());
    }

    // ── List ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task List_NoSubsts_ProducesNoOutput_ReturnsZero()
    {
        var (ctx, _, sw) = Setup();
        var result = await Subst.Program.Main(ctx, Args(), sw);
        Assert.AreEqual(0, result);
        Assert.AreEqual("", sw.ToString());
    }

    [TestMethod]
    public async Task List_WithOneSubst_ShowsCorrectFormat()
    {
        var (ctx, fs, sw) = Setup();
        fs.AddSubst('Q', @"C:\Temp");
        var result = await Subst.Program.Main(ctx, Args(), sw);
        Assert.AreEqual(0, result);
        Assert.AreEqual(@"Q:\: => C:\Temp", sw.ToString().Trim());
    }

    [TestMethod]
    public async Task List_WithMultipleSubsts_SortsAlphabetically()
    {
        var (ctx, fs, sw) = Setup();
        fs.AddSubst('Z', @"C:\Foo");
        fs.AddSubst('A', @"C:\Bar");
        fs.AddSubst('M', @"C:\Mid");
        await Subst.Program.Main(ctx, Args(), sw);
        var lines = sw.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual('A', lines[0][0]);
        Assert.AreEqual('M', lines[1][0]);
        Assert.AreEqual('Z', lines[2][0]);
    }

    // ── Assign ───────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Assign_ValidPath_AddsToFileSystem_ReturnsZero()
    {
        var (ctx, fs, sw) = Setup();
        fs.AddDir('C', ["Temp"]);
        var result = await Subst.Program.Main(
            ctx, Args(Token.Text("Q:"), Token.Whitespace(" "), Token.Text(@"C:\Temp")), sw);
        Assert.AreEqual(0, result);
        Assert.IsTrue(fs.GetSubsts().ContainsKey('Q'));
        Assert.AreEqual("", sw.ToString());
    }

    [TestMethod]
    public async Task Assign_PathNotFound_ShowsError_ReturnsOne()
    {
        var (ctx, _, sw) = Setup();
        var result = await Subst.Program.Main(
            ctx, Args(Token.Text("Q:"), Token.Whitespace(" "), Token.Text(@"C:\NoSuch")), sw);
        Assert.AreEqual(1, result);
        Assert.AreEqual(@"Path not found - C:\NoSuch", sw.ToString().Trim());
    }

    [TestMethod]
    public async Task Assign_AlreadySubsted_ShowsError_ReturnsOne()
    {
        var (ctx, fs, sw) = Setup();
        fs.AddDir('C', ["Temp"]);
        fs.AddSubst('Q', @"C:\Existing");
        var result = await Subst.Program.Main(
            ctx, Args(Token.Text("Q:"), Token.Whitespace(" "), Token.Text(@"C:\Temp")), sw);
        Assert.AreEqual(1, result);
        Assert.AreEqual("Drive already SUBSTed", sw.ToString().Trim());
        Assert.AreEqual(@"C:\Existing", fs.GetSubsts()['Q']);  // unchanged
    }

    [TestMethod]
    public async Task Assign_RelativePath_ResolvesFromCurrentDir()
    {
        var (ctx, fs, sw) = Setup();
        ctx.SetPath('C', ["Users", "Bart"]);
        fs.AddDir('C', ["Users", "Bart", "Docs"]);
        var result = await Subst.Program.Main(
            ctx, Args(Token.Text("Q:"), Token.Whitespace(" "), Token.Text("Docs")), sw);
        Assert.AreEqual(0, result);
        Assert.IsTrue(fs.GetSubsts().ContainsKey('Q'));
    }

    [TestMethod]
    public async Task Assign_LowercaseDriveLetter_StoredAsUppercase()
    {
        var (ctx, fs, sw) = Setup();
        fs.AddDir('C', ["Temp"]);
        await Subst.Program.Main(
            ctx, Args(Token.Text("q:"), Token.Whitespace(" "), Token.Text(@"C:\Temp")), sw);
        Assert.IsTrue(fs.GetSubsts().ContainsKey('Q'));
        Assert.IsFalse(fs.GetSubsts().ContainsKey('q'));
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Delete_ExistingDrive_RemovesIt_ReturnsZero()
    {
        var (ctx, fs, sw) = Setup();
        fs.AddSubst('Q', @"C:\Temp");
        var result = await Subst.Program.Main(
            ctx, Args(Token.Text("Q:"), Token.Whitespace(" "), Token.Text("/D")), sw);
        Assert.AreEqual(0, result);
        Assert.IsFalse(fs.GetSubsts().ContainsKey('Q'));
        Assert.AreEqual("", sw.ToString());
    }

    [TestMethod]
    public async Task Delete_NonExistentDrive_ShowsInvalidParameter_ReturnsOne()
    {
        var (ctx, _, sw) = Setup();
        var result = await Subst.Program.Main(
            ctx, Args(Token.Text("Z:"), Token.Whitespace(" "), Token.Text("/D")), sw);
        Assert.AreEqual(1, result);
        Assert.AreEqual("Invalid parameter - Z:", sw.ToString().Trim());
    }

    [TestMethod]
    public async Task Delete_LowercaseDriveLetter_FindsUppercaseSubst()
    {
        var (ctx, fs, sw) = Setup();
        fs.AddSubst('Q', @"C:\Temp");
        var result = await Subst.Program.Main(
            ctx, Args(Token.Text("q:"), Token.Whitespace(" "), Token.Text("/D")), sw);
        Assert.AreEqual(0, result);
        Assert.IsFalse(fs.GetSubsts().ContainsKey('Q'));
    }

    // ── Help ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Help_PrintsHelpText_ReturnsZero()
    {
        var (ctx, _, sw) = Setup();
        var result = await Subst.Program.Main(
            ctx, Args(Token.Text("/?")), sw);
        Assert.AreEqual(0, result);
        Assert.IsTrue(sw.ToString().Contains("SUBST"));
        Assert.IsTrue(sw.ToString().Contains("/D"));
    }

    // ── DirCommand volume header on subst'd drive ─────────────────────────────
    //
    // Real Windows: vol Q: (after subst Q: C:\Temp) shows same label and serial as C:.
    // Our DirCommand always prints "has no label" and derives the serial via
    // GetVolumeSerialNumber, which for TestFileSystem always returns 0.
    // The meaningful assertion is that the header uses the VIRTUAL drive letter.

    [TestMethod]
    public async Task Dir_SubstedDrive_VolumeHeaderUsesVirtualDriveLetter()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddDir('Q', []);  // TestFileSystem.DirectoryExists checks _dirs; subst-chain not followed in mock
        fs.AddSubst('Q', @"C:\");

        var console = new TestConsole();
        var ctx = new TestCommandContext(fs) { Console = console };
        ctx.SetCurrentDrive('Q');
        ctx.SetPath('Q', []);

        var bc = new BatchContext { Context = ctx };
        var cmd = new DirCommand();

        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(), bc, []);

        Assert.IsTrue(console.OutLines.Any(l => l.Contains("Volume in drive Q")));
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("Directory of Q:\\")));
    }
}


