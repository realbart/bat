#if WINDOWS
using Bat.Context;
using BatD.Context.Dos;

namespace Bat.UnitTests;

[TestClass]
public class PathTranslatorTests
{
    [TestMethod]
    public async Task TranslateHostPathToBat_SingleMapping_Translates()
    {
        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var hostPath = @"C:\Windows\System32;C:\Program Files";
        var result = await BatD.Files.PathTranslator.TranslateHostPathToBat(hostPath, fs);

        Assert.AreEqual(@"Z:\Windows\System32;Z:\Program Files", result);
    }

    [TestMethod]
    public async Task TranslateHostPathToBat_MultipleMappings_UsesFirstAvailable()
    {
        var fs = new DosFileSystem(new Dictionary<char, string>
        {
            ['Y'] = @"C:\",
            ['Z'] = @"D:\"
        });

        var hostPath = @"C:\Windows;D:\Tools";
        var result = await BatD.Files.PathTranslator.TranslateHostPathToBat(hostPath, fs);

        Assert.AreEqual(@"Y:\Windows;Z:\Tools", result);
    }

    [TestMethod]
    public async Task TranslateHostPathToBat_NoMapping_Omits()
    {
        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var hostPath = @"C:\Windows;E:\External";
        var result = await BatD.Files.PathTranslator.TranslateHostPathToBat(hostPath, fs);

        Assert.AreEqual(@"Z:\Windows", result);
    }

    [TestMethod]
    public async Task TranslateBatPathToHost_SimpleMapping_Translates()
    {
        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var batPath = @"Z:\Windows\System32\cmd.exe";
        var result = await BatD.Files.PathTranslator.TranslateBatPathToHost(batPath, fs);

        Assert.AreEqual(@"C:\Windows\System32\cmd.exe", result);
    }

    [TestMethod]
    public async Task TranslateBatPathToHost_RootPath_Translates()
    {
        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var batPath = @"Z:\";
        var result = await BatD.Files.PathTranslator.TranslateBatPathToHost(batPath, fs);

        Assert.AreEqual(@"C:\", result);
    }
}
#endif
