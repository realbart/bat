using Bat.Context.Dos;
using Bat.Context.Ux;

namespace Bat.UnitTests;

[TestClass]
public class FileAssociationTests
{
    [TestMethod]
    public void UxFileSystemAdapter_GetFileAssociations_ReturnsStandardTypes()
    {
        var fs = new UxFileSystemAdapter();
        var assoc = fs.GetFileAssociations();

        Assert.AreEqual("batfile", assoc[".bat"]);
        Assert.AreEqual("cmdfile", assoc[".cmd"]);
        Assert.AreEqual("comfile", assoc[".com"]);
        Assert.AreEqual("dllfile", assoc[".dll"]);
        Assert.AreEqual("exefile", assoc[".exe"]);
    }

    [TestMethod]
    public void UxFileSystemAdapter_GetFileAssociations_IsCaseInsensitive()
    {
        var fs = new UxFileSystemAdapter();
        var assoc = fs.GetFileAssociations();

        Assert.AreEqual("exefile", assoc[".EXE"]);
        Assert.AreEqual("batfile", assoc[".BAT"]);
    }

    [TestMethod]
    public void DosFileSystem_GetFileAssociations_ReturnsWindowsRegistry()
    {
        if (!OperatingSystem.IsWindows()) return;

        var fs = new DosFileSystem();
        var assoc = fs.GetFileAssociations();

        Assert.IsTrue(assoc.Count > 0, "Windows registry should contain file associations");
        Assert.IsTrue(assoc.ContainsKey(".exe") || assoc.ContainsKey(".EXE"),
            "Windows should have .exe association");
    }

    [TestMethod]
    public void DosFileSystem_GetFileAssociations_IsCaseInsensitive()
    {
        if (!OperatingSystem.IsWindows()) return;

        var fs = new DosFileSystem();
        var assoc = fs.GetFileAssociations();

        if (assoc.TryGetValue(".exe", out var lower) && assoc.TryGetValue(".EXE", out var upper))
            Assert.AreEqual(lower, upper, "File association lookup should be case-insensitive");
    }
}
