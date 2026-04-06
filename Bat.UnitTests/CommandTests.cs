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
        var result = await cmd.ExecuteAsync(TestArgs.For<ExitCommand>(), bc, []);
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
        var result = await cmd.ExecuteAsync(TestArgs.For<ExitCommand>(Token.Text("/B")), bc, []);
        Assert.AreEqual(ExitCommand.ExitBatchSentinel, result);
    }

    [TestMethod]
    public async Task Exit_WithB_InRepl_ReturnsSentinel()
    {
        var (cmd, _, bc, _) = Setup();
        var result = await cmd.ExecuteAsync(TestArgs.For<ExitCommand>(Token.Text("/B")), bc, []);
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
        var bc = new BatchContext { Context = new TestCommandContext(), Console = null! };
        var result = await cmd.ExecuteAsync(TestArgs.For<RemCommand>(Token.Text("this is a comment")), bc, []);
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
    public (bool Found, string NativePath) TryGetCurrentFolder()
    {
        if (!FileSystem.DirectoryExists(CurrentDrive, CurrentPath))
            return (false, "");
        return (true, FileSystem.GetNativePath(CurrentDrive, CurrentPath));
    }
}

/// <summary>In-memory IFileSystem for unit tests.</summary>
internal sealed class TestFileSystem : IFileSystem
{
    private readonly HashSet<string> _dirs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<(string Name, bool IsDir, long Size, DateTime Date, FileAttributes Attrs, string? Owner)>> _contents
        = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _shortNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _fileContents = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<char, string> _substs = [];

    public void AddDir(char drive, string[] path) => _dirs.Add(Key(drive, path));

    public void AddBatchFile(char drive, string[] dir, string name, string content)
    {
        AddEntry(drive, dir, name, false);
        _fileContents[Key(drive, [.. dir, name])] = content;
    }

