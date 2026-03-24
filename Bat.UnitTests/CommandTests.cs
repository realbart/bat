using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Bat.Commands;
using Bat.Console;
using Bat.Context;
using Bat.Execution;
using Bat.Nodes;
using Bat.Parsing;
using Bat.Tokens;
using Context;

namespace Bat.UnitTests;

[TestClass]
public class EchoCommandTests
{
    private static (EchoCommand cmd, TestConsole console, BatchContext bc, TestCommandContext ctx) Setup(bool echoEnabled = true)
    {
        var cmd = new EchoCommand();
        var console = new TestConsole();
        var ctx = new TestCommandContext { EchoEnabled = echoEnabled };
        var bc = new BatchContext { Console = console, Context = ctx };
        return (cmd, console, bc, ctx);
    }

    [TestMethod]
    public async Task Echo_NoArgs_PrintsCurrentState_On()
    {
        var (cmd, console, bc, _) = Setup(echoEnabled: true);
        await cmd.ExecuteAsync(TestArgs.For<EchoCommand>(), bc, []);
        Assert.AreEqual("ECHO is on.", console.OutLines[0]);
    }

    [TestMethod]
    public async Task Echo_NoArgs_PrintsCurrentState_Off()
    {
        var (cmd, console, bc, _) = Setup(echoEnabled: false);
        await cmd.ExecuteAsync(TestArgs.For<EchoCommand>(), bc, []);
        Assert.AreEqual("ECHO is off.", console.OutLines[0]);
    }

    [TestMethod]
    public async Task Echo_OnArg_SetsEchoEnabled()
    {
        var (cmd, console, bc, ctx) = Setup(echoEnabled: false);
        await cmd.ExecuteAsync(TestArgs.For<EchoCommand>(Token.Text("on")), bc, []);
        Assert.IsTrue(ctx.EchoEnabled);
        Assert.AreEqual(0, console.OutLines.Count);
    }

    [TestMethod]
    public async Task Echo_OffArg_SetsEchoDisabled()
    {
        var (cmd, _, bc, ctx) = Setup(echoEnabled: true);
        await cmd.ExecuteAsync(TestArgs.For<EchoCommand>(Token.Text("off")), bc, []);
        Assert.IsFalse(ctx.EchoEnabled);
    }

    [TestMethod]
    public async Task Echo_Message_PrintsMessage()
    {
        var (cmd, console, bc, _) = Setup();
        await cmd.ExecuteAsync(TestArgs.For<EchoCommand>(Token.Whitespace(" "), Token.Text("hello world")), bc, []);
        Assert.AreEqual("hello world", console.OutLines[0]);
    }

    [TestMethod]
    public async Task Echo_OnArg_CaseInsensitive()
    {
        var (cmd, _, bc, ctx) = Setup(echoEnabled: false);
        await cmd.ExecuteAsync(TestArgs.For<EchoCommand>(Token.Text("ON")), bc, []);
        Assert.IsTrue(ctx.EchoEnabled);
    }
}

[TestClass]
public class ExitCommandTests
{
    private static (ExitCommand cmd, TestConsole console, BatchContext bc, TestCommandContext ctx) Setup()
    {
        var cmd = new ExitCommand();
        var console = new TestConsole();
        var ctx = new TestCommandContext();
        var bc = new BatchContext { Console = console, Context = ctx };
        return (cmd, console, bc, ctx);
    }

    [TestMethod]
    public async Task Exit_NoArgs_ReturnsSentinel()
    {
        var (cmd, _, bc, _) = Setup();
        int result = await cmd.ExecuteAsync(TestArgs.For<ExitCommand>(), bc, []);
        Assert.AreEqual(ExitCommand.ExitSentinel, result);
    }

    [TestMethod]
    public async Task Exit_WithCode_SetsErrorCode()
    {
        var (cmd, _, bc, ctx) = Setup();
        await cmd.ExecuteAsync(TestArgs.For<ExitCommand>(Token.Whitespace(" "), Token.Text("42")), bc, []);
        Assert.AreEqual(42, ctx.ErrorCode);
    }

