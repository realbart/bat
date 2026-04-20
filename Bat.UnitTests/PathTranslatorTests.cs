#if WINDOWS
using Bat.Context;
using Bat.Context.Dos;

namespace Bat.UnitTests;

[TestClass]
public class PathTranslatorTests
{
    [TestMethod]
    public void TranslateHostPathToBat_SingleMapping_Translates()
    {
        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var hostPath = @"C:\Windows\System32;C:\Program Files";
        var result = PathTranslator.TranslateHostPathToBat(hostPath, fs);

        Assert.AreEqual(@"Z:\Windows\System32;Z:\Program Files", result);
    }

    [TestMethod]
    public void TranslateHostPathToBat_MultipleMappings_UsesFirstAvailable()
    {
        var fs = new DosFileSystem(new Dictionary<char, string>
        {
            ['Y'] = @"C:\",
            ['Z'] = @"D:\"
        });

        var hostPath = @"C:\Windows;D:\Tools";
        var result = PathTranslator.TranslateHostPathToBat(hostPath, fs);

        Assert.AreEqual(@"Y:\Windows;Z:\Tools", result);
    }

    [TestMethod]
    public void TranslateHostPathToBat_NoMapping_Omits()
    {
        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var hostPath = @"C:\Windows;E:\External";
        var result = PathTranslator.TranslateHostPathToBat(hostPath, fs);

        Assert.AreEqual(@"Z:\Windows", result);
    }

    [TestMethod]
    public void TranslateBatPathToHost_SimpleMapping_Translates()
    {
        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var batPath = @"Z:\Windows\System32\cmd.exe";
        var result = PathTranslator.TranslateBatPathToHost(batPath, fs);

        Assert.AreEqual(@"C:\Windows\System32\cmd.exe", result);
    }

    [TestMethod]
    public void TranslateBatPathToHost_RootPath_Translates()
    {
        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var batPath = @"Z:\";
        var result = PathTranslator.TranslateBatPathToHost(batPath, fs);

        Assert.AreEqual(@"C:\", result);
    }
}
#endif