    public void AddEntry(char drive, string[] dir, string name, bool isDir, long size = 100,
        DateTime date = default, FileAttributes attrs = FileAttributes.Normal, string? owner = null)
    {
        if (isDir) attrs |= FileAttributes.Directory;
        var key = Key(drive, dir);
        if (!_contents.TryGetValue(key, out var list))
            _contents[key] = list = [];
        list.Add((name, isDir, size, date == default ? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local) : date, attrs, owner));
    }

    public void SetShortName(char drive, string[] path, string shortName)
    {
        _shortNames[Key(drive, path)] = shortName;
    }

    private static string Key(char drive, string[] path)
        => path.Length == 0
            ? $"{char.ToUpperInvariant(drive)}:\\"
            : $"{char.ToUpperInvariant(drive)}:\\{string.Join("\\", path)}";

    public string GetFullPathDisplayName(char drive, string[] path) => Key(drive, path);
    public string GetDisplayName(string segment) => segment;
    public string GetNativePath(char drive, string[] path) => Key(drive, path);
    public bool DirectoryExists(char drive, string[] path) => _dirs.Contains(Key(drive, path));
    public bool FileExists(char drive, string[] path)
    {
        if (path.Length == 0) return false;
        var dir = Key(drive, path[..^1]);
        var name = path[^1];
        if (_contents.TryGetValue(dir, out var list))
        {
            return list.Any(e => !e.IsDir && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        }
        return false;
    }

    public IEnumerable<DosFileEntry> EnumerateEntries(char drive, string[] path, string pattern)
    {
        var yieldedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_contents.TryGetValue(Key(drive, path), out var list))
        {
            foreach (var e in list)
            {
                if (GlobMatch(e.Name, pattern))
                {
                    var shortName = _shortNames.TryGetValue(Key(drive, [..path, e.Name]), out var sn) ? sn : "";
                    yield return new DosFileEntry(e.Name, e.IsDir, shortName, e.Size, e.Date, e.Attrs, e.Owner ?? "");
                    yieldedNames.Add(e.Name);
                }
            }
        }

        var parentKey = Key(drive, path);
        var prefix = parentKey.EndsWith('\\') ? parentKey : parentKey + "\\";
        foreach (var dirKey in _dirs)
        {
            if (!dirKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var remainder = dirKey[prefix.Length..];
            if (remainder.Length == 0 || remainder.Contains('\\')) continue;
            if (!yieldedNames.Contains(remainder) && GlobMatch(remainder, pattern))
                yield return new DosFileEntry(remainder, true, "", 0, DateTime.MinValue, FileAttributes.Directory, "");
        }
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

    public void CreateDirectory(char drive, string[] path) => throw new NotImplementedException();
    public bool IsExecutable(char drive, string[] path) => false;
    public void DeleteFile(char drive, string[] path) => throw new NotImplementedException();
    public void DeleteDirectory(char drive, string[] path, bool recursive) => throw new NotImplementedException();
    public Stream OpenRead(char drive, string[] path) => throw new NotImplementedException();
    public Stream OpenWrite(char drive, string[] path, bool append) => throw new NotImplementedException();
    public string ReadAllText(char drive, string[] path) => _fileContents.TryGetValue(Key(drive, path), out var content) ? content : "";
    public void WriteAllText(char drive, string[] path, string content) => _fileContents[Key(drive, path)] = content;
    public void CopyFile(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath, bool overwrite) => throw new NotImplementedException();
    public void MoveFile(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath) => throw new NotImplementedException();
    public void RenameFile(char drive, string[] path, string newName) => throw new NotImplementedException();
    public void SetAttributes(char drive, string[] path, FileAttributes attributes) => throw new NotImplementedException();
    public uint GetVolumeSerialNumber(char drive) => 0;
    public IReadOnlyDictionary<string, string> GetFileAssociations() => new Dictionary<string, string>();
    public IReadOnlyDictionary<char, string> GetSubsts() => _substs;
    public void AddSubst(char drive, string nativePath) => _substs[char.ToUpperInvariant(drive)] = nativePath;
    public void RemoveSubst(char drive) => _substs.Remove(char.ToUpperInvariant(drive));
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
        var result = await cmd.ExecuteAsync(TestArgs.For<CdCommand>(Token.Text(@"\Missing")), bc, []);
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
        var result = await cmd.ExecuteAsync(TestArgs.For<CdCommand>(Token.Text("/?") ), bc, []);
        Assert.AreEqual(0, result);
        Assert.IsTrue(console.OutText.Contains("CHDIR"));
        Assert.IsTrue(console.OutText.Contains("/D"));
    }

    [TestMethod]
    public async Task Cd_PathWithSpaces_WorksWithoutQuotes()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', ["Program Files", "App"]);
        var (cmd, _, bc, ctx) = Setup(fs, 'C', []);
        await cmd.ExecuteAsync(TestArgs.For<CdCommand>(Token.Text(@"\Program Files\App")), bc, []);
        CollectionAssert.AreEqual(new[] { "Program Files", "App" }, ctx.CurrentPath);
    }

    [TestMethod]
    public async Task Cd_MultipleDoubleDots_NavigatesUp()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', ["Users"]);
        var (cmd, _, bc, ctx) = Setup(fs, 'C', ["Users", "Bart", "Documents", "Work"]);
        await cmd.ExecuteAsync(TestArgs.For<CdCommand>(Token.Text(@"..\..\..")), bc, []);
        CollectionAssert.AreEqual(new[] { "Users" }, ctx.CurrentPath);
    }

    [TestMethod]
    public async Task Cd_DoubleDotFromRoot_StaysAtRoot()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        var (cmd, _, bc, ctx) = Setup(fs, 'C', []);
        await cmd.ExecuteAsync(TestArgs.For<CdCommand>(Token.Text("..")), bc, []);
        CollectionAssert.AreEqual(Array.Empty<string>(), ctx.CurrentPath);
    }

    [TestMethod]
    public async Task Cd_RelativePathWithDriveLetter_ChangesPathOnOtherDrive()
    {
        var fs = new TestFileSystem();
        fs.AddDir('D', ["Work", "Projects"]);
        var (cmd, _, bc, ctx) = Setup(fs, 'C', ["Users"]);
        ctx.SetPath('D', ["Work"]);
        await cmd.ExecuteAsync(TestArgs.For<CdCommand>(Token.Text("D:Projects")), bc, []);
        CollectionAssert.AreEqual(new[] { "Work", "Projects" }, ctx.GetPathForDrive('D'));
        Assert.AreEqual('C', ctx.CurrentDrive);
    }

    [TestMethod]
    public async Task Cd_SlashD_CurrentDrive_SwitchesToCurrentDrive()
    {
        var fs = new TestFileSystem();
        var (cmd, _, bc, ctx) = Setup(fs, 'C', ["Users"]);
        await cmd.ExecuteAsync(TestArgs.For<CdCommand>(Token.Text("/D"), Token.Whitespace(" "), Token.Text("C:")), bc, []);
        Assert.AreEqual('C', ctx.CurrentDrive);
        CollectionAssert.AreEqual(new[] { "Users" }, ctx.CurrentPath);
    }

    [TestMethod]
    public async Task Cd_DotInPath_IsIgnored()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', ["Users", "Bart"]);
        var (cmd, _, bc, ctx) = Setup(fs, 'C', ["Users"]);
        await cmd.ExecuteAsync(TestArgs.For<CdCommand>(Token.Text(@".\Bart")), bc, []);
        CollectionAssert.AreEqual(new[] { "Users", "Bart" }, ctx.CurrentPath);
    }

    [TestMethod]
    public async Task Cd_ForwardSlashInPath_IsNotPathSeparator()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', ["Users", "Bart"]);
        var (cmd, console, bc, ctx) = Setup(fs, 'C', []);
        var result = await cmd.ExecuteAsync(TestArgs.For<CdCommand>(Token.Text(@"\Users/Bart")), bc, []);
        Assert.AreEqual(1, result);
        Assert.IsTrue(console.ErrLines[0].Contains("cannot find"));
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
        var alphaIdx = lines.IndexOf("alpha.txt");
        var zebraIdx = lines.IndexOf("zebra.txt");
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
        var dirIdx = lines.IndexOf("adir");
        var fileIdx = lines.IndexOf("zfile.txt");
        Assert.IsTrue(dirIdx >= 0 && fileIdx >= 0 && dirIdx < fileIdx);
    }

    [TestMethod]
    public async Task Dir_SlashQuestion_PrintsHelp()
    {
        var fs = new TestFileSystem();
        var (cmd, console, bc) = Setup(fs, 'C', []);
        var result = await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/?") ), bc, []);
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

    // Real CMD: after deleting the subst for the current drive while in a subdirectory,
    // dir (no args) shows "The system cannot find the path specified." with no volume header.
    // The prompt still shows the last-known path (e.g. B:\net10.0).
    [TestMethod]
    public async Task Dir_NoArgs_DeletedSubstCurrentDrive_ShowsPathNotSpecified()
    {
        var fs = new TestFileSystem();
        // B: has no dirs — simulates a drive whose subst has been deleted
        var (cmd, console, bc) = Setup(fs, 'B', ["net10.0"]);

        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(), bc, []);

        Assert.IsTrue(console.OutLines.Any(l => l.Contains("The system cannot find the path specified.")));
        Assert.IsFalse(console.OutLines.Any(l => l.Contains("Volume")), "Must not show volume header for unreachable drive");

        // Context unchanged — prompt still shows B:\net10.0
        Assert.AreEqual('B', bc.Context.CurrentDrive);
        CollectionAssert.AreEqual(new[] { "net10.0" }, bc.Context.CurrentPath);
    }

    // "dir C:" with current drive Z: should list C:'s root — not inject Z:'s path segments.
    [TestMethod]
    public async Task Dir_DriveColonOnly_ListsRootOfSpecifiedDrive()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "on_c.txt", false);
        var (cmd, console, bc) = Setup(fs, 'Z', []);  // current drive is Z, not C
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("C:")), bc, []);
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("on_c.txt")), "Should list C:'s root entries");
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("Directory of C:\\")), "Header must show C:\\ not Z:'s path");
    }

    [TestMethod]
    public async Task Dir_DriveColonOnly_DoesNotContainCurrentDrivePath()
    {
        var fs = new TestFileSystem();
        fs.AddDir('Z', ["deeply", "nested"]);
        fs.AddDir('C', []);
        var (cmd, console, bc) = Setup(fs, 'Z', ["deeply", "nested"]);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("C:")), bc, []);
        // Must NOT contain Z:'s current path segments in the header
        Assert.IsFalse(console.OutLines.Any(l => l.Contains("deeply")), "Z:'s path must not bleed into C: listing");
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
        var content = console.OutText;
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

    [TestMethod]
    public async Task Dir_InvalidSwitch_PrintsError()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        var (dispatcher, console, ctx) = Setup(fs, 'C', []);

        var cmd = Parser.Parse("dir /g");
        await dispatcher.ExecuteCommandAsync(ctx, console, cmd);

        Assert.AreEqual(1, ctx.ErrorCode);
        Assert.IsTrue(console.ErrLines.Any(l => l.Contains("Invalid switch") && l.Contains("\"g\"")));
    }

    [TestMethod]
    public async Task DirSlashQ_ShowsOwnerColumn()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "file.txt", false, owner: "DOMAIN\\User");
        var (dispatcher, console, ctx) = Setup(fs, 'C', []);

        var cmd = Parser.Parse("dir/q");
        await dispatcher.ExecuteCommandAsync(ctx, console, cmd);

        Assert.AreEqual(0, ctx.ErrorCode, $"Should succeed. Errors: {string.Join(", ", console.ErrLines)}");
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("DOMAIN\\User")), "Should show owner");
        Assert.IsFalse(console.OutLines.Any(l => l.Contains("Directory of") && l.Contains("\\q")), "Should NOT treat /q as path");
    }

    [TestMethod]
    public async Task DirSlashQ_WithPath_ShowsOwnerColumn()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddDir('C', ["windows"]);
        fs.AddEntry('C', ["windows"], "file.txt", false, owner: "NT AUTHORITY\\SYSTEM");
        var (dispatcher, console, ctx) = Setup(fs, 'C', []);

        var cmd = Parser.Parse("dir/q \\windows");

        var cmdNode = (Bat.Nodes.CommandNode)cmd.Root;
        var headRaw = cmdNode.Head.Raw;
        var headType = cmdNode.Head.GetType().Name;
        var tailTokens = string.Join(", ", cmdNode.Tail.Select(t => $"{t.GetType().Name}:{t.Raw}"));

        await dispatcher.ExecuteCommandAsync(ctx, console, cmd);

        var allOutput = string.Join("\n", console.OutLines) + "\n" + string.Join("\n", console.ErrLines);
        var debugInfo = $"Head={headType}[{headRaw}], Tail=[{tailTokens}]";
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("NT AUTHORITY\\SYSTEM")), $"Should show owner. {debugInfo}\nOutput:\n{allOutput}");
    }

    // CMD: cd..  → goes to parent directory (no space needed in CMD)
    [TestMethod]
    public async Task CdDotDot_NavigatesToParent()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', ["Users"]);
        fs.AddDir('C', ["Users", "Bart"]);
        var (dispatcher, console, ctx) = Setup(fs, 'C', ["Users", "Bart"]);

        var cmd = Parser.Parse(@"cd..");
        await dispatcher.ExecuteCommandAsync(ctx, console, cmd);

        Assert.AreEqual(0, ctx.ErrorCode, console.ErrText);
        CollectionAssert.AreEqual(new[] { "Users" }, ctx.CurrentPath);
    }

    // CMD: cd.  → stays in current directory (no-op)
    [TestMethod]
    public async Task CdDot_StaysInCurrentDirectory()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', ["Users", "Bart"]);
        var (dispatcher, console, ctx) = Setup(fs, 'C', ["Users", "Bart"]);

        var cmd = Parser.Parse(@"cd.");
        await dispatcher.ExecuteCommandAsync(ctx, console, cmd);

        Assert.AreEqual(0, ctx.ErrorCode, console.ErrText);
        CollectionAssert.AreEqual(new[] { "Users", "Bart" }, ctx.CurrentPath);
    }

    // CMD: cd.\subdir → navigates into subdir relative to current
    [TestMethod]
    public async Task CdDotBackslash_NavigatesIntoSubdir()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', ["Users", "Bart", "Documents"]);
        var (dispatcher, console, ctx) = Setup(fs, 'C', ["Users", "Bart"]);

        var cmd = Parser.Parse(@"cd.\Documents");
        await dispatcher.ExecuteCommandAsync(ctx, console, cmd);

        Assert.AreEqual(0, ctx.ErrorCode, console.ErrText);
        CollectionAssert.AreEqual(new[] { "Users", "Bart", "Documents" }, ctx.CurrentPath);
    }

    // "D:" alone switches to drive D when its root is accessible.
    [TestMethod]
    public async Task DriveSwitch_AccessibleDrive_SwitchesCurrentDrive()
    {
        var fs = new TestFileSystem();
        fs.AddDir('D', []);
        var (dispatcher, console, ctx) = Setup(fs, 'C', []);

        await dispatcher.ExecuteCommandAsync(ctx, console, Parser.Parse("D:"));

        Assert.AreEqual(0, ctx.ErrorCode, console.ErrText);
        Assert.AreEqual('D', ctx.CurrentDrive);
    }

    // "X:" where X: is not mapped → error message, drive unchanged.
    [TestMethod]
    public async Task DriveSwitch_NonExistentDrive_ShowsError()
    {
        var fs = new TestFileSystem();
        var (dispatcher, console, ctx) = Setup(fs, 'C', []);

        await dispatcher.ExecuteCommandAsync(ctx, console, Parser.Parse("X:"));

        Assert.AreEqual(1, ctx.ErrorCode);
        Assert.AreEqual('C', ctx.CurrentDrive);
        Assert.IsTrue(console.ErrLines.Any(l => l.Contains("cannot find the drive")));
    }

    // Per-drive directory is retained when switching back to a previously visited drive.
    [TestMethod]
    public async Task DriveSwitch_RetainsPerDriveDirectory()
    {
        var fs = new TestFileSystem();
        fs.AddDir('D', []);
        fs.AddDir('D', ["Work"]);
        var (dispatcher, console, ctx) = Setup(fs, 'C', []);
        ctx.SetPath('D', ["Work"]);  // D: was last visited at D:\Work

        await dispatcher.ExecuteCommandAsync(ctx, console, Parser.Parse("D:"));

        Assert.AreEqual('D', ctx.CurrentDrive);
        CollectionAssert.AreEqual(new[] { "Work" }, ctx.CurrentPath);
    }

    // Case insensitive: "d:" should work the same as "D:".
    [TestMethod]
    public async Task DriveSwitch_LowercaseDrive_SwitchesDrive()
    {
        var fs = new TestFileSystem();
        fs.AddDir('D', []);
        var (dispatcher, console, ctx) = Setup(fs, 'C', []);

        await dispatcher.ExecuteCommandAsync(ctx, console, Parser.Parse("d:"));

        Assert.AreEqual(0, ctx.ErrorCode, console.ErrText);
        Assert.AreEqual('D', ctx.CurrentDrive);
    }
}