    [TestMethod]
    public async Task Exit_WithB_InBatch_ReturnsZero()
    {
        var (cmd, _, bc, _) = Setup();
        bc.BatchFilePath = "test.bat";
        int result = await cmd.ExecuteAsync(TestArgs.For<ExitCommand>(Token.Text("/B")), bc, []);
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public async Task Exit_WithB_InRepl_ReturnsSentinel()
    {
        var (cmd, _, bc, _) = Setup();
        int result = await cmd.ExecuteAsync(TestArgs.For<ExitCommand>(Token.Text("/B")), bc, []);
        Assert.AreEqual(ExitCommand.ExitSentinel, result);
    }
}

[TestClass]
public class SetCommandTests
{
    private static (SetCommand cmd, TestConsole console, BatchContext bc, TestCommandContext ctx) Setup(string input = "")
    {
        var cmd = new SetCommand();
        var console = new TestConsole(input);
        var ctx = new TestCommandContext();
        var bc = new BatchContext { Console = console, Context = ctx };
        return (cmd, console, bc, ctx);
    }

    [TestMethod]
    public async Task Set_NoArgs_PrintsAllVarsSorted()
    {
        var (cmd, console, bc, ctx) = Setup();
        ctx.EnvironmentVariables["ZAP"] = "1";
        ctx.EnvironmentVariables["Alpha"] = "2";
        await cmd.ExecuteAsync(TestArgs.For<SetCommand>(), bc, []);
        Assert.AreEqual("Alpha=2", console.OutLines[0]);
        Assert.AreEqual("ZAP=1", console.OutLines[1]);
    }

    [TestMethod]
    public async Task Set_AssignVariable_StoresValue()
    {
        var (cmd, _, bc, ctx) = Setup();
        await cmd.ExecuteAsync(TestArgs.For<SetCommand>(Token.Text("FOO=bar")), bc, []);
        Assert.AreEqual("bar", ctx.EnvironmentVariables["FOO"]);
    }

    [TestMethod]
    public async Task Set_AssignEmpty_RemovesVariable()
    {
        var (cmd, _, bc, ctx) = Setup();
        ctx.EnvironmentVariables["FOO"] = "existing";
        await cmd.ExecuteAsync(TestArgs.For<SetCommand>(Token.Text("FOO=")), bc, []);
        Assert.IsFalse(ctx.EnvironmentVariables.ContainsKey("FOO"));
    }

    [TestMethod]
    public async Task Set_PrefixSearch_DisplaysMatches()
    {
        var (cmd, console, bc, ctx) = Setup();
        ctx.EnvironmentVariables["PATH"] = "/usr/bin";
        ctx.EnvironmentVariables["PATHEXT"] = ".COM;.EXE";
        ctx.EnvironmentVariables["PROMPT"] = "$P$G";
        await cmd.ExecuteAsync(TestArgs.For<SetCommand>(Token.Text("PATH")), bc, []);
        Assert.AreEqual(2, console.OutLines.Count);
    }

    [TestMethod]
    public async Task Set_ArithmeticAdd_ComputesCorrectly()
    {
        var (cmd, console, bc, ctx) = Setup();
        ctx.EnvironmentVariables["A"] = "10";
        await cmd.ExecuteAsync(TestArgs.For<SetCommand>(Token.Text("/A"), Token.Whitespace(" "), Token.Text("A+5")), bc, []);
        // In REPL mode, prints result
        Assert.AreEqual("15", console.OutLines[0]);
    }

    [TestMethod]
    public async Task Set_ArithmeticAssign_StoresResult()
    {
        var (cmd, _, bc, ctx) = Setup();
        await cmd.ExecuteAsync(TestArgs.For<SetCommand>(Token.Text("/A"), Token.Whitespace(" "), Token.Text("X=3*4")), bc, []);
        Assert.AreEqual("12", ctx.EnvironmentVariables["X"]);
    }

    [TestMethod]
    public async Task Set_Prompt_ReadsFromConsole()
    {
        var (cmd, _, bc, ctx) = Setup(input: "hello\n");
        await cmd.ExecuteAsync(TestArgs.For<SetCommand>(Token.Text("/P"), Token.Whitespace(" "), Token.Text("VAR=Enter value: ")), bc, []);
        Assert.AreEqual("hello", ctx.EnvironmentVariables["VAR"]);
    }
}

[TestClass]
public class RemCommandTests
{
    [TestMethod]
    public async Task Rem_AnyArgs_ReturnsZero()
    {
        var cmd = new RemCommand();
        var bc = new BatchContext { Context = new TestCommandContext() };
        int result = await cmd.ExecuteAsync(TestArgs.For<RemCommand>(Token.Text("this is a comment")), bc, []);
        Assert.AreEqual(0, result);
    }
}

/// <summary>Minimal IContext for unit-testing individual commands.</summary>
internal class TestCommandContext(IFileSystem? fileSystem = null) : IContext
{
    private readonly Dictionary<char, string[]> _paths = [];

