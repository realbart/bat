using System.Threading;
using System.IO.Abstractions.TestingHelpers;
using Bat.Commands;
using Bat.FileSystem;
using Spectre.Console.Testing;
using Xunit;

namespace Bat.Tests;

public class CommandTests
{
    [Fact]
    public async Task DirCommand_WithWideFlag_ShouldShowColumns()
    {
        // Arrange
        var (service, console, fs) = Setup();
        fs.AddFile("/test/a.txt", new MockFileData(""));
        fs.AddFile("/test/b.txt", new MockFileData(""));
        fs.AddFile("/test/c.txt", new MockFileData(""));
        fs.AddDirectory("/test/subdir");
        service.ChangeDirectory("/test");
        var command = new DirCommand();

        // Act
        await command.ExecuteAsync(new[] { "/W" }, service, console, CancellationToken.None);

        // Assert
        Assert.Contains("[subdir]", console.Output);
        Assert.Contains("a.txt", console.Output);
        Assert.Contains("b.txt", console.Output);
        Assert.Contains("c.txt", console.Output);
    }

    [Fact]
    public async Task DirCommand_WithCaseInsensitiveWildcard_ShouldFindFiles()
    {
        // Arrange
        var (service, console, fs) = Setup();
        fs.AddFile("/test/MyFile.Dll", new MockFileData(""));
        service.ChangeDirectory("/test");
        var command = new DirCommand();

        // Act
        await command.ExecuteAsync(new[] { "*.dll" }, service, console, CancellationToken.None);

        // Assert
        Assert.Contains("MyFile.Dll", console.Output);
    }

    [Fact]
    public async Task CopyCommand_ShouldPreserveCasing_WhenOverwriting()
    {
        // Arrange
        var (service, console, fs) = Setup();
        fs.AddFile("/Foo.txt", new MockFileData("original"));
        fs.AddFile("/bar.txt", new MockFileData("new"));
        var command = new CopyCommand();

        // Act - copy bar.txt to foo.txt (lowercase target, but Foo.txt exists)
        // Add /Y to skip the prompt
        await command.ExecuteAsync(new[] { "bar.txt", "foo.txt", "/Y" }, service, console, CancellationToken.None);

        // Assert
        Assert.True(fs.File.Exists("/Foo.txt"));
        // On Linux MockFileSystem, foo.txt might still exist if we didn't handle it, 
        // but our GetCaseInsensitiveMatch should have returned /Foo.txt
        var files = fs.Directory.GetFiles("/");
        Assert.Contains("/Foo.txt", files);
        Assert.DoesNotContain("/foo.txt", files.Where(f => !f.Equals("/Foo.txt")));
        Assert.Equal("new", fs.File.ReadAllText("/Foo.txt"));
    }

    [Fact]
    public async Task Prompt_Format_ShouldSupportPAndG()
    {
        // Arrange
        var (service, _, _) = Setup();
        service.SetEnvironmentVariable("PROMPT", "$P$G");
        service.ChangeDirectory("/test");

        // Act
        var prompt = service.FormatPrompt();

        // Assert
        Assert.Equal("C:\\test>", prompt);
    }

    [Fact]
    public async Task CopyCommand_ShouldConcatenateFiles()
    {
        // Arrange
        var (service, console, fs) = Setup();
        fs.File.WriteAllText("/file1.txt", "part1 ");
        fs.File.WriteAllText("/file2.txt", "part2");
        var command = new CopyCommand();

        // Act
        await command.ExecuteAsync(new[] { "file1.txt+file2.txt", "combined.txt" }, service, console, CancellationToken.None);

        // Assert
        Assert.Equal("part1 part2", fs.File.ReadAllText("/combined.txt"));
    }