[TestClass]
public class DirLowercaseTests
{
    private static (DirCommand cmd, TestConsole console, BatchContext bc) Setup(TestFileSystem fs, char drive = 'C')
    {
        var cmd = new DirCommand();
        var console = new TestConsole();
        var ctx = new TestCommandContext(fs);
        ctx.SetCurrentDrive(drive);
        ctx.SetPath(drive, []);
        return (cmd, console, new BatchContext { Console = console, Context = ctx });
    }

    // /L lowercases file names in the normal listing
    [TestMethod]
    public async Task Dir_L_FileNamesAreLowercase()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "ReadMe.TXT", false);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/L")), bc, []);
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("readme.txt")));
        Assert.IsFalse(console.OutLines.Any(l => l.Contains("ReadMe.TXT")));
    }

    // /L lowercases directory names in the normal listing
    [TestMethod]
    public async Task Dir_L_DirectoryNamesAreLowercase()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "MyFolder", true);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/L")), bc, []);
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("myfolder")));
        Assert.IsFalse(console.OutLines.Any(l => l.Contains("MyFolder")));
    }

    // /L with /B: bare format still produces lowercase names
    [TestMethod]
    public async Task Dir_L_WithBare_NamesAreLowercase()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "Alpha.TXT", false);
        fs.AddEntry('C', [], "Beta.DOC", false);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/L"), Token.Whitespace(" "), Token.Text("/B")), bc, []);
        Assert.IsTrue(console.OutLines.Contains("alpha.txt"));
        Assert.IsTrue(console.OutLines.Contains("beta.doc"));
        Assert.IsFalse(console.OutLines.Contains("Alpha.TXT"));
        Assert.IsFalse(console.OutLines.Contains("Beta.DOC"));
    }

    // /L with /W: wide format still produces lowercase names; dirs use [name]
    [TestMethod]
    public async Task Dir_L_WithWide_NamesAreLowercase()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "Alpha.TXT", false);
        fs.AddEntry('C', [], "SUBDIR", true);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/L"), Token.Whitespace(" "), Token.Text("/W")), bc, []);
        var content = console.OutText;
        Assert.IsTrue(content.Contains("alpha.txt"));
        Assert.IsTrue(content.Contains("[subdir]"));
        Assert.IsFalse(content.Contains("Alpha.TXT"));
        Assert.IsFalse(content.Contains("SUBDIR"));
    }

    // Without /L, mixed-case names are preserved as stored
    [TestMethod]
    public async Task Dir_Default_NamesKeepOriginalCase()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "MixedCase.TXT", false);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(), bc, []);
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("MixedCase.TXT")));
        Assert.IsFalse(console.OutLines.Any(l => l.Contains("mixedcase.txt")));
    }

    // /L lowercases multiple files with various mixed-case patterns
    [TestMethod]
    public async Task Dir_L_MixedCaseFiles_AllLowercased()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "UPPER.TXT", false);
        fs.AddEntry('C', [], "lower.txt", false);
        fs.AddEntry('C', [], "CamelCase.Dat", false);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/L")), bc, []);
        Assert.IsTrue(console.OutLines.Contains("upper.txt"));
        Assert.IsTrue(console.OutLines.Contains("lower.txt"));
        Assert.IsTrue(console.OutLines.Contains("camelcase.dat"));
    }

    // /L does not affect the volume/directory header lines
    [TestMethod]
    public async Task Dir_L_HeaderLinesAreNotLowercased()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/L")), bc, []);
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("Volume")));
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("Directory")));
    }
}