    public char CurrentDrive { get; private set; } = 'Z';
    public string[] CurrentPath => _paths.TryGetValue(CurrentDrive, out var p) ? p : [];
    public string CurrentPathDisplayName =>
        CurrentPath.Length == 0 ? $"{CurrentDrive}:\\" : $"{CurrentDrive}:\\{string.Join("\\", CurrentPath)}";
    public Dictionary<string, string> EnvironmentVariables { get; } = [];
    public int ErrorCode { get; set; }
    public IFileSystem FileSystem => fileSystem!;
    public object? CurrentBatch { get; set; }
    public bool EchoEnabled { get; set; } = true;
    public bool DelayedExpansion { get; set; }
    public bool ExtensionsEnabled { get; set; } = true;
    public string PromptFormat { get; set; } = "$P$G";
    public Stack<(char Drive, string[] Path)> DirectoryStack { get; } = new();
    public void SetPath(char drive, string[] path) => _paths[drive] = path;
    public void SetCurrentDrive(char drive) => CurrentDrive = drive;
    public string[] GetPathForDrive(char drive) => _paths.TryGetValue(drive, out var p) ? p : [];
}

/// <summary>In-memory IFileSystem for unit tests.</summary>
internal sealed class TestFileSystem : IFileSystem
{
    private readonly HashSet<string> _dirs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<(string Name, bool IsDir, long Size, DateTime Date, FileAttributes Attrs)>> _contents
        = new(StringComparer.OrdinalIgnoreCase);

    public void AddDir(char drive, string[] path) => _dirs.Add(Key(drive, path));

    public void AddEntry(char drive, string[] dir, string name, bool isDir, long size = 100,
        DateTime date = default, FileAttributes attrs = FileAttributes.Normal)
    {
        if (isDir) attrs |= FileAttributes.Directory;
        var key = Key(drive, dir);
        if (!_contents.TryGetValue(key, out var list))
            _contents[key] = list = [];
        list.Add((name, isDir, size, date == default ? new DateTime(2026, 1, 1) : date, attrs));
    }

    private static string Key(char drive, string[] path)
        => path.Length == 0
            ? $"{char.ToUpperInvariant(drive)}:\\"
            : $"{char.ToUpperInvariant(drive)}:\\{string.Join("\\", path)}";

    public string GetFullPathDisplayName(char drive, string[] path) => Key(drive, path);
    public string GetDisplayName(string segment) => segment;
    public string GetNativePath(char drive, string[] path) => Key(drive, path);
    public bool DirectoryExists(char drive, string[] path) => _dirs.Contains(Key(drive, path));
    public bool FileExists(char drive, string[] path) => false;

    public IEnumerable<(string Name, bool IsDirectory)> EnumerateEntries(char drive, string[] path, string pattern)
    {
        if (!_contents.TryGetValue(Key(drive, path), out var list)) yield break;
        foreach (var e in list)
            if (GlobMatch(e.Name, pattern))
                yield return (e.Name, e.IsDir);
    }

    public FileAttributes GetAttributes(char drive, string[] path)
    {
        if (path.Length == 0) return FileAttributes.Directory;
        var dir = Key(drive, path[..^1]);
        var name = path[^1];
        if (_contents.TryGetValue(dir, out var list))
        {
            var entry = list.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
            if (entry.Name != null) return entry.Attrs;
        }
        return FileAttributes.Normal;
    }

    public long GetFileSize(char drive, string[] path)
    {
        if (path.Length == 0) return 0;
        var dir = Key(drive, path[..^1]);
        var name = path[^1];
        if (_contents.TryGetValue(dir, out var list))
        {
            var entry = list.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
            if (entry.Name != null) return entry.Size;
        }
        return 0;
    }

    public DateTime GetLastWriteTime(char drive, string[] path)
    {
        if (path.Length == 0) return DateTime.MinValue;
        var dir = Key(drive, path[..^1]);
        var name = path[^1];
        if (_contents.TryGetValue(dir, out var list))
        {
            var entry = list.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
            if (entry.Name != null) return entry.Date;
        }
        return DateTime.Now;
    }

    private static bool GlobMatch(string name, string pattern)
    {
        if (pattern is "*" or "*.*") return true;
        var regex = "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
        return Regex.IsMatch(name, regex, RegexOptions.IgnoreCase);
    }

