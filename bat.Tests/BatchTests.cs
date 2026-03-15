using System.Threading;
using System.IO.Abstractions.TestingHelpers;
using Bat.Commands;
using Bat.FileSystem;
using Spectre.Console.Testing;
using Xunit;

namespace Bat.Tests;

public class BatchTests
{
    private (CommandDispatcher dispatcher, FileSystemService service, TestConsole console, MockFileSystem fs) Setup()
    {
        var fs = new MockFileSystem();
        var service = new FileSystemService(fs);
        var dispatcher = new CommandDispatcher(service);
        var console = new TestConsole();
        return (dispatcher, service, console, fs);
    }

    [Fact]
    public async Task Batch_ShouldHandleLabelsAndGoto()
    {
        var (dispatcher, service, console, fs) = Setup();
        var batchContent = @"@echo off
goto LABEL2
echo Skipped
:LABEL1
echo Inside LABEL1
goto EOF
:LABEL2
echo Inside LABEL2
goto LABEL1";
        fs.AddFile("C:\\test.bat", new MockFileData(batchContent));

        await dispatcher.DispatchAsync("C:\\test.bat", default, console);

        var output = console.Output.Trim().Replace("\r\n", "\n");
        Assert.Contains("Inside LABEL2", output);
        Assert.Contains("Inside LABEL1", output);
        Assert.DoesNotContain("Skipped", output);
    }

    [Fact]
    public async Task Batch_ShouldHandleIfElse()
    {
        var (dispatcher, service, console, fs) = Setup();
        var batchContent = @"@echo off
if ""a""==""a"" echo a is a
if not ""a""==""b"" echo a is not b
if exist C:\test.bat (
  echo exist
) else (
  echo not exist
)";
        fs.AddFile("C:\\test.bat", new MockFileData(batchContent));

        await dispatcher.DispatchAsync("C:\\test.bat", default, console);

        var output = console.Output.Trim().Replace("\r\n", "\n");
        Assert.Contains("a is a", output);
        Assert.Contains("a is not b", output);
        Assert.Contains("exist", output);
    }
    
    [Fact]
    public async Task Batch_ShouldHandleAtPrefix()
    {
        var (dispatcher, service, console, fs) = Setup();
        // Global echo is ON by default
        var batchContent = @"echo visible
@echo hidden";
        fs.AddFile("C:\\test.bat", new MockFileData(batchContent));

        await dispatcher.DispatchAsync("C:\\test.bat", default, console);

        var output = console.Output.Trim().Replace("\r\n", "\n");
        // ""echo visible"" should be echoed because echo is ON
        Assert.Contains("echo visible\nvisible", output);
        // ""echo hidden"" should NOT be echoed because of @
        Assert.DoesNotContain("echo hidden", output);
        Assert.Contains("hidden", output);
    }

    [Fact]
    public async Task Batch_ShouldHandleEchoOnOff()
    {
        var (dispatcher, service, console, fs) = Setup();
        var batchContent = @"echo on
echo 1
echo off
echo 2";
        fs.AddFile("C:\\test.bat", new MockFileData(batchContent));

        await dispatcher.DispatchAsync("C:\\test.bat", default, console);

        var output = console.Output.Trim().Replace("\r\n", "\n");
        Assert.Contains("echo 1\n1", output);
        Assert.DoesNotContain("echo 2", output);
        Assert.Contains("2", output);
    }
}