[TestClass]
public class DirNewLongFormatTests
{
    private static (DirCommand cmd, TestConsole console, BatchContext bc) Setup(TestFileSystem fs, char drive = 'C')
    {
        var cmd = new DirCommand();
        var console = new TestConsole();
        var ctx = new TestCommandContext(fs);
        ctx.SetCurrentDrive(drive);
        ctx.SetPath(drive, []);
        return (cmd, console, new BatchContext { Console = console, Context = ctx });
    }

    // /N shows all files in the listing
    [TestMethod]
    public async Task Dir_N_ShowsAllFiles()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "document.txt", false, size: 1024);
        fs.AddEntry('C', [], "image.png", false, size: 4096);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/N")), bc, []);
        var content = console.OutText;
        Assert.IsTrue(content.Contains("document.txt"));
        Assert.IsTrue(content.Contains("image.png"));
    }

    // /N shows directories with <DIR> marker
    [TestMethod]
    public async Task Dir_N_ShowsDirectoriesWithDirMarker()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "subdir", true);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/N")), bc, []);
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("subdir") && l.Contains("<DIR>")));
    }

    // /N still shows the volume header and directory info
    [TestMethod]
    public async Task Dir_N_ShowsVolumeHeaderAndDirectoryInfo()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "file.txt", false, size: 100);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/N")), bc, []);
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("Volume")));
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("Directory")));
    }

    // /N still shows the summary (file count and total bytes)
    [TestMethod]
    public async Task Dir_N_ShowsSummaryLine()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "file.txt", false, size: 500);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/N")), bc, []);
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("File(s)")));
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("Dir(s)")));
    }

    // /N shows long filenames without truncation
    [TestMethod]
    public async Task Dir_N_WithLongFileName_IsNotTruncated()
    {
        var longName = "this_is_a_very_long_filename_that_exceeds_eight_chars.txt";
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], longName, false, size: 100);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/N")), bc, []);
        Assert.IsTrue(console.OutLines.Any(l => l.Contains(longName)));
    }

    // /N and default produce the same file listing (long names shown in both)
    [TestMethod]
    public async Task Dir_N_ProducesSameFilesAsDefault()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "document.txt", false, size: 1234);
        fs.AddEntry('C', [], "subdir", true);
        var (cmdN, consoleN, bcN) = Setup(fs);
        var (cmdDefault, consoleDefault, bcDefault) = Setup(fs);
        await cmdN.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/N")), bcN, []);
        await cmdDefault.ExecuteAsync(TestArgs.For<DirCommand>(), bcDefault, []);
        Assert.IsTrue(consoleN.OutLines.Any(l => l.Contains("document.txt")));
        Assert.IsTrue(consoleDefault.OutLines.Any(l => l.Contains("document.txt")));
        Assert.IsTrue(consoleN.OutLines.Any(l => l.Contains("subdir")));
        Assert.IsTrue(consoleDefault.OutLines.Any(l => l.Contains("subdir")));
    }

    // /N combined with /B: bare names still work
    [TestMethod]
    public async Task Dir_N_WithBare_ShowsOnlyNames()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "file.txt", false, size: 999);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/N"), Token.Whitespace(" "), Token.Text("/B")), bc, []);
        Assert.IsTrue(console.OutLines.Contains("file.txt"));
        Assert.IsFalse(console.OutLines.Any(l => l.Contains("Volume")));
        Assert.IsFalse(console.OutLines.Any(l => l.Contains("bytes")));
    }
}

[TestClass]
public class DirSortOrderTests
{
    private static (DirCommand cmd, TestConsole console, BatchContext bc) Setup(TestFileSystem fs, char drive = 'C')
    {
        var cmd = new DirCommand();
        var console = new TestConsole();
        var ctx = new TestCommandContext(fs);
        ctx.SetCurrentDrive(drive);
        ctx.SetPath(drive, []);
        return (cmd, console, new BatchContext { Console = console, Context = ctx });
    }