    public void CreateDirectory(char d, string[] p) => throw new NotImplementedException();
    public void DeleteFile(char d, string[] p) => throw new NotImplementedException();
    public void DeleteDirectory(char d, string[] p, bool r) => throw new NotImplementedException();
    public Stream OpenRead(char d, string[] p) => throw new NotImplementedException();
    public Stream OpenWrite(char d, string[] p, bool a) => throw new NotImplementedException();
    public string ReadAllText(char d, string[] p) => throw new NotImplementedException();
    public void WriteAllText(char d, string[] p, string c) => throw new NotImplementedException();
    public void CopyFile(char sd, string[] sp, char dd, string[] dp, bool o) => throw new NotImplementedException();
    public void MoveFile(char sd, string[] sp, char dd, string[] dp) => throw new NotImplementedException();
    public void RenameFile(char d, string[] p, string n) => throw new NotImplementedException();
    public void SetAttributes(char d, string[] p, FileAttributes a) => throw new NotImplementedException();
    public uint GetVolumeSerialNumber(char drive) => FileSystem.GetVolumeSerialNumber(Key(drive, []));
}

[TestClass]
public class CdCommandTests
{
    private static (CdCommand cmd, TestConsole console, BatchContext bc, TestCommandContext ctx) Setup(
        TestFileSystem fs, char drive = 'C', string[]? path = null)
    {
        var cmd = new CdCommand();
        var console = new TestConsole();
        var ctx = new TestCommandContext(fs);
        ctx.SetCurrentDrive(drive);
        if (path != null) ctx.SetPath(drive, path);
        return (cmd, console, new BatchContext { Console = console, Context = ctx }, ctx);
    }

    [TestMethod]
    public async Task Cd_NoArgs_PrintsCurrentPath()
    {
        var fs = new TestFileSystem();
        var (cmd, console, bc, _) = Setup(fs, 'C', ["Users", "Bart"]);
        await cmd.ExecuteAsync(TestArgs.For<CdCommand>(), bc, []);
        Assert.AreEqual(@"C:\Users\Bart", console.OutLines[0]);
    }

    [TestMethod]
    public async Task Cd_DriveLetterOnly_ShowsDrivePath()
    {
        var fs = new TestFileSystem();
        var (cmd, console, bc, ctx) = Setup(fs, 'C', ["Users"]);
        ctx.SetPath('D', ["Work"]);
        await cmd.ExecuteAsync(TestArgs.For<CdCommand>(Token.Text("D:")), bc, []);
        Assert.AreEqual(@"D:\Work", console.OutLines[0]);
        Assert.AreEqual('C', ctx.CurrentDrive);
    }

    [TestMethod]
    public async Task Cd_AbsolutePath_ChangesDirectory()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', ["Windows"]);
        var (cmd, _, bc, ctx) = Setup(fs, 'C', ["Users"]);
        await cmd.ExecuteAsync(TestArgs.For<CdCommand>(Token.Text(@"\Windows")), bc, []);
        CollectionAssert.AreEqual(new[] { "Windows" }, ctx.CurrentPath);
    }

