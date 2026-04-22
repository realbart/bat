using Bat.Commands;

namespace Bat.UnitTests;

[TestClass]
public class TerminalDetectorTests
{
    [TestMethod]
    public void FindTemplate_KnownTerminal_ReturnsTemplate()
    {
        Assert.AreEqual("konsole -e {cmd}", TerminalDetector.FindTemplate("konsole"));
        Assert.AreEqual("xterm -e {cmd}", TerminalDetector.FindTemplate("xterm"));
        Assert.AreEqual("gnome-terminal -- {cmd}", TerminalDetector.FindTemplate("gnome-terminal-server"));
    }

    [TestMethod]
    public void FindTemplate_CaseInsensitive()
    {
        Assert.AreEqual("konsole -e {cmd}", TerminalDetector.FindTemplate("Konsole"));
        Assert.AreEqual("alacritty -e {cmd}", TerminalDetector.FindTemplate("ALACRITTY"));
    }

    [TestMethod]
    public void FindTemplate_UnknownTerminal_ReturnsNull()
    {
        Assert.IsNull(TerminalDetector.FindTemplate("bash"));
        Assert.IsNull(TerminalDetector.FindTemplate("fish"));
        Assert.IsNull(TerminalDetector.FindTemplate("zsh"));
    }

    [TestMethod]
    public void BuildLaunchCommand_SimpleTemplate_SplitsCorrectly()
    {
        var (exe, args) = TerminalDetector.BuildLaunchCommand("xterm -e {cmd}", "/usr/bin/bat");
        Assert.AreEqual("xterm", exe);
        Assert.AreEqual("-e /usr/bin/bat", args);
    }

    [TestMethod]
    public void BuildLaunchCommand_GnomeTerminal_UsesDoubleDash()
    {
        var (exe, args) = TerminalDetector.BuildLaunchCommand("gnome-terminal -- {cmd}", "/usr/bin/bat /C echo hi");
        Assert.AreEqual("gnome-terminal", exe);
        Assert.AreEqual("-- /usr/bin/bat /C echo hi", args);
    }

    [TestMethod]
    public void BuildLaunchCommand_CommandWithArguments_PreservesAll()
    {
        var (exe, args) = TerminalDetector.BuildLaunchCommand("konsole -e {cmd}", "/opt/bat/batd --arg1 --arg2");
        Assert.AreEqual("konsole", exe);
        Assert.AreEqual("-e /opt/bat/batd --arg1 --arg2", args);
    }

    [TestMethod]
    public void KnownTerminals_HasExpectedEntries()
    {
        Assert.IsTrue(TerminalDetector.KnownTerminals.Length >= 7);

        var names = TerminalDetector.KnownTerminals.Select(t => t.ProcessName).ToList();
        Assert.IsTrue(names.Contains("konsole"));
        Assert.IsTrue(names.Contains("xterm"));
        Assert.IsTrue(names.Contains("alacritty"));
        Assert.IsTrue(names.Contains("gnome-terminal-server"));
        Assert.IsTrue(names.Contains("x-terminal-emulator"));
    }

    [TestMethod]
    public void KnownTerminals_AllTemplatesContainCmdPlaceholder()
    {
        foreach (var (name, template) in TerminalDetector.KnownTerminals)
        {
            Assert.IsTrue(template.Contains("{cmd}"),
                $"Template for {name} missing {{cmd}} placeholder: {template}");
        }
    }
}