    // /O:N — sort by name ascending (alphabetical)
    [TestMethod]
    public async Task Dir_OColonN_SortsByNameAscending()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "zebra.txt", false);
        fs.AddEntry('C', [], "alpha.txt", false);
        fs.AddEntry('C', [], "mango.txt", false);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/O:N")), bc, []);
        var lines = console.OutLines.ToList();
        Assert.IsTrue(lines.IndexOf("alpha.txt") < lines.IndexOf("mango.txt"));
        Assert.IsTrue(lines.IndexOf("mango.txt") < lines.IndexOf("zebra.txt"));
    }

    // /ON (no colon) — prefix-option fallback, same effect as /O:N
    [TestMethod]
    public async Task Dir_ON_NoColon_SortsByNameAscending()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "zebra.txt", false);
        fs.AddEntry('C', [], "alpha.txt", false);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/ON")), bc, []);
        var lines = console.OutLines.ToList();
        Assert.IsTrue(lines.IndexOf("alpha.txt") < lines.IndexOf("zebra.txt"));
    }

    // /O:N comparison is case-insensitive
    [TestMethod]
    public async Task Dir_OColonN_SortIsNotCaseSensitive()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "BETA.txt", false);
        fs.AddEntry('C', [], "alpha.txt", false);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/O:N")), bc, []);
        var lines = console.OutLines.ToList();
        Assert.IsTrue(lines.IndexOf("alpha.txt") < lines.IndexOf("BETA.txt"),
            "Case-insensitive: alpha should appear before BETA");
    }

    // /O:-N — sort by name descending (reverse alphabetical)
    [TestMethod]
    public async Task Dir_OColonMinusN_SortsByNameDescending()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "alpha.txt", false);
        fs.AddEntry('C', [], "zebra.txt", false);
        fs.AddEntry('C', [], "mango.txt", false);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/O:-N")), bc, []);
        var lines = console.OutLines.ToList();
        Assert.IsTrue(lines.IndexOf("zebra.txt") < lines.IndexOf("mango.txt"));
        Assert.IsTrue(lines.IndexOf("mango.txt") < lines.IndexOf("alpha.txt"));
    }

    // /O:E — sort by extension ascending
    [TestMethod]
    public async Task Dir_OColonE_SortsByExtensionAscending()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "file.txt", false);
        fs.AddEntry('C', [], "file.doc", false);
        fs.AddEntry('C', [], "file.bat", false);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/O:E")), bc, []);
        var lines = console.OutLines.ToList();
        // .bat < .doc < .txt alphabetically
        Assert.IsTrue(lines.IndexOf("file.bat") < lines.IndexOf("file.doc"));
        Assert.IsTrue(lines.IndexOf("file.doc") < lines.IndexOf("file.txt"));
    }

    // /O:E — files with no extension sort before files with any extension
    [TestMethod]
    public async Task Dir_OColonE_NoExtensionSortedBeforeExtensions()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "readme", false);
        fs.AddEntry('C', [], "file.txt", false);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/O:E")), bc, []);
        var lines = console.OutLines.ToList();
        Assert.IsTrue(lines.IndexOf("readme") < lines.IndexOf("file.txt"),
            "Empty extension should sort before .txt");
    }

    // /O:-E — sort by extension descending (reverse)
    [TestMethod]
    public async Task Dir_OColonMinusE_SortsByExtensionDescending()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "file.txt", false);
        fs.AddEntry('C', [], "file.doc", false);
        fs.AddEntry('C', [], "file.bat", false);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/O:-E")), bc, []);
        var lines = console.OutLines.ToList();
        // reversed: .txt > .doc > .bat
        Assert.IsTrue(lines.IndexOf("file.txt") < lines.IndexOf("file.doc"));
        Assert.IsTrue(lines.IndexOf("file.doc") < lines.IndexOf("file.bat"));
    }

    // /O:S — sort by size ascending (smallest first)
    [TestMethod]
    public async Task Dir_OColonS_SortsBySizeSmallestFirst()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "big.txt", false, size: 9000);
        fs.AddEntry('C', [], "small.txt", false, size: 100);
        fs.AddEntry('C', [], "medium.txt", false, size: 5000);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/O:S")), bc, []);
        var lines = console.OutLines.ToList();
        Assert.IsTrue(lines.IndexOf("small.txt") < lines.IndexOf("medium.txt"));
        Assert.IsTrue(lines.IndexOf("medium.txt") < lines.IndexOf("big.txt"));
    }

    // /O:-S — sort by size descending (largest first)
    [TestMethod]
    public async Task Dir_OColonMinusS_SortsBySizeLargestFirst()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "big.txt", false, size: 9000);
        fs.AddEntry('C', [], "small.txt", false, size: 100);
        fs.AddEntry('C', [], "medium.txt", false, size: 5000);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/O:-S")), bc, []);
        var lines = console.OutLines.ToList();
        Assert.IsTrue(lines.IndexOf("big.txt") < lines.IndexOf("medium.txt"));
        Assert.IsTrue(lines.IndexOf("medium.txt") < lines.IndexOf("small.txt"));
    }

    // /O:D — sort by date ascending (oldest first)
    [TestMethod]
    public async Task Dir_OColonD_SortsByDateOldestFirst()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "new.txt", false, date: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Local));
        fs.AddEntry('C', [], "old.txt", false, date: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Local));
        fs.AddEntry('C', [], "mid.txt", false, date: new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Local));
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/O:D")), bc, []);
        var lines = console.OutLines.ToList();
        Assert.IsTrue(lines.IndexOf("old.txt") < lines.IndexOf("mid.txt"));
        Assert.IsTrue(lines.IndexOf("mid.txt") < lines.IndexOf("new.txt"));
    }

    // /O:-D — sort by date descending (newest first)
    [TestMethod]
    public async Task Dir_OColonMinusD_SortsByDateNewestFirst()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "new.txt", false, date: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Local));
        fs.AddEntry('C', [], "old.txt", false, date: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Local));
        fs.AddEntry('C', [], "mid.txt", false, date: new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Local));
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/O:-D")), bc, []);
        var lines = console.OutLines.ToList();
        Assert.IsTrue(lines.IndexOf("new.txt") < lines.IndexOf("mid.txt"));
        Assert.IsTrue(lines.IndexOf("mid.txt") < lines.IndexOf("old.txt"));
    }

    // /O:G — directories appear before files regardless of name order
    [TestMethod]
    public async Task Dir_OColonG_GroupsDirsBeforeFiles()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "zfile.txt", false);
        fs.AddEntry('C', [], "adir", true);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/O:G")), bc, []);
        var lines = console.OutLines.ToList();
        var dirIdx = lines.IndexOf("adir");
        var fileIdx = lines.IndexOf("zfile.txt");
        Assert.IsTrue(dirIdx >= 0 && fileIdx >= 0 && dirIdx < fileIdx);
    }

    // /O:G with multiple dirs — all dirs appear before any file
    [TestMethod]
    public async Task Dir_OColonG_AllDirsBeforeAllFiles()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "z.txt", false);
        fs.AddEntry('C', [], "b.txt", false);
        fs.AddEntry('C', [], "zdir", true);
        fs.AddEntry('C', [], "adir", true);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/O:G")), bc, []);
        var lines = console.OutLines.ToList();
        var lastDirIdx = Math.Max(lines.IndexOf("zdir"), lines.IndexOf("adir"));
        var firstFileIdx = Math.Min(lines.IndexOf("z.txt"), lines.IndexOf("b.txt"));
        Assert.IsTrue(lastDirIdx < firstFileIdx, "All directories should appear before any file");
    }

    // /O:GN — group dirs first, then within each group sort by name
    [TestMethod]
    public async Task Dir_OColonGN_GroupsDirsFirst_ThenSortsByName()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "zebra.txt", false);
        fs.AddEntry('C', [], "alpha.txt", false);
        fs.AddEntry('C', [], "zdir", true);
        fs.AddEntry('C', [], "adir", true);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/O:GN")), bc, []);
        var lines = console.OutLines.ToList();
        var adirIdx = lines.IndexOf("adir");
        var zdirIdx = lines.IndexOf("zdir");
        var alphaIdx = lines.IndexOf("alpha.txt");
        var zebraIdx = lines.IndexOf("zebra.txt");
        // All dirs before all files
        Assert.IsTrue(adirIdx < alphaIdx, "adir (dir) should appear before alpha.txt (file)");
        Assert.IsTrue(zdirIdx < alphaIdx, "zdir (dir) should appear before alpha.txt (file)");
        // Dirs sorted by name within the dir group
        Assert.IsTrue(adirIdx < zdirIdx, "adir should sort before zdir within the dirs group");
        // Files sorted by name within the file group
        Assert.IsTrue(alphaIdx < zebraIdx, "alpha.txt should sort before zebra.txt within the files group");
    }

    // /O:GS — group dirs first, then within each group sort by size
    [TestMethod]
    public async Task Dir_OColonGS_GroupsDirsFirst_ThenSortsBySize()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "big.txt", false, size: 9000);
        fs.AddEntry('C', [], "small.txt", false, size: 100);
        fs.AddEntry('C', [], "adir", true);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/O:GS")), bc, []);
        var lines = console.OutLines.ToList();
        var dirIdx = lines.IndexOf("adir");
        var smallIdx = lines.IndexOf("small.txt");
        var bigIdx = lines.IndexOf("big.txt");
        // Dir before files
        Assert.IsTrue(dirIdx < smallIdx);
        // Files sorted by size ascending
        Assert.IsTrue(smallIdx < bigIdx);
    }

    // /O:N with a single file — always succeeds without error
    [TestMethod]
    public async Task Dir_OColonN_SingleFile_Succeeds()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "only.txt", false);
        var (cmd, console, bc) = Setup(fs);
        var result = await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/O:N")), bc, []);
        Assert.AreEqual(0, result);
        Assert.IsTrue(console.OutLines.Contains("only.txt"));
    }

    // /O:N with empty directory — no entries, no crash
    [TestMethod]
    public async Task Dir_OColonN_EmptyDirectory_ShowsEmptySummary()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        var (cmd, console, bc) = Setup(fs);
        var result = await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/O:N")), bc, []);
        Assert.AreEqual(0, result);
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("File(s)")));
    }
}