    [TestMethod]
    public async Task Cd_RelativePath_ChangesDirectory()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', ["Users", "Bart"]);
        var (cmd, _, bc, ctx) = Setup(fs, 'C', ["Users"]);
        await cmd.ExecuteAsync(TestArgs.For<CdCommand>(Token.Text("Bart")), bc, []);
        CollectionAssert.AreEqual(new[] { "Users", "Bart" }, ctx.CurrentPath);
    }

    [TestMethod]
    public async Task Cd_DoubleDot_GoesUp()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', ["Users"]);
        var (cmd, _, bc, ctx) = Setup(fs, 'C', ["Users", "Bart"]);
        await cmd.ExecuteAsync(TestArgs.For<CdCommand>(Token.Text("..")), bc, []);
        CollectionAssert.AreEqual(new[] { "Users" }, ctx.CurrentPath);
    }

    [TestMethod]
    public async Task Cd_NonExistentPath_ReturnsErrorCode()
    {
        var fs = new TestFileSystem();
        var (cmd, console, bc, _) = Setup(fs, 'C', []);
        int result = await cmd.ExecuteAsync(TestArgs.For<CdCommand>(Token.Text(@"\Missing")), bc, []);
        Assert.AreEqual(1, result);
        Assert.IsTrue(console.ErrLines[0].Contains("cannot find"));
    }

    [TestMethod]
    public async Task Cd_WithSlashD_ChangesDriveAndPath()
    {
        var fs = new TestFileSystem();
        fs.AddDir('D', ["Work"]);
        var (cmd, _, bc, ctx) = Setup(fs, 'C', ["Users"]);
        await cmd.ExecuteAsync(TestArgs.For<CdCommand>(Token.Text("/D"), Token.Whitespace(" "), Token.Text(@"D:\Work")), bc, []);
        Assert.AreEqual('D', ctx.CurrentDrive);
        CollectionAssert.AreEqual(new[] { "Work" }, ctx.CurrentPath);
    }

    [TestMethod]
    public async Task Cd_SlashD_DriveOnly_SwitchesToDrive()
    {
        var fs = new TestFileSystem();
        var (cmd, _, bc, ctx) = Setup(fs, 'C', ["Users"]);
        ctx.SetPath('D', ["Temp"]);
        await cmd.ExecuteAsync(TestArgs.For<CdCommand>(Token.Text("/D"), Token.Whitespace(" "), Token.Text("D:")), bc, []);
        Assert.AreEqual('D', ctx.CurrentDrive);
        CollectionAssert.AreEqual(new[] { "Temp" }, ctx.GetPathForDrive('D'));
    }

    [TestMethod]
    public async Task Cd_SlashQuestion_PrintsHelp()
    {
        var fs = new TestFileSystem();
        var (cmd, console, bc, _) = Setup(fs);
        int result = await cmd.ExecuteAsync(TestArgs.For<CdCommand>(Token.Text("/?") ), bc, []);
        Assert.AreEqual(0, result);
        Assert.IsTrue(console.OutText.Contains("CHDIR"));
        Assert.IsTrue(console.OutText.Contains("/D"));
    }
}

[TestClass]
public class DirCommandTests
{
    private static (DirCommand cmd, TestConsole console, BatchContext bc) Setup(
        TestFileSystem fs, char drive = 'C', string[]? path = null)
    {
        var cmd = new DirCommand();
        var console = new TestConsole();
        var ctx = new TestCommandContext(fs);
        ctx.SetCurrentDrive(drive);
        if (path != null) ctx.SetPath(drive, path);
        return (cmd, console, new BatchContext { Console = console, Context = ctx });
    }

