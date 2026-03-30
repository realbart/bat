using Bat.Context;

namespace Bat.UnitTests;

[TestClass]
public class UxFileSystemTests : IDisposable
{
    private readonly string _testRoot;
    private readonly UxFileSystemAdapter _fs;
    private bool _disposed;

    public UxFileSystemTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"BatUxTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
        _fs = new UxFileSystemAdapter(new Dictionary<char, string> { ['Z'] = _testRoot });
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
    public void GetNativePath_WithSegments_JoinsWithForwardSlash()
    {
        var expected = _testRoot.TrimEnd('/') + "/Users/test.txt";
        Assert.AreEqual(expected, _fs.GetNativePath('Z', ["Users", "test.txt"]));
    }

    [TestMethod]
    public void GetNativePath_UnmappedDrive_ReturnsFallbackPath()
    {
        var result = _fs.GetNativePath('Q', []);
        Assert.AreEqual("/q:", result);
    }

    [TestMethod]
    public void GetFullPathDisplayName_ShowsWindowsStylePath()
    {
        var display = _fs.GetFullPathDisplayName('Z', ["Projects", "bat"]);
        Assert.AreEqual(@"Z:\Projects\bat", display);
    }

    [TestMethod]
    public void HasDrive_MappedDrive_ReturnsTrue()
    {
        Assert.IsTrue(_fs.HasDrive('Z'));
        Assert.IsFalse(_fs.HasDrive('Q'));
    }

    [TestMethod]
    public void HasDrive_CaseInsensitive()
    {
        Assert.IsTrue(_fs.HasDrive('z'));
        Assert.IsTrue(_fs.HasDrive('Z'));
    }

    // ── Case-insensitive lookup ──────────────────────────────────────────────

    [TestMethod]
    public void FileExists_CaseInsensitive_FindsFile()
    {
        File.WriteAllText(Path.Combine(_testRoot, "Hello.txt"), "test");
        Assert.IsTrue(_fs.FileExists('Z', ["Hello.txt"]));
        Assert.IsTrue(_fs.FileExists('Z', ["hello.txt"]));
        Assert.IsTrue(_fs.FileExists('Z', ["HELLO.TXT"]));
    }

    [TestMethod]
    public void DirectoryExists_CaseInsensitive_FindsDir()
    {
        Directory.CreateDirectory(Path.Combine(_testRoot, "MyDir"));
        Assert.IsTrue(_fs.DirectoryExists('Z', ["MyDir"]));
        Assert.IsTrue(_fs.DirectoryExists('Z', ["mydir"]));
        Assert.IsTrue(_fs.DirectoryExists('Z', ["MYDIR"]));
    }

    [TestMethod]
    public void ReadAllText_CaseInsensitive_FindsFile()
    {
        File.WriteAllText(Path.Combine(_testRoot, "Data.txt"), "content");
        Assert.AreEqual("content", _fs.ReadAllText('Z', ["data.txt"]));
    }

    // On case-sensitive filesystems (ext4), Hallo.txt and hallo.txt are two
    // separate files.  CMD can never encounter this (NTFS is case-insensitive).
    // Bat's rule: exact match wins; if no exact match, first case-insensitive hit.
    [TestMethod]
    public void FileExists_ExactMatchWins_WhenAmbiguousCaseExists()
    {
        File.WriteAllText(Path.Combine(_testRoot, "Hallo.txt"), "upper");
        File.WriteAllText(Path.Combine(_testRoot, "hallo.txt"), "lower");

        // Detect whether the filesystem is truly case-sensitive
        var bothExist = File.Exists(Path.Combine(_testRoot, "Hallo.txt"))
            && File.ReadAllText(Path.Combine(_testRoot, "Hallo.txt")) == "upper";
        if (!bothExist)
        {
            // NTFS: both names point to the same file — only test case-insensitive lookup
            Assert.IsTrue(_fs.FileExists('Z', ["HALLO.TXT"]));
            return;
        }

        // Case-sensitive FS (ext4): exact matches find their specific file
        Assert.AreEqual("upper", _fs.ReadAllText('Z', ["Hallo.txt"]));
        Assert.AreEqual("lower", _fs.ReadAllText('Z', ["hallo.txt"]));

        // Non-exact case still finds *something* (which one is filesystem-dependent)
        Assert.IsTrue(_fs.FileExists('Z', ["HALLO.TXT"]));
    }