[TestClass]
public class DirRecursiveTests
{
    private static (DirCommand cmd, TestConsole console, BatchContext bc) Setup(TestFileSystem fs, char drive = 'C')
    {
        var cmd = new DirCommand();
        var console = new TestConsole();
        var ctx = new TestCommandContext(fs);
        ctx.SetCurrentDrive(drive);
        ctx.SetPath(drive, []);
        return (cmd, console, new BatchContext { Console = console, Context = ctx });
    }

    [TestMethod]
    public async Task Dir_S_RecursesIntoSubdirectories()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddDir('C', ["subdir"]);
        fs.AddEntry('C', [], "root.txt", false);
        fs.AddEntry('C', ["subdir"], "nested.txt", false);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/S")), bc, []);
        Assert.IsTrue(console.OutLines.Contains("root.txt"));
        Assert.IsTrue(console.OutLines.Contains("nested.txt"));
    }

    [TestMethod]
    public async Task Dir_S_ShowsMultipleDirectoryHeaders()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddDir('C', ["sub"]);
        fs.AddEntry('C', [], "file1.txt", false);
        fs.AddEntry('C', ["sub"], "file2.txt", false);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/S")), bc, []);
        var dirHeaders = console.OutLines.Where(l => l.Contains("Directory of")).ToList();
        Assert.IsTrue(dirHeaders.Count >= 2);
    }

    [TestMethod]
    public async Task Dir_S_WithWildcard_FindsInSubdirs()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddDir('C', ["subdir"]);
        fs.AddEntry('C', [], "root.log", false);
        fs.AddEntry('C', [], "root.txt", false);
        fs.AddEntry('C', ["subdir"], "nested.txt", false);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/S"), Token.Whitespace(" "), Token.Text("*.txt")), bc, []);
        Assert.IsTrue(console.OutLines.Contains("root.txt"));
        Assert.IsTrue(console.OutLines.Contains("nested.txt"));
        Assert.IsFalse(console.OutLines.Contains("root.log"));
    }
}

[TestClass]
public class DirDircmdEnvironmentTests
{
    private static (DirCommand cmd, TestConsole console, BatchContext bc, TestCommandContext ctx) Setup(
        TestFileSystem fs, char drive = 'C')
    {
        var cmd = new DirCommand();
        var console = new TestConsole();
        var ctx = new TestCommandContext(fs);
        ctx.SetCurrentDrive(drive);
        ctx.SetPath(drive, []);
        return (cmd, console, new BatchContext { Console = console, Context = ctx }, ctx);
    }

