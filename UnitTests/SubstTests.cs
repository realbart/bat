using System.Collections.Frozen;
using Bat.Commands;
using BatD.Context.Dos;
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

    // ── Substs property ──────────────────────────────────────────────────────

    [TestMethod]
    public void Substs_InitiallyEmpty()
    {
        Assert.AreEqual(0, _fs.Substs.Count);
    }

    [TestMethod]
    public void Substs_Add_ThenRead_ReturnsMapping()
    {
        _fs.Substs['Q'] = new BatPath('C', ["Temp"]);
        Assert.AreEqual(1, _fs.Substs.Count);
        Assert.AreEqual('C', _fs.Substs['Q'].Drive);
    }

    [TestMethod]
    public void Substs_LowercaseKey_MustBeStoredAsUppercase()
    {
        _fs.Substs[char.ToUpperInvariant('q')] = new BatPath('C', ["Temp"]);
        Assert.IsTrue(_fs.Substs.ContainsKey('Q'));
    }

    [TestMethod]
    public void Substs_Remove_ExistingDrive_RemovesIt()
    {
        _fs.Substs['Q'] = new BatPath('C', ["Temp"]);
        _fs.Substs.Remove('Q');
        Assert.AreEqual(0, _fs.Substs.Count);
    }

    [TestMethod]
    public void Substs_Remove_NonExistentDrive_DoesNotThrow()
    {
        _fs.Substs.Remove('Q');  // no error
    }

    // ── GetNativePath through subst ──────────────────────────────────────────

    [TestMethod]
    public async Task GetNativePath_SubstedDrive_RootPath_ReturnsSubstRoot()
    {
        _fs.Substs['Q'] = new BatPath('C', ["Temp"]);
        var result = await _fs.GetNativePathAsync(new BatPath('Q', []));
        Assert.AreEqual(@"C:\Temp", result.Path);
    }

    [TestMethod]
    public async Task GetNativePath_SubstedDrive_WithSegments_CombinesPath()
    {
        _fs.Substs['Q'] = new BatPath('C', ["Temp"]);
        var result = await _fs.GetNativePathAsync(new BatPath('Q', ["subdir", "file.txt"]));
        Assert.AreEqual(@"C:\Temp\subdir\file.txt", result.Path);
    }

    [TestMethod]
    public async Task GetNativePath_SubstedDrive_CaseInsensitiveDriveLetter()
    {
        _fs.Substs['Q'] = new BatPath('C', ["Temp"]);
        var upper = await _fs.GetNativePathAsync(new BatPath('Q', []));
        var lower = await _fs.GetNativePathAsync(new BatPath('q', []));
        Assert.AreEqual(upper.Path, lower.Path);
    }

    [TestMethod]
    public async Task GetNativePath_SubstedDrive_TrailingBackslashOnRoot_StillCombinesCorrectly()
    {
        _fs.Substs['Q'] = new BatPath('C', ["Temp"]);
        var result = await _fs.GetNativePathAsync(new BatPath('Q', ["subdir"]));
        Assert.AreEqual(@"C:\Temp\subdir", result.Path);
    }

    [TestMethod]
    public async Task GetNativePath_UnsubstedDrive_DelegatesNormally()
    {
        var result = await _fs.GetNativePathAsync(new BatPath('Z', ["file.txt"]));
        Assert.AreEqual(Path.Combine(_testRoot, "file.txt"), result.Path);
    }

    // ── TryGetNativePathAsync ────────────────────────────────────────────────

    [TestMethod]
    public async Task TryGetNativePath_SubstedDrive_ReturnsTrueAndPath()
    {
        _fs.Substs['Q'] = new BatPath('C', ["Temp"]);
        var (success, hostPath) = await _fs.TryGetNativePathAsync(new BatPath('Q', []));
        Assert.IsTrue(success);
        Assert.AreEqual(@"C:\Temp", hostPath.Path);
    }

    [TestMethod]
    public async Task TryGetNativePath_SubstedDrive_WithSegments_ReturnsCombinedPath()
    {
        _fs.Substs['Q'] = new BatPath('C', ["Temp"]);
        var (success, hostPath) = await _fs.TryGetNativePathAsync(new BatPath('Q', ["sub"]));
        Assert.IsTrue(success);
        Assert.AreEqual(@"C:\Temp\sub", hostPath.Path);
    }

    [TestMethod]
    public async Task TryGetNativePath_UnknownDrive_ReturnsFalse()
    {
        var (success, _) = await _fs.TryGetNativePathAsync(new BatPath('X', []));
        Assert.IsFalse(success);
    }

    [TestMethod]
    public async Task TryGetNativePath_UnsubstedDrive_KnownDrive_ReturnsTrue()
    {
        var (success, hostPath) = await _fs.TryGetNativePathAsync(new BatPath('Z', []));
        Assert.IsTrue(success);
        Assert.AreEqual(_testRoot, hostPath.Path);
    }

    // ── Volume serial (Windows-only: uses GetVolumeInformationW) ────────────

    [TestMethod]
    public async Task GetVolumeSerialNumber_SubstToRoot_SameAsUnderlyingDevice()
    {
        if (!OperatingSystem.IsWindows()) return;
        var fs = new DosFileSystem(new() { ['C'] = @"C:\" });
        fs.Substs['Q'] = new BatPath('C', []);
        var serialC = await fs.GetVolumeSerialNumberAsync('C');
        var serialQ = await fs.GetVolumeSerialNumberAsync('Q');
        Assert.AreEqual(serialC, serialQ);
    }

    [TestMethod]
    public async Task GetVolumeSerialNumber_SubstToSubdir_DifferentFromDevice()
    {
        if (!OperatingSystem.IsWindows()) return;
        var fs = new DosFileSystem(new() { ['C'] = @"C:\" });
        var tempSegments = Path.GetTempPath().TrimEnd('\\')[3..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
        fs.Substs['Q'] = new BatPath('C', tempSegments);
        var serialC = await fs.GetVolumeSerialNumberAsync('C');
        var serialQ = await fs.GetVolumeSerialNumberAsync('Q');
        Assert.AreNotEqual(serialC, serialQ);
    }

    // ── FileExists / DirectoryExists route through the subst ─────────────────

    [TestMethod]
    public async Task FileExists_ThroughSubst_ReturnsTrue()
    {
        var file = Path.Combine(_testRoot, "hello.txt");
        File.WriteAllText(file, "x");
        _fs.Substs['Q'] = new BatPath('Z', []);
        Assert.IsTrue(await _fs.FileExistsAsync(new BatPath('Q', ["hello.txt"])));
    }

    [TestMethod]
    public async Task DirectoryExists_ThroughSubst_ReturnsTrue()
    {
        var sub = Path.Combine(_testRoot, "sub");
        Directory.CreateDirectory(sub);
        _fs.Substs['Q'] = new BatPath('Z', []);
        Assert.IsTrue(await _fs.DirectoryExistsAsync(new BatPath('Q', ["sub"])));
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
        fs.Substs['Q'] = new BatPath('C', ["Temp"]);
        var result = await Subst.Program.Main(ctx, Args(), sw);
        Assert.AreEqual(0, result);
        Assert.IsTrue(sw.ToString().Trim().Contains("Q:\\"));
    }

    [TestMethod]
    public async Task List_WithMultipleSubsts_SortsAlphabetically()
    {
        var (ctx, fs, sw) = Setup();
        fs.Substs['Z'] = new BatPath('C', ["Foo"]);
        fs.Substs['A'] = new BatPath('C', ["Bar"]);
        fs.Substs['M'] = new BatPath('C', ["Mid"]);
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
        Assert.IsTrue(fs.Substs.ContainsKey('Q'));
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
        fs.Substs['Q'] = new BatPath('C', ["Existing"]);
        var result = await Subst.Program.Main(
            ctx, Args(Token.Text("Q:"), Token.Whitespace(" "), Token.Text(@"C:\Temp")), sw);
        Assert.AreEqual(1, result);
        Assert.AreEqual("Drive already SUBSTed", sw.ToString().Trim());
        Assert.AreEqual('C', fs.Substs['Q'].Drive);  // unchanged
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
        Assert.IsTrue(fs.Substs.ContainsKey('Q'));
    }

    [TestMethod]
    public async Task Assign_LowercaseDriveLetter_StoredAsUppercase()
    {
        var (ctx, fs, sw) = Setup();
        fs.AddDir('C', ["Temp"]);
        await Subst.Program.Main(
            ctx, Args(Token.Text("q:"), Token.Whitespace(" "), Token.Text(@"C:\Temp")), sw);
        Assert.IsTrue(fs.Substs.ContainsKey('Q'));
        Assert.IsFalse(fs.Substs.ContainsKey('q'));
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Delete_ExistingDrive_RemovesIt_ReturnsZero()
    {
        var (ctx, fs, sw) = Setup();
        fs.Substs['Q'] = new BatPath('C', ["Temp"]);
        var result = await Subst.Program.Main(
            ctx, Args(Token.Text("Q:"), Token.Whitespace(" "), Token.Text("/D")), sw);
        Assert.AreEqual(0, result);
        Assert.IsFalse(fs.Substs.ContainsKey('Q'));
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
        fs.Substs['Q'] = new BatPath('C', ["Temp"]);
        var result = await Subst.Program.Main(
            ctx, Args(Token.Text("q:"), Token.Whitespace(" "), Token.Text("/D")), sw);
        Assert.AreEqual(0, result);
        Assert.IsFalse(fs.Substs.ContainsKey('Q'));
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

    [TestMethod]
    public async Task Dir_SubstedDrive_VolumeHeaderUsesVirtualDriveLetter()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddDir('Q', []);
        fs.Substs['Q'] = new BatPath('C', []);

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