    private (FileSystemService service, TestConsole console, MockFileSystem fs) Setup()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { "/test/file.txt", new MockFileData("hello world") },
            { "/Test/Case.txt", new MockFileData("case preserved") },
            { "/EmptyDir", new MockDirectoryData() }
        }, "/");
        var service = new FileSystemService(fs);
        // Ensure current directory is root
        service.ChangeDirectory("/");
        var console = new TestConsole();
        return (service, console, fs);
    }

    [Theory]
    [InlineData(typeof(CdCommand), "cd")]
    [InlineData(typeof(DirCommand), "dir")]
    [InlineData(typeof(MdCommand), "md")]
    [InlineData(typeof(RdCommand), "rd")]
    [InlineData(typeof(CopyCommand), "copy")]
    [InlineData(typeof(DelCommand), "del")]
    [InlineData(typeof(RenCommand), "ren")]
    [InlineData(typeof(TypeCommand), "type")]
    [InlineData(typeof(ClsCommand), "cls")]
    [InlineData(typeof(DateCommand), "date")]
    [InlineData(typeof(TimeCommand), "time")]
    [InlineData(typeof(VerCommand), "ver")]
    [InlineData(typeof(VolCommand), "vol")]
    [InlineData(typeof(ExitCommand), "exit")]
    [InlineData(typeof(EchoCommand), "echo")]
    [InlineData(typeof(MoveCommand), "move")]
    [InlineData(typeof(PauseCommand), "pause")]
    [InlineData(typeof(RemCommand), "rem")]
    [InlineData(typeof(TitleCommand), "title")]
    [InlineData(typeof(ColorCommand), "color")]
    [InlineData(typeof(SetCommand), "set")]
    [InlineData(typeof(PathCommand), "path")]
    [InlineData(typeof(PromptCommand), "prompt")]
    [InlineData(typeof(PushdCommand), "pushd")]
    [InlineData(typeof(PopdCommand), "popd")]
    [InlineData(typeof(AssocCommand), "assoc")]
    [InlineData(typeof(FtypeCommand), "ftype")]
    [InlineData(typeof(MklinkCommand), "mklink")]
    [InlineData(typeof(IfCommand), "if")]
    [InlineData(typeof(GotoCommand), "goto")]
    [InlineData(typeof(CallCommand), "call")]
    [InlineData(typeof(ShiftCommand), "shift")]
    [InlineData(typeof(SetlocalCommand), "setlocal")]
    [InlineData(typeof(EndlocalCommand), "endlocal")]
    [InlineData(typeof(ForCommand), "for")]
    [InlineData(typeof(StartCommand), "start")]
    [InlineData(typeof(VerifyCommand), "verify")]
    [InlineData(typeof(BreakCommand), "break")]
    public async Task AllCommands_ShouldShowHelp_WithSlashQuestionMark(Type commandType, string name)
    {
        // Arrange
        var (service, console, _) = Setup();
        var command = (ICommand)Activator.CreateInstance(commandType)!;

        // Act
        await command.ExecuteAsync(new[] { "/?" }, service, console, CancellationToken.None);

        // Assert
        var expected = command.HelpText.Trim();
        if (expected.Length > 20) expected = expected.Substring(0, 20);
        Assert.Contains(expected, console.Output);
    }

    [Fact]
    public async Task CdCommand_ShouldChangeDirectory_WithCasePreservation()
    {
        // Arrange
        var (service, console, _) = Setup();
        var command = new CdCommand();

        // Act
        await command.ExecuteAsync(new[] { "test" }, service, console, CancellationToken.None);

        // Assert
        Assert.Equal(@"C:\test", service.CurrentDirectory);
        
        // Act back
        await command.ExecuteAsync(new[] { ".." }, service, console, CancellationToken.None);
        Assert.Equal(@"C:\", service.CurrentDirectory);

        // Act case-insensitive
        await command.ExecuteAsync(new[] { "TEST" }, service, console, CancellationToken.None);
        Assert.Equal(@"C:\test", service.CurrentDirectory);
    }

    [Fact]
    public async Task MdCommand_ShouldCreateDirectory()
    {
        // Arrange
        var (service, console, fs) = Setup();
        var command = new MdCommand();

        // Act
        await command.ExecuteAsync(new[] { "newdir" }, service, console, CancellationToken.None);

        // Assert
        Assert.True(fs.Directory.Exists("/newdir"));
    }

    [Fact]
    public async Task RdCommand_ShouldRemoveDirectory()
    {
        // Arrange
        var (service, console, fs) = Setup();
        var command = new RdCommand();

        // Act
        await command.ExecuteAsync(new[] { "EmptyDir" }, service, console, CancellationToken.None);

        // Assert
        Assert.False(fs.Directory.Exists("/EmptyDir"));
    }

    [Fact]
    public async Task CopyCommand_ShouldPromptWhenFileExists()
    {
        // Arrange
        var (service, console, fs) = Setup();
        var command = new CopyCommand();
        fs.File.WriteAllText("/test/target.txt", "old content");
        
        // Mock user input "No" using ConsoleKey
        console.Input.PushKey(ConsoleKey.N);
        console.Input.PushKey(ConsoleKey.Enter);

        // Act
        await command.ExecuteAsync(new[] { "test/file.txt", "test/target.txt" }, service, console, CancellationToken.None);

        // Assert
        Assert.Equal("old content", fs.File.ReadAllText("/test/target.txt"));
        Assert.Contains("Overwrite", console.Output);
    }

    [Fact]
    public async Task CopyCommand_ShouldOverwriteWithSlashY()
    {
        // Arrange
        var (service, console, fs) = Setup();
        var command = new CopyCommand();
        fs.File.WriteAllText("/test/target.txt", "old content");

        // Act
        await command.ExecuteAsync(new[] { "test/file.txt", "test/target.txt", "/Y" }, service, console, CancellationToken.None);

        // Assert
        Assert.Equal("hello world", fs.File.ReadAllText("/test/target.txt"));
        Assert.DoesNotContain("Overwrite", console.Output);
    }

    [Fact]
    public async Task DelCommand_ShouldDeleteFile()
    {
        // Arrange
        var (service, console, fs) = Setup();
        var command = new DelCommand();

        // Act
        await command.ExecuteAsync(new[] { "test/file.txt" }, service, console, CancellationToken.None);

        // Assert
        Assert.False(fs.File.Exists("/test/file.txt"));
    }

    [Fact]
    public async Task RenCommand_ShouldRenameFile()
    {
        // Arrange
        var (service, console, fs) = Setup();
        var command = new RenCommand();

        // Act
        await command.ExecuteAsync(new[] { "/test/file.txt", "newname.txt" }, service, console, CancellationToken.None);

        // Assert
        Assert.False(fs.File.Exists("/test/file.txt"));
        Assert.True(fs.File.Exists("/test/newname.txt"));
    }

    [Fact]
    public async Task TypeCommand_ShouldShowFileContent()
    {
        // Arrange
        var (service, console, _) = Setup();
        var command = new TypeCommand();

        // Act
        await command.ExecuteAsync(new[] { "/test/file.txt" }, service, console, CancellationToken.None);

        // Assert
        Assert.Contains("hello world", (string)console.Output);
    }

    [Fact]
    public async Task VerCommand_ShouldShowVersion()
    {
        // Arrange
        var (service, console, _) = Setup();
        var command = new VerCommand();

        // Act
        await command.ExecuteAsync(Array.Empty<string>(), service, console, CancellationToken.None);

        // Assert
        Assert.Contains("0.1.0", (string)console.Output);
    }

    [Fact]
    public async Task DirCommand_ShouldSupportWildcardsAndRecursiveSearch()
    {
        // Arrange
        var (service, console, fs) = Setup();
        fs.AddFile("/test/subdir/file2.txt", new MockFileData("hello again"));
        fs.AddFile("/test/other.dll", new MockFileData("binary content"));
        var command = new DirCommand();

        // Act - Wildcard
        await command.ExecuteAsync(new[] { "test/*.txt" }, service, console, CancellationToken.None);
        var output = console.Output;
        Assert.Contains("file.txt", output);
        Assert.DoesNotContain("other.dll", output);
        // TestConsole in Spectre.Console.Testing doesn't have a parameterless Clear. 
        // We'll just check for unique strings or ignore the previous output if needed.
        
        // Act - Recursive
        var recursiveConsole = new TestConsole();
        await command.ExecuteAsync(new[] { "/S", "/B" }, service, recursiveConsole, CancellationToken.None);
        output = recursiveConsole.Output;
        Assert.Contains("C:\\test\\file.txt", output);
        Assert.Contains("C:\\test\\subdir\\file2.txt", output);
    }

    [Fact]
    public async Task DelCommand_ShouldSupportWildcardsAndRecursive()
    {
        // Arrange
        var (service, console, fs) = Setup();
        fs.AddFile("/test/subdir/file2.txt", new MockFileData("hello again"));
        fs.AddFile("/test/delete_me.tmp", new MockFileData("temp"));
        var command = new DelCommand();

        // Act - Wildcard
        await command.ExecuteAsync(new[] { "test/*.tmp" }, service, console, CancellationToken.None);
        Assert.False(fs.File.Exists("/test/delete_me.tmp"));
        Assert.True(fs.File.Exists("/test/file.txt"));

        // Act - Recursive
        await command.ExecuteAsync(new[] { "/S", "file2.txt" }, service, console, CancellationToken.None);
        Assert.False(fs.File.Exists("/test/subdir/file2.txt"));
    }

    [Fact]
    public async Task PushdPopd_ShouldChangeDirectoryStack()
    {
        // Arrange
        var (service, console, _) = Setup();
        var pushd = new PushdCommand();
        var popd = new PopdCommand();

        // Act
        await pushd.ExecuteAsync(new[] { "test" }, service, console, CancellationToken.None);
        Assert.Equal("C:\\test", service.CurrentDirectory);

        await popd.ExecuteAsync(Array.Empty<string>(), service, console, CancellationToken.None);
        Assert.Equal("C:\\", service.CurrentDirectory);
    }

    [Fact]
    public async Task SetCommand_ShouldManageEnvironmentVariables()
    {
        // Arrange
        var (service, console, _) = Setup();
        var command = new SetCommand();

        // Act - Set
        await command.ExecuteAsync(new[] { "MYVAR=hello" }, service, console, CancellationToken.None);
        Assert.Equal("hello", service.GetEnvironmentVariable("MYVAR"));

        // Act - Display
        var displayConsole = new TestConsole();
        await command.ExecuteAsync(new[] { "MYVAR" }, service, displayConsole, CancellationToken.None);
        Assert.Contains("MYVAR=hello", displayConsole.Output);
    }

    [Fact]
    public async Task DirCommand_ShouldCancel_WhenTokenIsCancelled()
    {
        // Arrange
        var (service, console, fs) = Setup();
        // Voeg veel bestanden toe om te zorgen dat het even duurt (hoewel we de token handmatig kunnen setten)
        for (var i = 0; i < 100; i++)
        {
            fs.AddFile($"/test/file{i}.txt", new MockFileData("content"));
        }
        var command = new DirCommand();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Direct annuleren

        // Act
        await command.ExecuteAsync(new[] { "test/*" }, service, console, cts.Token);

        // Assert
        // Als het direct geannuleerd is, zou de output leeg moeten zijn (of in ieder geval niet alle 100 bestanden bevatten)
        // De header wordt wel geprint voordat de loop begint, maar de bestanden niet.
        Assert.DoesNotContain("file99.txt", console.Output);
    }

    [Fact]
    public async Task MoveCommand_ShouldMoveFileAndDirectory()
    {
        // Arrange
        var (service, console, fs) = Setup();
        var command = new MoveCommand();

        // Act - Move File
        await command.ExecuteAsync(new[] { "test/file.txt", "moved.txt" }, service, console, CancellationToken.None);
        Assert.False(fs.File.Exists("/test/file.txt"));
        Assert.True(fs.File.Exists("/moved.txt"));

        // Act - Rename Directory
        await command.ExecuteAsync(new[] { "EmptyDir", "NewDir" }, service, console, CancellationToken.None);
        Assert.False(fs.Directory.Exists("/EmptyDir"));
        Assert.True(fs.Directory.Exists("/NewDir"));
    }

    [Fact]
    public async Task ResolvePath_ShouldPreserveCase()
    {
        // Arrange
        var (service, _, _) = Setup();

        // Act
        var result = service.ResolvePath("/TEST/CASE.TXT");

        // Assert
        Assert.Equal("C:\\Test\\Case.txt", result);
    }
    [Fact]
    public async Task DirCommand_Attributes_ShouldFilterCorrectly()
    {
        // Arrange
        var (service, console, fs) = Setup();
        var command = new DirCommand();
        fs.File.Create("/hidden.txt");
        fs.File.SetAttributes("/hidden.txt", System.IO.FileAttributes.Hidden);
        fs.File.Create("/normal.txt");

        // Act - Default (no hidden)
        await command.ExecuteAsync(new string[] { }, service, console, CancellationToken.None);
        Assert.Contains("normal.txt", console.Output);
        Assert.DoesNotContain("hidden.txt", console.Output);

        // Act - Show hidden
        var (service2, console2, fs2) = Setup();
        fs2.File.Create("/hidden.txt");
        fs2.File.SetAttributes("/hidden.txt", System.IO.FileAttributes.Hidden);
        fs2.File.Create("/normal.txt");
        await command.ExecuteAsync(new[] { "/AH" }, service2, console2, CancellationToken.None);
        Assert.Contains("hidden.txt", console2.Output);
        Assert.DoesNotContain("normal.txt", console2.Output);
    }

    [Fact]
    public async Task DirCommand_Sorting_ShouldSortByName()
    {
        // Arrange
        var (service, console, fs) = Setup();
        var command = new DirCommand();
        fs.File.Create("/b.txt");
        fs.File.Create("/a.txt");
        fs.File.Create("/c.txt");

        // Act
        await command.ExecuteAsync(new[] { "/ON" }, service, console, CancellationToken.None);
        
        // Assert order in output (very basic check)
        var output = console.Output;
        Assert.True(output.IndexOf("a.txt") < output.IndexOf("b.txt"));
        Assert.True(output.IndexOf("b.txt") < output.IndexOf("c.txt"));
    }

    [Fact]
    public async Task DirCommand_Folder_ShouldShowContents()
    {
        // Arrange
        var (service, console, fs) = Setup();
        var command = new DirCommand();
        fs.Directory.CreateDirectory("/testfolder");
        fs.File.Create("/testfolder/inside.txt");

        // Act
        await command.ExecuteAsync(new[] { "testfolder" }, service, console, CancellationToken.None);

        // Assert
        Assert.Contains("inside.txt", console.Output);
        Assert.Contains("Directory of C:\\testfolder", console.Output);
    }

    [Fact]
    public async Task EchoCommand_ShouldExpandVariables()
    {
        // Arrange
        var (service, console, fs) = Setup();
        service.SetEnvironmentVariable("TESTVAR", "hello");
        var dispatcher = new CommandDispatcher(service);

        // Act
        await dispatcher.DispatchAsync("echo %TESTVAR%", consoleOverride: console);

        // Assert
        Assert.Contains("hello", console.Output);
    }

    [Fact]
    public async Task EchoCommand_ShouldExpandVariables_CaseInsensitive()
    {
        // Arrange
        var (service, console, fs) = Setup();
        service.SetEnvironmentVariable("TESTVAR", "hello");
        var dispatcher = new CommandDispatcher(service);

        // Act
        await dispatcher.DispatchAsync("echo %testvar%", consoleOverride: console);

        // Assert
        Assert.Contains("hello", console.Output);
    }

    [Fact]
    public async Task EchoCommand_ShouldLeaveUnknownVariables()
    {
        // Arrange
        var (service, console, fs) = Setup();
        var dispatcher = new CommandDispatcher(service);

        // Act
        await dispatcher.DispatchAsync("echo %UNKNOWN%", consoleOverride: console);

        // Assert
        Assert.Contains("%UNKNOWN%", console.Output);
    }

    [Fact]
    public async Task Dispatcher_ShouldSupportRedirection()
    {
        // Arrange
        var (service, console, fs) = Setup();
        var dispatcher = new CommandDispatcher(service);

        // Act
        await dispatcher.DispatchAsync("echo hello > test.txt", consoleOverride: console);

        // Assert
        Assert.True(fs.File.Exists("/test.txt"));
        Assert.Equal("hello" + Environment.NewLine, fs.File.ReadAllText("/test.txt"));
    }

    [Fact]
    public async Task Dispatcher_ShouldSupportRedirectionNoSpace()
    {
        // Arrange
        var (service, console, fs) = Setup();
        var dispatcher = new CommandDispatcher(service);

        // Act
        await dispatcher.DispatchAsync("echo hi>hi.txt", consoleOverride: console);

        // Assert
        Assert.True(fs.File.Exists("/hi.txt"));
        Assert.Equal("hi" + Environment.NewLine, fs.File.ReadAllText("/hi.txt"));
    }

    [Fact]
    public async Task CopyCommand_ShouldSupportCopyCon()
    {
        // Arrange
        var (service, console, fs) = Setup();
        var command = new CopyCommand();
        
        // Simuleer Console.In voor de test
        var reader = new StringReader("Line 1\nLine 2\n\x1a");
        var originalIn = Console.In;
        Console.SetIn(reader);
        
        try
        {
            // Act
            await command.ExecuteAsync(new[] { "con:", "test.txt" }, service, console, CancellationToken.None);

            // Assert
            Assert.True(fs.File.Exists("/test.txt"));
            var content = fs.File.ReadAllLines("/test.txt");
            Assert.Equal(2, content.Length);
            Assert.Equal("Line 1", content[0]);
            Assert.Equal("Line 2", content[1]);
            Assert.Contains("1 file(s) copied", console.Output);
        }
        finally
        {
            Console.SetIn(originalIn);
        }
    }

    [Fact]
    public async Task DirCommand_ShouldShowHiddenFilesWithSlashA()
    {
        // Arrange
        var (service, console, fs) = Setup();
        var command = new DirCommand();
        fs.File.WriteAllText("/hidden.txt", "content");
        fs.File.SetAttributes("/hidden.txt", FileAttributes.Hidden);

        // Act - Without /A
        await command.ExecuteAsync(new[] { "hidden.txt" }, service, console, CancellationToken.None);
        Assert.DoesNotContain("hidden.txt", console.Output);

        // Act - With /A
        await command.ExecuteAsync(new[] { "hidden.txt", "/A" }, service, console, CancellationToken.None);
        
        // Assert
        Assert.Contains("hidden.txt", console.Output);
    }

    [Fact]
    public async Task DirCommand_ShouldShowSymlinksAndJunctions()
    {
        // Arrange
        var (service, console, fs) = Setup();
        var command = new DirCommand();
        
        // Mock a directory symlink
        fs.Directory.CreateDirectory("/target");
        fs.Directory.CreateDirectory("/linkd");
        var attr = fs.File.GetAttributes("/linkd");
        fs.File.SetAttributes("/linkd", attr | FileAttributes.ReparsePoint);
        
        // Act
        await command.ExecuteAsync(new string[] { }, service, console, CancellationToken.None);

        // Assert
        Assert.Contains("<SYMLINKD>", console.Output);
        Assert.Contains("linkd", console.Output);
    }

    [Fact]
    public async Task Dispatcher_ShouldExpandMacros_WithParameters()
    {
        // Arrange
        var (service, console, fs) = Setup();
        var dispatcher = new CommandDispatcher(service);

        // Set up macro via protocol simulation (normally done by doskey)
        // We'll use reflection or direct access to _macros for testing
        var macrosField = typeof(CommandDispatcher).GetField("_macros",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var macros = (Dictionary<string, Dictionary<string, string>>)macrosField!.GetValue(dispatcher)!;
        macros["bat"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "greet", "echo Hello $1!" }
        };

        // Act
        await dispatcher.DispatchAsync("greet World", consoleOverride: console);

        // Assert
        Assert.Contains("Hello World!", console.Output);
    }

    [Fact]
    public async Task Dispatcher_ShouldExpandMacros_WithAllParameters()
    {
        // Arrange
        var (service, console, fs) = Setup();
        var dispatcher = new CommandDispatcher(service);

        var macrosField = typeof(CommandDispatcher).GetField("_macros",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var macros = (Dictionary<string, Dictionary<string, string>>)macrosField!.GetValue(dispatcher)!;
        macros["bat"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "showargs", "echo Args: $*" }
        };

        // Act
        await dispatcher.DispatchAsync("showargs one two three", consoleOverride: console);

        // Assert
        Assert.Contains("Args: one two three", console.Output);
    }

    [Fact]
    public async Task Dispatcher_ShouldExpandMacros_MultiCommand()
    {
        // Arrange
        var (service, console, fs) = Setup();
        var dispatcher = new CommandDispatcher(service);

        var macrosField = typeof(CommandDispatcher).GetField("_macros",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var macros = (Dictionary<string, Dictionary<string, string>>)macrosField!.GetValue(dispatcher)!;
        macros["bat"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "multi", "echo First $T echo Second" }
        };

        // Act
        await dispatcher.DispatchAsync("multi", consoleOverride: console);

        // Assert
        Assert.Contains("First", console.Output);
        Assert.Contains("Second", console.Output);
    }
}