    [TestMethod]
    public async Task Dir_DircmdBare_OverriddenByWide_ShowsHeader()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "file.txt", false);
        var (cmd, console, bc, ctx) = Setup(fs);
        ctx.EnvironmentVariables["DIRCMD"] = "/B";
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/W")), bc, []);
        Assert.IsTrue(console.OutText.Contains("Volume"));
    }

    [TestMethod]
    public async Task Dir_DircmdWide_OverriddenByBare_HidesHeader()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "file.txt", false);
        var (cmd, console, bc, ctx) = Setup(fs);
        ctx.EnvironmentVariables["DIRCMD"] = "/W";
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B")), bc, []);
        Assert.IsFalse(console.OutText.Contains("Volume"));
    }

    [TestMethod]
    public async Task Dir_DircmdLowercase_WithoutOverride_Lowercases()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "UPPER.TXT", false);
        var (cmd, console, bc, ctx) = Setup(fs);
        ctx.EnvironmentVariables["DIRCMD"] = "/L";
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(), bc, []);
        Assert.IsTrue(console.OutText.Contains("upper.txt"));
    }

    [TestMethod]
    public async Task Dir_DircmdLowercase_OverriddenByNegateL_PreservesCase()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "UPPER.TXT", false);
        var (cmd, console, bc, ctx) = Setup(fs);
        ctx.EnvironmentVariables["DIRCMD"] = "/L";
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/-L")), bc, []);
        Assert.IsTrue(console.OutText.Contains("UPPER.TXT"));
    }

    [TestMethod]
    public async Task Dir_DircmdSort_OverriddenByReverseSort()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "alpha.txt", false);
        fs.AddEntry('C', [], "zebra.txt", false);
        var (cmd, console, bc, ctx) = Setup(fs);
        ctx.EnvironmentVariables["DIRCMD"] = "/O:N";
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/B"), Token.Whitespace(" "), Token.Text("/O:-N")), bc, []);
        var lines = console.OutLines.ToList();
        Assert.IsTrue(lines.IndexOf("zebra.txt") < lines.IndexOf("alpha.txt"));
    }

    [TestMethod]
    public async Task Dir_DircmdSeparator_OverriddenByNegateC_NoCommas()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "big.txt", false, size: 1234567);
        var (cmd, console, bc, ctx) = Setup(fs);
        ctx.EnvironmentVariables["DIRCMD"] = "/C";
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/-C")), bc, []);
        Assert.IsTrue(console.OutText.Contains("1234567"));
        Assert.IsFalse(console.OutText.Contains("1,234,567"));
    }

    [TestMethod]
    [DataRow("/A:H", "", "hidden.txt", DisplayName = "DIRCMD /A:H without override shows hidden")]
    [DataRow("/A:H", "/-A", "visible.txt", DisplayName = "DIRCMD /A:H overridden shows visible")]
    [DataRow("/A:D", "", "subdir", DisplayName = "DIRCMD /A:D without override shows dirs")]
    [DataRow("/A:D", "/A:-D", "file.txt", DisplayName = "DIRCMD /A:D overridden shows files")]
    public async Task Dir_Dircmd_AttributeFilter_Combinations(string dircmdValue, string cmdLineFlag, string expectedName)
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "visible.txt", false, attrs: FileAttributes.Normal);
        fs.AddEntry('C', [], "hidden.txt", false, attrs: FileAttributes.Hidden);
        fs.AddEntry('C', [], "file.txt", false);
        fs.AddEntry('C', [], "subdir", true);
        var (cmd, console, bc, ctx) = Setup(fs);
        ctx.EnvironmentVariables["DIRCMD"] = dircmdValue;

        var tokens = string.IsNullOrWhiteSpace(cmdLineFlag)
            ? new IToken[] { Token.Text("/B") }
            : new IToken[] { Token.Text("/B"), Token.Whitespace(" "), Token.Text(cmdLineFlag) };

        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(tokens), bc, []);
        Assert.IsTrue(console.OutLines.Contains(expectedName), $"Expected to find {expectedName} in output");
    }

    [TestMethod]
    [DataRow("/O:N", "", "alpha.txt", "zebra.txt", DisplayName = "DIRCMD /O:N ascending")]
    [DataRow("/O:N", "/O:-N", "zebra.txt", "alpha.txt", DisplayName = "DIRCMD /O:N overridden by reverse")]
    [DataRow("/O:E", "", "file.bat", "file.txt", DisplayName = "DIRCMD /O:E by extension")]
    [DataRow("/O:S", "", "small.txt", "big.txt", DisplayName = "DIRCMD /O:S by size")]
    [DataRow("/O:GN", "", "adir", "alpha.txt", DisplayName = "DIRCMD /O:GN dirs first then alpha")]
    public async Task Dir_Dircmd_SortOrder_Combinations(string dircmdValue, string cmdLineFlag, string firstExpected, string secondExpected)
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "alpha.txt", false);
        fs.AddEntry('C', [], "zebra.txt", false);
        fs.AddEntry('C', [], "file.bat", false);
        fs.AddEntry('C', [], "file.txt", false);
        fs.AddEntry('C', [], "small.txt", false, size: 100);
        fs.AddEntry('C', [], "big.txt", false, size: 9000);
        fs.AddEntry('C', [], "zfile.txt", false);
        fs.AddEntry('C', [], "adir", true);
        var (cmd, console, bc, ctx) = Setup(fs);
        ctx.EnvironmentVariables["DIRCMD"] = dircmdValue;

        var tokens = string.IsNullOrWhiteSpace(cmdLineFlag)
            ? new IToken[] { Token.Text("/B") }
            : new IToken[] { Token.Text("/B"), Token.Whitespace(" "), Token.Text(cmdLineFlag) };

        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(tokens), bc, []);
        var lines = console.OutLines.ToList();
        Assert.IsTrue(lines.IndexOf(firstExpected) < lines.IndexOf(secondExpected),
            $"{firstExpected} should appear before {secondExpected}");
    }

    [TestMethod]
    [DataRow("/N", "", "<DIR>", DisplayName = "DIRCMD /N shows <DIR> marker")]
    [DataRow("/N", "/-N", "<DIR>", DisplayName = "DIRCMD /N overridden still shows dirs")]
    public async Task Dir_Dircmd_NewLongFormat_Combinations(string dircmdValue, string cmdLineFlag, string expectedMarker)
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "subdir", true);
        var (cmd, console, bc, ctx) = Setup(fs);
        ctx.EnvironmentVariables["DIRCMD"] = dircmdValue;

        var tokens = string.IsNullOrWhiteSpace(cmdLineFlag)
            ? Array.Empty<IToken>()
            : new IToken[] { Token.Text(cmdLineFlag) };

        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(tokens), bc, []);
        Assert.IsTrue(console.OutText.Contains(expectedMarker));
    }
}

