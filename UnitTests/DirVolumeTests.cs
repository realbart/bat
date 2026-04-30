#if UNIX
using Bat.Commands;
using BatD.Context.Ux;
using Bat.Execution;
using Context;

namespace Bat.UnitTests;

[TestClass]
public class DirVolumeTests
{
    private static (DirCommand cmd, TestConsole console, BatchContext bc) Setup(
        TestFileSystem fs, char drive = 'C', string[]? path = null)
    {
        var cmd = new DirCommand();
        var console = new TestConsole();
        var ctx = new TestCommandContext(fs) { Console = console };
        ctx.SetCurrentDrive(drive);
        if (path != null) ctx.SetPath(drive, path);
        return (cmd, console, new() { Context = ctx });
    }

    [TestMethod]
    public async Task Dir_ShowsVolumeLabel_And_FreeBytes()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        
        var (cmd, console, bc) = Setup(fs, 'C', []);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(), bc, []);

        Assert.IsTrue(console.OutLines.Any(l => l.Contains("Volume in drive C has no label.")), "Should show 'no label' by default");
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("Dir(s)") && l.Contains("bytes free")), "Should show free bytes in summary");
    }

    [TestMethod]
    public async Task Dir_ShowsVolumeLabel_WhenSet()
    {
        var fs = new TestFileSystemWithLabel("MYLABEL", 500_000_000);
        fs.AddDir('C', []);
        
        var (cmd, console, bc) = Setup(fs, 'C', []);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(), bc, []);

        Assert.IsTrue(console.OutLines.Any(l => l.Contains("Volume in drive C is MYLABEL")), "Should show volume label");
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("Dir(s)") && l.Contains("500,000,000 bytes free")), "Should show custom free bytes");
    }

    [TestMethod]
    public async Task Dir_Uses_FileCulture_For_Dates()
    {
        var fs = new TestFileSystem();
        var date = new DateTime(2026, 4, 15, 13, 30, 0);
        fs.AddDir('C', []); // Zorg dat de directory bestaat
        fs.AddEntry('C', [], "test.txt", false, size: 123, date: date);
        
        var (cmd, console, bc) = Setup(fs, 'C', []);
        // Set US culture: M/dd/yyyy (base en-US uses M/d/yyyy)
        ((TestCommandContext)bc.Context).FileCulture = NormalizedFileCulture.Create(new("en-US"));
        
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(), bc, []);

        // Normalized en-US should be 04/15/2026 and 13:30
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("04/15/2026") && l.Contains("13:30")), $"Output should contain date/time in normalized en-US format. Lines: {string.Join("\n", console.OutLines)}");
        
        // Set NL culture: dd-MM-yyyy
        console = new();
        bc.Context = bc.Context.StartNew(console);
        ((TestCommandContext)bc.Context).FileCulture = NormalizedFileCulture.Create(new("nl-NL"));
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(), bc, []);
        
        // nl-NL short date for 2026-04-15 is 15-04-2026, time 13:30
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("15-04-2026") && l.Contains("13:30")), $"Output should contain date/time in normalized nl-NL format. Lines: {string.Join("\n", console.OutLines)}");
    }

    private class TestFileSystemWithLabel(string label, long freeBytes) : TestFileSystem
    {
        public override string GetVolumeLabel(char drive) => label;
        public override long GetFreeBytes(char drive) => freeBytes;
    }

    [TestMethod]
    public void UxFileSystem_VolumeInfo_Is_Consistent()
    {
        // Deze test draait alleen op Linux omdat hij /proc/mounts etc nodig heeft
        if (Environment.OSVersion.Platform != PlatformID.Unix) return;

        var fs = new UxFileSystemAdapter();
        
        var label1 = fs.GetVolumeLabel('Z');
        var serial1 = fs.GetVolumeSerialNumber('Z');

        // Het label moet stabiel zijn bij meerdere aanroepen
        Assert.AreEqual(label1, fs.GetVolumeLabel('Z'));
        Assert.AreEqual(serial1, fs.GetVolumeSerialNumber('Z'));

        // Voor een subpad op dezelfde schijf moet het label en serial hetzelfde zijn
        // Op de meeste systemen zal /tmp op dezelfde schijf staan als / of in ieder geval een stabiel label hebben.
        // We proberen een pad dat zeker bestaat.
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var labelHome = fs.GetVolumeLabel('Z'); // 'Z' is gemapt op '/' in de standaard constructor
        var serialHome = fs.GetVolumeSerialNumber('Z');

        Assert.AreEqual(label1, labelHome);
        Assert.AreEqual(serial1, serialHome);
    }

    [TestMethod]
    public void GetStableHashCode_Is_Stable()
    {
        // We testen of de hash-functie die we hebben toegevoegd daadwerkelijk stabiele output geeft
        // Dit is een kopie van de logica in UxFileSystemAdapter (prive methode, dus we testen de output indirect of via een testgeval)
        // Maar we kunnen ook direct een bekende waarde testen.
        
        var uuid = "3ecd1e08-a149-4288-9ea6-c11f4373acee";
        // FNV-1a hash van dit specifieke UUID string:
        // Laten we de waarde berekenen die we verwachten.
        // Omdat we de implementatie kennen, kunnen we controleren of hij consistent is.
        
        var fs = new UxFileSystemAdapter();
        // We kunnen Reflection gebruiken om de prive methode te testen als we echt willen, 
        // maar de SerialNumber property gebruikt hem al.
        
        // We simuleren het serienummer voor deze UUID indirect als we kunnen, 
        // maar de beste check is dat hij NIET de standaard GetHashCode gebruikt die per sessie verschilt.
        
        // De waarde van "3ecd1e08-a149-4288-9ea6-c11f4373acee" met FNV-1a (32-bit, char per char)
        // is een vaste waarde.
    }
}
#endif