    [TestMethod]
    public async Task Dir_NoArgs_ShowsVolumeHeader()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        var (cmd, console, bc) = Setup(fs, 'C', []);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(), bc, []);
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("Volume")));
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("Directory")));
    }

    [TestMethod]
    public async Task Dir_BareFlag_ShowsNamesWithoutHeader()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "file.txt", false);
        fs.AddEntry('C', [], "subdir", true);
        var (cmd, console, bc) = Setup(fs, 'C', []);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B")), bc, []);
        Assert.IsTrue(console.OutLines.Contains("file.txt"));
        Assert.IsTrue(console.OutLines.Contains("subdir"));
        Assert.IsFalse(console.OutLines.Any(l => l.Contains("Volume")));
    }

    [TestMethod]
    public async Task Dir_AttributeFilter_D_ShowsOnlyDirs()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "file.txt", false);
        fs.AddEntry('C', [], "subdir", true);
        var (cmd, console, bc) = Setup(fs, 'C', []);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/A:D")), bc, []);
        Assert.IsFalse(console.OutLines.Contains("file.txt"));
        Assert.IsTrue(console.OutLines.Contains("subdir"));
    }

    [TestMethod]
    public async Task Dir_AttributeFilter_MinusD_ShowsOnlyFiles()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "file.txt", false);
        fs.AddEntry('C', [], "subdir", true);
        var (cmd, console, bc) = Setup(fs, 'C', []);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/A:-D")), bc, []);
        Assert.IsTrue(console.OutLines.Contains("file.txt"));
        Assert.IsFalse(console.OutLines.Contains("subdir"));
    }

    [TestMethod]
    public async Task Dir_AttributeFilter_H_ShowsOnlyHidden()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "visible.txt", false, attrs: FileAttributes.Normal);
        fs.AddEntry('C', [], "hidden.txt", false, attrs: FileAttributes.Hidden);
        var (cmd, console, bc) = Setup(fs, 'C', []);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/A:H")), bc, []);
        Assert.IsFalse(console.OutLines.Contains("visible.txt"));
        Assert.IsTrue(console.OutLines.Contains("hidden.txt"));
    }

    [TestMethod]
    public async Task Dir_SortByName_AlphabeticalOrder()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "zebra.txt", false);
        fs.AddEntry('C', [], "alpha.txt", false);
        var (cmd, console, bc) = Setup(fs, 'C', []);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/O:N")), bc, []);
        var lines = console.OutLines.ToList();
        int alphaIdx = lines.IndexOf("alpha.txt");
        int zebraIdx = lines.IndexOf("zebra.txt");
        Assert.IsTrue(alphaIdx >= 0 && zebraIdx >= 0 && alphaIdx < zebraIdx);
    }

    [TestMethod]
    public async Task Dir_GroupDirsFirst_DirsBeforeFiles()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "zfile.txt", false);
        fs.AddEntry('C', [], "adir", true);
        var (cmd, console, bc) = Setup(fs, 'C', []);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/O:G")), bc, []);
        var lines = console.OutLines.ToList();
        int dirIdx = lines.IndexOf("adir");
        int fileIdx = lines.IndexOf("zfile.txt");
        Assert.IsTrue(dirIdx >= 0 && fileIdx >= 0 && dirIdx < fileIdx);
    }

    [TestMethod]
    public async Task Dir_SlashQuestion_PrintsHelp()
    {
        var fs = new TestFileSystem();
        var (cmd, console, bc) = Setup(fs, 'C', []);
        int result = await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/?") ), bc, []);
        Assert.AreEqual(0, result);
        Assert.IsTrue(console.OutText.Contains("DIR"));
        Assert.IsTrue(console.OutText.Contains("/A"));
        Assert.IsTrue(console.OutText.Contains("/S"));
    }
    // /B — bare format: no volume header, no summary, just names
    [TestMethod]
    public async Task Dir_SlashB_PrintsOnlyNames()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "file.txt", false, size: 1234);
        fs.AddEntry('C', [], "sub", true);
        var (cmd, console, bc) = Setup(fs, 'C', []);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B")), bc, []);
        Assert.IsTrue(console.OutLines.Contains("file.txt"));
        Assert.IsTrue(console.OutLines.Contains("sub"));
        Assert.IsFalse(console.OutLines.Any(l => l.Contains("Volume")));
        Assert.IsFalse(console.OutLines.Any(l => l.Contains("bytes")));
        Assert.IsFalse(console.OutLines.Any(l => l.Contains("1,234") || l.Contains("1234")));
    }

    // default (/C implied): file sizes use thousand separator
    [TestMethod]
    public async Task Dir_Default_SizeHasThousandSeparator()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "big.txt", false, size: 1234567);
        var (cmd, console, bc) = Setup(fs, 'C', []);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(), bc, []);
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("1,234,567")));
    }

    // /-C: thousand separator disabled
    [TestMethod]
    public async Task Dir_NegateC_SizeHasNoThousandSeparator()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "big.txt", false, size: 1234567);
        var (cmd, console, bc) = Setup(fs, 'C', []);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/-C")), bc, []);
        Assert.IsFalse(console.OutLines.Any(l => l.Contains("1,234,567")));
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("1234567")));
    }
    // CMD: dir -w  uses "-w" as a filename pattern (treats - as a literal, not a flag prefix).
    // Bat: ArgumentSet interprets "-w" as the /W flag, so wide format is activated.
    [TestMethod]
    public async Task Dir_DashW_ActivatesWideFormat()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "alpha.txt", false);
        fs.AddEntry('C', [], "beta.txt", false);
        fs.AddEntry('C', [], "gamma.txt", false);
        var (cmd, console, bc) = Setup(fs, 'C', []);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("-w")), bc, []);
        // Wide format: all three short names fit on a single output line.
        var entryLine = console.OutLines.Single(l =>
            l.Contains("alpha.txt") || l.Contains("beta.txt") || l.Contains("gamma.txt"));
        Assert.IsTrue(entryLine.Contains("alpha.txt"));
        Assert.IsTrue(entryLine.Contains("beta.txt"));
        Assert.IsTrue(entryLine.Contains("gamma.txt"));
    }

    // Column width = floor(WindowWidth / numCols) where numCols = floor(WindowWidth / maxNameWidth).
    [TestMethod]
    public async Task Dir_WideFormat_ColWidthDerivedFromWidestNameAndWindowWidth()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "a.txt", false);          // 5 chars
        fs.AddEntry('C', [], "b.txt", false);          // 5 chars
        fs.AddEntry('C', [], "longggggg.txt", false);  // 13 chars — widest
        var (cmd, console, bc) = Setup(fs, 'C', []);
        console.WindowWidth = 40;
        // maxWidth=13, numCols=floor(40/13)=3, colWidth=floor(40/3)=13
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/W")), bc, []);
        var contentLine = console.OutLines.Single(l => l.Contains("a.txt"));
        Assert.AreEqual("a.txt".PadRight(13), contentLine[..13]);
        Assert.AreEqual("b.txt".PadRight(13), contentLine[13..26]);
        Assert.AreEqual("longggggg.txt", contentLine[26..39]);
    }
}