    // ── Standard IFileSystem contract (same as DosFileSystemTests) ───────────

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
        Directory.CreateDirectory(Path.Combine(_testRoot, "todelete"));
        _fs.DeleteDirectory('Z', ["todelete"], recursive: false);
        Assert.IsFalse(Directory.Exists(Path.Combine(_testRoot, "todelete")));
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

    [TestMethod]
    public void EnumerateEntries_ReturnsFilesAndDirs()
    {
        Directory.CreateDirectory(Path.Combine(_testRoot, "dir1"));
        File.WriteAllText(Path.Combine(_testRoot, "file1.txt"), "");

        var entries = _fs.EnumerateEntries('Z', [], "*").ToList();

        Assert.IsTrue(entries.Any(e => e.Name == "dir1" && e.IsDirectory));
        Assert.IsTrue(entries.Any(e => e.Name == "file1.txt" && !e.IsDirectory));
    }

    [TestMethod]
    public void EnumerateEntries_Wildcard_Filters()
    {
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
        var entries = _fs.EnumerateEntries('Z', ["nosuchdir"], "*").ToList();
        Assert.AreEqual(0, entries.Count);
    }

    [TestMethod]
    public void EnumerateEntries_ShortName_AlwaysEmpty()
    {
        File.WriteAllText(Path.Combine(_testRoot, "VeryLongFileName.txt"), "");
        var entries = _fs.EnumerateEntries('Z', [], "*").ToList();
        Assert.IsTrue(entries.All(e => e.ShortName == ""));
    }

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

    [TestMethod]
    public void DeleteFile_RemovesFile()
    {
        File.WriteAllText(Path.Combine(_testRoot, "delete_me.txt"), "");
        _fs.DeleteFile('Z', ["delete_me.txt"]);
        Assert.IsFalse(File.Exists(Path.Combine(_testRoot, "delete_me.txt")));
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

    [TestMethod]
    public void GetFileSize_ReturnsCorrectSize()
    {
        File.WriteAllText(Path.Combine(_testRoot, "sized.txt"), "12345");
        Assert.IsTrue(_fs.GetFileSize('Z', ["sized.txt"]) > 0);
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
        File.WriteAllText(Path.Combine(_testRoot, "attr.txt"), "");
        _fs.SetAttributes('Z', ["attr.txt"], FileAttributes.ReadOnly);
        var attrs = _fs.GetAttributes('Z', ["attr.txt"]);
        Assert.IsTrue(attrs.HasFlag(FileAttributes.ReadOnly));
        _fs.SetAttributes('Z', ["attr.txt"], FileAttributes.Normal);
    }

    // ── Multi-drive mapping ──────────────────────────────────────────────────

    [TestMethod]
    public void MultipleDrives_EachMapsToOwnRoot()
    {
        var root2 = Path.Combine(Path.GetTempPath(), $"BatUxTest2_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root2);
        try
        {
            var fs = new UxFileSystemAdapter(new Dictionary<char, string>
            {
                ['C'] = _testRoot,
                ['D'] = root2
            });
            File.WriteAllText(Path.Combine(_testRoot, "c.txt"), "");
            File.WriteAllText(Path.Combine(root2, "d.txt"), "");

            Assert.IsTrue(fs.FileExists('C', ["c.txt"]));
            Assert.IsFalse(fs.FileExists('C', ["d.txt"]));
            Assert.IsTrue(fs.FileExists('D', ["d.txt"]));
            Assert.IsFalse(fs.FileExists('D', ["c.txt"]));
        }
        finally
        {
            Directory.Delete(root2, recursive: true);
        }
    }
}