[TestClass]
public class ShortNameTests
{
    [TestMethod]
    public void DosFileSystem_EnumerateEntries_IncludesShortNamesWhenPresent()
    {
        if (!OperatingSystem.IsWindows()) return;

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(tempDir);
            var longFileName = "This is a very long file name that exceeds 8.3 format.txt";
            File.WriteAllText(Path.Combine(tempDir, longFileName), "test");

            var fs = new DosFileSystem(new Dictionary<char, string> { ['T'] = tempDir + "\\" });
            var entries = fs.EnumerateEntries('T', [], "*").ToList();

            Assert.IsTrue(entries.Count > 0, "Should find at least one entry");
            var entry = entries.FirstOrDefault(e => e.Name == longFileName);
            Assert.IsNotNull(entry.Name, $"Should find '{longFileName}'. Found: {string.Join(", ", entries.Select(e => e.Name))}");
            Assert.IsTrue(entry.ShortName.Length > 0, $"Long filename should have a short name. Got: '{entry.ShortName}'");
            Assert.IsTrue(entry.ShortName.Length <= 12, $"Short name should be 8.3 format, got length {entry.ShortName.Length}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [TestMethod]
    public void TestFileSystem_EnumerateEntries_ReturnsSetShortName()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "LongFileName.txt", false);
        fs.SetShortName('C', ["LongFileName.txt"], "LONGFI~1.TXT");

        var entries = fs.EnumerateEntries('C', [], "*").ToList();
        var entry = entries.Single(e => e.Name == "LongFileName.txt");
        Assert.AreEqual("LONGFI~1.TXT", entry.ShortName);
    }

    [TestMethod]
    public void TestFileSystem_EnumerateEntries_NoShortNameSet_ReturnsEmpty()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "file.txt", false);

        var entries = fs.EnumerateEntries('C', [], "*").ToList();
        var entry = entries.Single(e => e.Name == "file.txt");
        Assert.AreEqual("", entry.ShortName);
    }
}

[TestClass]
public class DirQFlagTests
{
    private static (DirCommand cmd, TestConsole console, BatchContext bc) Setup(TestFileSystem fs)
    {
        var cmd = new DirCommand();
        var console = new TestConsole();
        var ctx = new TestCommandContext(fs);
        ctx.SetCurrentDrive('C');
        ctx.SetPath('C', []);
        return (cmd, console, new BatchContext { Console = console, Context = ctx });
    }

    [TestMethod]
    public async Task Dir_SlashQ_FileWithOwner_ShowsOwner()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "file.txt", false, owner: "BG\\kempsb");
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/Q")), bc, []);
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("BG\\kempsb") && l.Contains("file.txt")));
    }

    [TestMethod]
    public async Task Dir_SlashQ_DirectoryWithOwner_ShowsOwner()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "subdir", true, owner: "BG\\kempsb");
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/Q")), bc, []);
        Assert.IsTrue(console.OutLines.Any(l => l.Contains("BG\\kempsb") && l.Contains("subdir")));
    }

    [TestMethod]
    public async Task Dir_SlashQ_FileWithoutOwner_ShowsBlankOwnerField()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "file.txt", false);
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/Q")), bc, []);
        var fileLine = console.OutLines.First(l => l.Contains("file.txt"));
        Assert.IsTrue(fileLine.Contains("                       "));
    }

    [TestMethod]
    public async Task Dir_SlashQ_WithB_ShowsOnlyNames()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "file.txt", false, owner: "BG\\kempsb");
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(
            TestArgs.For<DirCommand>(Token.Text("/Q"), Token.Whitespace(" "), Token.Text("/B")), bc, []);
        Assert.IsTrue(console.OutLines.Contains("file.txt"));
        Assert.IsFalse(console.OutLines.Any(l => l.Contains("BG\\kempsb")));
    }

    [TestMethod]
    public async Task Dir_SlashQ_WithW_DoesNotCrash()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "file.txt", false, owner: "BG\\kempsb");
        var (cmd, console, bc) = Setup(fs);
        var result = await cmd.ExecuteAsync(
            TestArgs.For<DirCommand>(Token.Text("/Q"), Token.Whitespace(" "), Token.Text("/W")), bc, []);
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public async Task Dir_CompoundFlags_QX_SplitsIntoSeparateFlags()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "LongFileName.txt", false, owner: "DOMAIN\\User");
        fs.SetShortName('C', ["LongFileName.txt"], "LONGFI~1.TXT");
        var (cmd, console, bc) = Setup(fs);
        await cmd.ExecuteAsync(TestArgs.For<DirCommand>(Token.Text("/Q/X")), bc, []);
        var hasFile = console.OutLines.Any(l => l.Contains("LongFileName.txt"));
        Assert.IsTrue(hasFile, "Should list the file, not try to navigate to /Q/X path");
        if (hasFile)
        {
            var fileLine = console.OutLines.First(l => l.Contains("LongFileName.txt"));
            Assert.IsTrue(fileLine.Contains("DOMAIN\\User") || fileLine.Contains("LONGFI~1.TXT"),
                "Should show either owner or short name when compound flags are recognized");
        }
    }
}

[TestClass]
public class ExecutableTypeDetectorTests : IDisposable
{
    private readonly string _testDir = Path.Combine(Path.GetTempPath(), $"BatDetectorTest_{Guid.NewGuid():N}");

    public ExecutableTypeDetectorTests() => Directory.CreateDirectory(_testDir);

    public void Dispose()
    {
        if (Directory.Exists(_testDir)) Directory.Delete(_testDir, recursive: true);
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_testDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [TestMethod]
    public void GetExecutableType_TextFile_ReturnsDocument()
    {
        var path = Write("hi.txt", "hello world");
        Assert.AreEqual(ExecutableType.Document, ExecutableTypeDetector.GetExecutableType(path));
    }

    [TestMethod]
    public void GetExecutableType_JsonFile_ReturnsDocument()
    {
        var path = Write("config.json", """{"key":"value"}""");
        Assert.AreEqual(ExecutableType.Document, ExecutableTypeDetector.GetExecutableType(path));
    }

    [TestMethod]
    public void GetExecutableType_EmptyFile_ReturnsDocument()
    {
        var path = Write("empty.txt", "");
        Assert.AreEqual(ExecutableType.Document, ExecutableTypeDetector.GetExecutableType(path));
    }

    [TestMethod]
    public void GetExecutableType_NonExistentFile_ReturnsUnknown()
    {
        var path = Path.Combine(_testDir, "doesnotexist.txt");
        Assert.AreEqual(ExecutableType.Unknown, ExecutableTypeDetector.GetExecutableType(path));
    }

    [TestMethod]
    public void GetExecutableType_BatchFile_ReturnsDocument()
    {
        // A .bat file with plain text content (no MZ header) is a Document at the header level.
        // The dispatcher handles .bat by extension before reaching the type detector.
        var path = Write("script.bat", "@echo off\r\necho hello");
        Assert.AreEqual(ExecutableType.Document, ExecutableTypeDetector.GetExecutableType(path));
    }
}