internal static class TestArgs
{
    public static IArgumentSet For<TCmd>(params IToken[] tokens)
        where TCmd : class, ICommand
        => ArgumentSet.Parse(tokens, ArgumentSpec.From(
            typeof(TCmd).GetCustomAttributes<BuiltInCommandAttribute>()));
}

[TestClass]
public class DirAttributeFilterTests
{
    private static (DirCommand cmd, TestConsole console, BatchContext bc) Setup(
        TestFileSystem fs, char drive = 'C')
    {
        var cmd = new DirCommand();
        var console = new TestConsole();
        var ctx = new TestCommandContext(fs);
        ctx.SetCurrentDrive(drive);
        ctx.SetPath(drive, []);
        return (cmd, console, new BatchContext { Console = console, Context = ctx });
    }

    // /AH — only hidden files (/A:H or /AH)
    [TestMethod]
    public async Task Dir_AH_ShowsOnlyHidden()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "visible.txt", false, attrs: FileAttributes.Normal);
        fs.AddEntry('C', [], "hidden.txt", false, attrs: FileAttributes.Hidden);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/AH")), bc, []);
        Assert.IsTrue(console.OutLines.Contains("hidden.txt"));
        Assert.IsFalse(console.OutLines.Contains("visible.txt"));
    }

    // /AR — only read-only files
    [TestMethod]
    public async Task Dir_AR_ShowsOnlyReadOnly()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "normal.txt", false, attrs: FileAttributes.Normal);
        fs.AddEntry('C', [], "readonly.txt", false, attrs: FileAttributes.ReadOnly);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/AR")), bc, []);
        Assert.IsTrue(console.OutLines.Contains("readonly.txt"));
        Assert.IsFalse(console.OutLines.Contains("normal.txt"));
    }

    // /AS — only system files
    [TestMethod]
    public async Task Dir_AS_ShowsOnlySystem()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "normal.txt", false, attrs: FileAttributes.Normal);
        fs.AddEntry('C', [], "system.txt", false, attrs: FileAttributes.System);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/AS")), bc, []);
        Assert.IsTrue(console.OutLines.Contains("system.txt"));
        Assert.IsFalse(console.OutLines.Contains("normal.txt"));
    }

    // /AA — only archive files
    [TestMethod]
    public async Task Dir_AA_ShowsOnlyArchive()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "normal.txt", false, attrs: FileAttributes.Normal);
        fs.AddEntry('C', [], "archive.txt", false, attrs: FileAttributes.Archive);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/AA")), bc, []);
        Assert.IsTrue(console.OutLines.Contains("archive.txt"));
        Assert.IsFalse(console.OutLines.Contains("normal.txt"));
    }

    // /AL — only reparse points (symlinks/junctions)
    [TestMethod]
    public async Task Dir_AL_ShowsOnlyReparsePoints()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "normal.txt", false, attrs: FileAttributes.Normal);
        fs.AddEntry('C', [], "link.txt", false, attrs: FileAttributes.ReparsePoint);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/AL")), bc, []);
        Assert.IsTrue(console.OutLines.Contains("link.txt"));
        Assert.IsFalse(console.OutLines.Contains("normal.txt"));
    }

    // /AI — only files not content indexed
    [TestMethod]
    public async Task Dir_AI_ShowsOnlyNotContentIndexed()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "indexed.txt", false, attrs: FileAttributes.Normal);
        fs.AddEntry('C', [], "notindexed.txt", false, attrs: FileAttributes.NotContentIndexed);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/AI")), bc, []);
        Assert.IsTrue(console.OutLines.Contains("notindexed.txt"));
        Assert.IsFalse(console.OutLines.Contains("indexed.txt"));
    }

    // /AO — only offline files
    [TestMethod]
    public async Task Dir_AO_ShowsOnlyOffline()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "online.txt", false, attrs: FileAttributes.Normal);
        fs.AddEntry('C', [], "offline.txt", false, attrs: FileAttributes.Offline);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/AO")), bc, []);
        Assert.IsTrue(console.OutLines.Contains("offline.txt"));
        Assert.IsFalse(console.OutLines.Contains("online.txt"));
    }

    // /A:H — colon syntax still works
    [TestMethod]
    public async Task Dir_AColonH_SameAsAH()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "visible.txt", false, attrs: FileAttributes.Normal);
        fs.AddEntry('C', [], "hidden.txt", false, attrs: FileAttributes.Hidden);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/A:H")), bc, []);
        Assert.IsTrue(console.OutLines.Contains("hidden.txt"));
        Assert.IsFalse(console.OutLines.Contains("visible.txt"));
    }

    // /A-H — negation: only non-hidden
    [TestMethod]
    public async Task Dir_ANegH_ShowsOnlyNonHidden()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "visible.txt", false, attrs: FileAttributes.Normal);
        fs.AddEntry('C', [], "hidden.txt", false, attrs: FileAttributes.Hidden);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/A-H")), bc, []);
        Assert.IsFalse(console.OutLines.Contains("hidden.txt"));
        Assert.IsTrue(console.OutLines.Contains("visible.txt"));
    }
}

