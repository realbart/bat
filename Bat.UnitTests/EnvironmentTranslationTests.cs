using Bat.Context;

namespace Bat.UnitTests;

[TestClass]
public class EnvironmentTranslationTests
{
    // ── DosContext ────────────────────────────────────────────────────────────

    [TestMethod]
    public void DosContext_NonPathVariable_KeptUnchanged()
    {
        if (!OperatingSystem.IsWindows()) return;

        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var ctx = new DosContext(fs);

        Assert.IsTrue(ctx.EnvironmentVariables.ContainsKey("COMPUTERNAME"),
            "Non-path variable COMPUTERNAME should be kept");
    }

    [TestMethod]
    public void DosContext_AbsolutePathInScope_Translated()
    {
        if (!OperatingSystem.IsWindows()) return;

        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var ctx = new DosContext(fs);

        // APPDATA is always C:\Users\<user>\AppData\Roaming on Windows
        Assert.IsTrue(ctx.EnvironmentVariables.TryGetValue("APPDATA", out var appData),
            "APPDATA should exist");
        Assert.IsTrue(appData.StartsWith(@"Z:\"),
            $"APPDATA should be translated to Z: drive, was: {appData}");
    }

    [TestMethod]
    public void DosContext_AbsolutePathOutOfScope_Removed()
    {
        if (!OperatingSystem.IsWindows()) return;

        // Map Z: to a non-existent root — no C:\ paths will match
        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"Q:\nonexistent\" });
        var ctx = new DosContext(fs);

        // APPDATA (C:\Users\...) is not under Q:\, so it should be removed
        Assert.IsFalse(ctx.EnvironmentVariables.ContainsKey("APPDATA"),
            "APPDATA should be removed when its path is not in scope of any mapped drive");
    }

    [TestMethod]
    public void DosContext_PathVariable_AlreadyHandledByBase()
    {
        if (!OperatingSystem.IsWindows()) return;

        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var ctx = new DosContext(fs);

        // PATH is translated by the base class, should exist and use Z:\ prefix
        Assert.IsTrue(ctx.EnvironmentVariables.TryGetValue("PATH", out var path),
            "PATH should exist");
        Assert.IsTrue(path.Contains(@"Z:\"),
            $"PATH should contain translated Z: paths, was: {path}");
    }

    [TestMethod]
    public void DosContext_NonPathExtensions_KeptUnchanged()
    {
        if (!OperatingSystem.IsWindows()) return;

        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var ctx = new DosContext(fs);

        // PATHEXT contains .COM;.EXE;... — no absolute paths, should be kept
        if (ctx.EnvironmentVariables.TryGetValue("PATHEXT", out var pathExt))
        {
            Assert.IsTrue(pathExt.Contains(".EXE"),
                $"PATHEXT should be kept unchanged, was: {pathExt}");
        }
    }

    [TestMethod]
    public void DosContext_PromptVariable_KeptUnchanged()
    {
        if (!OperatingSystem.IsWindows()) return;

        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var ctx = new DosContext(fs);

        Assert.IsTrue(ctx.EnvironmentVariables.TryGetValue("PROMPT", out var prompt),
            "PROMPT should exist");
        Assert.AreEqual("$P$G", prompt);
    }

    [TestMethod]
    public void DosContext_BareDriveReference_Translated()
    {
        if (!OperatingSystem.IsWindows()) return;

        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var ctx = new DosContext(fs);

        // HOMEDRIVE is "C:" on Windows — should become "Z:"
        if (ctx.EnvironmentVariables.TryGetValue("HOMEDRIVE", out var homeDrive))
            Assert.AreEqual("Z:", homeDrive, $"HOMEDRIVE should be translated from C: to Z:");
    }

    [TestMethod]
    public void DosContext_BareDriveOutOfScope_UsesFallback()
    {
        if (!OperatingSystem.IsWindows()) return;

        var fs = new DosFileSystem(new Dictionary<char, string> { ['Y'] = @"Q:\nonexistent\" });
        var ctx = new DosContext(fs);

        // HOMEDRIVE=C: is not under Q:\, so it falls back to Y: (first mapped drive)
        Assert.AreEqual("Y:", ctx.EnvironmentVariables["HOMEDRIVE"]);
        Assert.AreEqual(@"\", ctx.EnvironmentVariables["HOMEPATH"]);
    }

    [TestMethod]
    public void DosContext_RootRelativePath_KeptUnchanged()
    {
        if (!OperatingSystem.IsWindows()) return;

        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var ctx = new DosContext(fs);

        // HOMEPATH is "\Users\kempsb" — root-relative, no drive letter, should be kept
        if (ctx.EnvironmentVariables.TryGetValue("HOMEPATH", out var homePath))
            Assert.IsTrue(homePath.StartsWith(@"\"),
                $"HOMEPATH should be kept as root-relative path, was: {homePath}");
    }

    [TestMethod]
    public void DosContext_SystemRootMappable_UsesMapping()
    {
        if (!OperatingSystem.IsWindows()) return;

        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var ctx = new DosContext(fs);

        Assert.IsTrue(ctx.EnvironmentVariables.TryGetValue("SystemRoot", out var sysRoot));
        Assert.IsTrue(sysRoot.StartsWith("Z:"), $"SystemRoot should start with Z:, was: {sysRoot}");
    }

    [TestMethod]
    public void DosContext_SystemRootNotMappable_UsesFallback()
    {
        if (!OperatingSystem.IsWindows()) return;

        var fs = new DosFileSystem(new Dictionary<char, string> { ['Y'] = @"Q:\nonexistent\" });
        var ctx = new DosContext(fs);

        Assert.AreEqual(@"Y:\Windows", ctx.EnvironmentVariables["SystemRoot"]);
    }

    [TestMethod]
    public void DosContext_SystemDrive_DerivedFromSystemRoot()
    {
        if (!OperatingSystem.IsWindows()) return;

        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var ctx = new DosContext(fs);

        Assert.IsTrue(ctx.EnvironmentVariables.TryGetValue("SystemDrive", out var sysDrive));
        Assert.IsTrue(ctx.EnvironmentVariables.TryGetValue("SystemRoot", out var sysRoot));
        Assert.AreEqual(sysRoot[..2], sysDrive, "SystemDrive should match first 2 chars of SystemRoot");
    }

    [TestMethod]
    public void DosContext_HomeDriveAndPathMappable_UsesMapping()
    {
        if (!OperatingSystem.IsWindows()) return;

        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var ctx = new DosContext(fs);

        Assert.IsTrue(ctx.EnvironmentVariables.TryGetValue("HOMEDRIVE", out var hd));
        Assert.IsTrue(ctx.EnvironmentVariables.TryGetValue("HOMEPATH", out var hp));
        Assert.AreEqual("Z:", hd);
        Assert.IsTrue(hp.StartsWith(@"\"), $"HOMEPATH should be root-relative, was: {hp}");
    }

    [TestMethod]
    public void DosContext_HomeDriveNotMappable_UsesFallback()
    {
        if (!OperatingSystem.IsWindows()) return;

        var fs = new DosFileSystem(new Dictionary<char, string> { ['Y'] = @"Q:\nonexistent\" });
        var ctx = new DosContext(fs);

        Assert.AreEqual("Y:", ctx.EnvironmentVariables["HOMEDRIVE"]);
        Assert.AreEqual(@"\", ctx.EnvironmentVariables["HOMEPATH"]);
    }

    [TestMethod]
    public void DosFileSystem_DefaultConstructor_MapsZDrive()
    {
        var fs = new DosFileSystem();
        Assert.IsTrue(fs.HasDrive('Z'), "Default constructor should map Z: drive");
    }

    [TestMethod]
    public void DosContext_DefaultFileSystem_InitializesSuccessfully()
    {
        if (!OperatingSystem.IsWindows()) return;

        var fs = new DosFileSystem();
        var ctx = new DosContext(fs);

        Assert.IsTrue(ctx.EnvironmentVariables.ContainsKey("SystemRoot"));
        Assert.IsTrue(ctx.EnvironmentVariables.ContainsKey("HOMEDRIVE"));
    }

    [TestMethod]
    public void DosFileSystem_FirstDrive_ReturnsInsertionOrder()
    {
        var fs = new DosFileSystem(new Dictionary<char, string>
        {
            ['Q'] = @"C:\Foo\Bar",
            ['A'] = @"C:\Foo\"
        });

        Assert.AreEqual('Q', fs.FirstDrive(), "First drive should be Q (insertion order), not A (alphabetical)");
    }

    [TestMethod]
    public void DosContext_FallbackDrive_UsesInsertionOrder()
    {
        if (!OperatingSystem.IsWindows()) return;

        var fs = new DosFileSystem(new Dictionary<char, string>
        {
            ['Q'] = @"X:\nonexistent\",
            ['A'] = @"Y:\other\"
        });
        var ctx = new DosContext(fs);

        Assert.AreEqual("Q:", ctx.EnvironmentVariables["HOMEDRIVE"], "HOMEDRIVE should use Q (first in insertion order)");
        Assert.AreEqual(@"Q:\Windows", ctx.EnvironmentVariables["SystemRoot"]);
    }

    // ── PathTranslator (unit-level, no OS dependency) ────────────────────────

    [TestMethod]
    public void TranslateHostPathToBat_SingleAbsolutePath_Translated()
    {
        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var result = PathTranslator.TranslateHostPathToBat(@"C:\Users\kempsb\AppData", fs);
        Assert.AreEqual(@"Z:\Users\kempsb\AppData", result);
    }

    [TestMethod]
    public void TranslateHostPathToBat_SemicolonSeparatedPaths_AllTranslated()
    {
        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var result = PathTranslator.TranslateHostPathToBat(@"C:\Windows;C:\Users", fs);
        Assert.AreEqual(@"Z:\Windows;Z:\Users", result);
    }

    [TestMethod]
    public void TranslateHostPathToBat_MixedInAndOutOfScope_OnlyInScopeKept()
    {
        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var result = PathTranslator.TranslateHostPathToBat(@"C:\Windows;E:\External;C:\Users", fs);
        Assert.AreEqual(@"Z:\Windows;Z:\Users", result);
    }

    [TestMethod]
    public void TranslateHostPathToBat_AllOutOfScope_ReturnsEmpty()
    {
        var fs = new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" });
        var result = PathTranslator.TranslateHostPathToBat(@"E:\External;F:\Other", fs);
        Assert.AreEqual("", result);
    }
}
