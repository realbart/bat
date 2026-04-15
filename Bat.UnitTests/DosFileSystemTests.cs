#if WINDOWS
using Bat.Context;
using Bat.Context.Dos;

namespace Bat.UnitTests;

[TestClass]
public class DosFileSystemTests : IDisposable
{
    private readonly string _testRoot;
    private readonly DosFileSystem _fs;
    private bool _disposed;

    public DosFileSystemTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"BatTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
        _fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = _testRoot });
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing && Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    // ── GetNativePath ────────────────────────────────────────────────────────

    [TestMethod]
    public void GetNativePath_RootPath_ReturnsRoot()
    {
        Assert.AreEqual(_testRoot, _fs.GetNativePath('Z', []));
    }

    [TestMethod]
    public void GetNativePath_WithSegments_CombinesCorrectly()
    {
        var expected = Path.Combine(_testRoot, "Users", "test.txt");
        Assert.AreEqual(expected, _fs.GetNativePath('Z', ["Users", "test.txt"]));
    }

    [TestMethod]
    public void GetNativePath_UnmappedDrive_ReturnsNonExistentPath()
    {
        var result = _fs.GetNativePath('X', []);
        Assert.IsFalse(Directory.Exists(result), "Unmapped drive must not resolve to an existing directory");
    }

    // ── FileExists / DirectoryExists ─────────────────────────────────────────

    [TestMethod]
    public void FileExists_ExistingFile_ReturnsTrue()
    {
        File.WriteAllText(Path.Combine(_testRoot, "test.txt"), "content");
        Assert.IsTrue(_fs.FileExists('Z', ["test.txt"]));
    }

    [TestMethod]
    public void FileExists_NonExisting_ReturnsFalse()
    {
        Assert.IsFalse(_fs.FileExists('Z', ["notfound.txt"]));
    }

    [TestMethod]
    public void DirectoryExists_ExistingDir_ReturnsTrue()
    {
        Directory.CreateDirectory(Path.Combine(_testRoot, "testdir"));
        Assert.IsTrue(_fs.DirectoryExists('Z', ["testdir"]));
    }

    [TestMethod]
    public void DirectoryExists_NonExisting_ReturnsFalse()
    {
        Assert.IsFalse(_fs.DirectoryExists('Z', ["nosuchdir"]));
    }

    // ── CreateDirectory / DeleteDirectory ────────────────────────────────────

    [TestMethod]
    public void CreateDirectory_CreatesDirectory()
    {
        _fs.CreateDirectory('Z', ["newdir"]);
        Assert.IsTrue(Directory.Exists(Path.Combine(_testRoot, "newdir")));
    }

    [TestMethod]
    public void CreateDirectory_Nested_CreatesParents()
    {
        _fs.CreateDirectory('Z', ["parent", "child", "grandchild"]);
        Assert.IsTrue(Directory.Exists(Path.Combine(_testRoot, "parent", "child", "grandchild")));
    }

    [TestMethod]
    public void DeleteDirectory_RemovesDirectory()
    {
        var dir = Path.Combine(_testRoot, "todelete");
        Directory.CreateDirectory(dir);
        _fs.DeleteDirectory('Z', ["todelete"], recursive: false);
        Assert.IsFalse(Directory.Exists(dir));
    }

    [TestMethod]
    public void DeleteDirectory_Recursive_RemovesContents()
    {
        var dir = Path.Combine(_testRoot, "parent");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "child.txt"), "");
        _fs.DeleteDirectory('Z', ["parent"], recursive: true);
        Assert.IsFalse(Directory.Exists(dir));
    }

    // ── EnumerateEntries ─────────────────────────────────────────────────────

    [TestMethod]
    public void EnumerateEntries_ReturnsFilesAndDirs()
    {
        if (!OperatingSystem.IsWindows()) return;

        Directory.CreateDirectory(Path.Combine(_testRoot, "dir1"));
        File.WriteAllText(Path.Combine(_testRoot, "file1.txt"), "");
        File.WriteAllText(Path.Combine(_testRoot, "file2.log"), "");

        var entries = _fs.EnumerateEntries('Z', [], "*").ToList();

        Assert.IsTrue(entries.Any(e => e.Name == "dir1" && e.IsDirectory));
        Assert.IsTrue(entries.Any(e => e.Name == "file1.txt" && !e.IsDirectory));
        Assert.IsTrue(entries.Any(e => e.Name == "file2.log" && !e.IsDirectory));
    }

    [TestMethod]
    public void EnumerateEntries_Wildcard_Filters()
    {
        if (!OperatingSystem.IsWindows()) return;

        File.WriteAllText(Path.Combine(_testRoot, "test.txt"), "");
        File.WriteAllText(Path.Combine(_testRoot, "test.log"), "");
        File.WriteAllText(Path.Combine(_testRoot, "other.txt"), "");

        var entries = _fs.EnumerateEntries('Z', [], "test.*").ToList();

        Assert.AreEqual(2, entries.Count);
        Assert.IsTrue(entries.Any(e => e.Name == "test.txt"));
        Assert.IsTrue(entries.Any(e => e.Name == "test.log"));
    }

    [TestMethod]
    public void EnumerateEntries_NonExistingDir_ReturnsEmpty()
    {
        if (!OperatingSystem.IsWindows()) return;
        var entries = _fs.EnumerateEntries('Z', ["nosuchdir"], "*").ToList();
        Assert.AreEqual(0, entries.Count);
    }

    [TestMethod]
    public void EnumerateEntries_LongFileName_ReturnsShortName()
    {
        if (!OperatingSystem.IsWindows()) return;

        var longName = "This is a very long file name that definitely exceeds 8.3 format.txt";
        File.WriteAllText(Path.Combine(_testRoot, longName), "test");

        var entries = _fs.EnumerateEntries('Z', [], "*").ToList();
        var entry = entries.Single(e => e.Name == longName);

        Assert.IsTrue(entry.ShortName.Length > 0, "Long filename should have a short name");
        Assert.IsTrue(entry.ShortName.Length <= 12, $"Short name should be 8.3 format, got: {entry.ShortName}");
        Assert.IsTrue(entry.ShortName.Contains('~'), $"Short name should contain tilde, got: {entry.ShortName}");
    }

    [TestMethod]
    public void EnumerateEntries_ShortFileName_ShortNameMayBeEmpty()
    {
        if (!OperatingSystem.IsWindows()) return;

        File.WriteAllText(Path.Combine(_testRoot, "short.tx"), "test");

        var entries = _fs.EnumerateEntries('Z', [], "short.tx").ToList();
        var entry = entries.Single();

        Assert.AreEqual("short.tx", entry.Name);
    }

    [TestMethod]
    public void EnumerateEntries_MultipleFiles_AllHaveShortNames()
    {
        if (!OperatingSystem.IsWindows()) return;

        File.WriteAllText(Path.Combine(_testRoot, "VeryLongFileName1.txt"), "");
        File.WriteAllText(Path.Combine(_testRoot, "AnotherVeryLongFileName2.txt"), "");
        File.WriteAllText(Path.Combine(_testRoot, "YetAnotherExtremelyLongFileName3.txt"), "");

        var entries = _fs.EnumerateEntries('Z', [], "*.txt").ToList();

        Assert.AreEqual(3, entries.Count);
        foreach (var entry in entries)
        {
            Assert.IsTrue(entry.ShortName.Length > 0, $"{entry.Name} should have a short name");
        }
    }

    // ── File I/O ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void ReadAllText_ReturnsContent()
    {
        File.WriteAllText(Path.Combine(_testRoot, "content.txt"), "Hello World");
        Assert.AreEqual("Hello World", _fs.ReadAllText('Z', ["content.txt"]));
    }

    [TestMethod]
    public void WriteAllText_WritesContent()
    {
        _fs.WriteAllText('Z', ["written.txt"], "written content");
        Assert.AreEqual("written content", File.ReadAllText(Path.Combine(_testRoot, "written.txt")));
    }

    [TestMethod]
    public void OpenRead_ReturnsReadableStream()
    {
        File.WriteAllText(Path.Combine(_testRoot, "stream.txt"), "data");
        using var stream = _fs.OpenRead('Z', ["stream.txt"]);
        using var reader = new StreamReader(stream);
        Assert.AreEqual("data", reader.ReadToEnd());
    }

    [TestMethod]
    public void OpenWrite_Append_AppendsToFile()
    {
        File.WriteAllText(Path.Combine(_testRoot, "append.txt"), "first");
        using (var stream = _fs.OpenWrite('Z', ["append.txt"], append: true))
        using (var writer = new StreamWriter(stream))
            writer.Write("second");
        Assert.AreEqual("firstsecond", File.ReadAllText(Path.Combine(_testRoot, "append.txt")));
    }

    // ── DeleteFile / CopyFile / MoveFile / RenameFile ─────────────────────────

    [TestMethod]
    public void DeleteFile_RemovesFile()
    {
        var file = Path.Combine(_testRoot, "delete_me.txt");
        File.WriteAllText(file, "");
        _fs.DeleteFile('Z', ["delete_me.txt"]);
        Assert.IsFalse(File.Exists(file));
    }

    [TestMethod]
    public void CopyFile_CopiesContent()
    {
        File.WriteAllText(Path.Combine(_testRoot, "source.txt"), "test content");
        _fs.CopyFile('Z', ["source.txt"], 'Z', ["dest.txt"], overwrite: false);
        Assert.AreEqual("test content", File.ReadAllText(Path.Combine(_testRoot, "dest.txt")));
    }

    [TestMethod]
    public void MoveFile_MovesFile()
    {
        File.WriteAllText(Path.Combine(_testRoot, "move_src.txt"), "moving");
        _fs.MoveFile('Z', ["move_src.txt"], 'Z', ["move_dst.txt"]);
        Assert.IsFalse(File.Exists(Path.Combine(_testRoot, "move_src.txt")));
        Assert.AreEqual("moving", File.ReadAllText(Path.Combine(_testRoot, "move_dst.txt")));
    }

    [TestMethod]
    public void RenameFile_RenamesInPlace()
    {
        File.WriteAllText(Path.Combine(_testRoot, "old.txt"), "rename");
        _fs.RenameFile('Z', ["old.txt"], "new.txt");
        Assert.IsFalse(File.Exists(Path.Combine(_testRoot, "old.txt")));
        Assert.AreEqual("rename", File.ReadAllText(Path.Combine(_testRoot, "new.txt")));
    }

    // ── Metadata ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void GetFileSize_ReturnsCorrectSize()
    {
        File.WriteAllText(Path.Combine(_testRoot, "sized.txt"), "12345");
        var size = _fs.GetFileSize('Z', ["sized.txt"]);
        Assert.IsTrue(size > 0);
    }

    [TestMethod]
    public void GetLastWriteTime_ReturnsRecentTime()
    {
        File.WriteAllText(Path.Combine(_testRoot, "timestamped.txt"), "");
        var t = _fs.GetLastWriteTime('Z', ["timestamped.txt"]);
        Assert.IsTrue(t > DateTime.Now.AddMinutes(-1));
    }

    [TestMethod]
    public void GetAttributes_SetAttributes_RoundTrips()
    {
        var file = Path.Combine(_testRoot, "attr.txt");
        File.WriteAllText(file, "");
        _fs.SetAttributes('Z', ["attr.txt"], FileAttributes.ReadOnly);
        var attrs = _fs.GetAttributes('Z', ["attr.txt"]);
        Assert.IsTrue(attrs.HasFlag(FileAttributes.ReadOnly));
        // Clean up: remove ReadOnly so Dispose can delete the temp dir
        _fs.SetAttributes('Z', ["attr.txt"], FileAttributes.Normal);
    }

    // ── Default constructor maps actual drives ────────────────────────────────

    [TestMethod]
    public void DefaultConstructor_ZDriveExists()
    {
        // Only run if the test machine has a C: drive (true on any Windows CI)
        if (!DriveInfo.GetDrives().Any(d => char.ToUpperInvariant(d.Name[0]) == 'C'))
            Assert.Inconclusive("No C: drive on this machine.");

        var defaultFs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        Assert.IsTrue(defaultFs.HasDrive('Z'), "C: should be mapped to Z:");
        Assert.IsFalse(defaultFs.HasDrive('C'), "C: should NOT be directly accessible");
    }
}
#endif