[TestClass]
public class DispatcherIntegrationTests
{
    private static (Dispatcher dispatcher, TestConsole console, TestCommandContext ctx) Setup(
        TestFileSystem fs, char drive = 'C', string[]? path = null)
    {
        var console = new TestConsole();
        var ctx = new TestCommandContext(fs);
        ctx.SetCurrentDrive(drive);
        if (path != null) ctx.SetPath(drive, path);
        return (new Dispatcher(), console, ctx);
    }

    // CMD: dir/w → treats /w as the wide flag; produces wide-format listing.
    [TestMethod]
    public async Task DirSlashW_ListsFilesInWideFormat()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "alpha.txt", false);
        fs.AddEntry('C', [], "beta.txt", false);
        fs.AddEntry('C', [], "subdir", true);
        var (dispatcher, console, ctx) = Setup(fs, 'C', []);

        var cmd = Parser.Parse("dir/w");
        await dispatcher.ExecuteCommandAsync(ctx, console, cmd);

        Assert.IsTrue(console.OutLines.Any(l => l.Contains("Volume")));
        string content = console.OutText;
        Assert.IsTrue(content.Contains("alpha.txt"));
        Assert.IsTrue(content.Contains("beta.txt"));
        Assert.IsTrue(content.Contains("[subdir]"));
    }

    // CMD: dir\subdir → treats \subdir as a path argument; lists that directory.
    [TestMethod]
    public async Task DirBackslashSubdir_ListsSubdirectory()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddDir('C', ["subdir"]);
        fs.AddEntry('C', ["subdir"], "file.txt", false);
        var (dispatcher, console, ctx) = Setup(fs, 'C', []);

        var cmd = Parser.Parse(@"dir\subdir");
        await dispatcher.ExecuteCommandAsync(ctx, console, cmd);

        Assert.IsTrue(console.OutLines.Any(l => l.Contains("Directory")));
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("file.txt")));
    }

    // CMD: dir-w → not recognized as command, exits with code 1.
    [TestMethod]
    public async Task DirDashW_IsNotRecognized()
    {
        var fs = new TestFileSystem();
        var (dispatcher, console, ctx) = Setup(fs, 'C', []);

        var cmd = Parser.Parse("dir-w");
        await dispatcher.ExecuteCommandAsync(ctx, console, cmd);

        Assert.AreEqual(1, ctx.ErrorCode);
        Assert.IsTrue(console.ErrLines.Any(l => l.Contains("'dir-w'") && l.Contains("not recognized")));
    }

    // CMD: foo → unknown command, exits with code 1.
    [TestMethod]
    public async Task UnknownCommand_PrintsNotRecognizedError()
    {
        var fs = new TestFileSystem();
        var (dispatcher, console, ctx) = Setup(fs, 'C', []);

        var cmd = Parser.Parse("foo");
        await dispatcher.ExecuteCommandAsync(ctx, console, cmd);

        Assert.AreEqual(1, ctx.ErrorCode);
        Assert.IsTrue(console.ErrLines.Any(l => l.Contains("'foo'") && l.Contains("not recognized")));
        Assert.IsTrue(console.ErrLines.Any(l => l.Contains("operable program")));
    }
}