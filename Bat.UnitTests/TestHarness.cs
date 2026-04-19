using Bat.Console;
using Bat.Execution;
using Bat.Parsing;

namespace Bat.UnitTests;

/// <summary>
/// Shared test infrastructure for executing commands through the full Dispatcher pipeline.
/// Reduces boilerplate in integration and redirection tests.
/// </summary>
internal class TestHarness
{
    public TestFileSystem FileSystem { get; } = new();
    public TestConsole Console { get; }
    public TestCommandContext Context { get; }
    public Dispatcher Dispatcher { get; } = new();

    public TestHarness(string input = "", char drive = 'C', string[]? path = null)
    {
        Console = new(input);
        Context = new(FileSystem)
        {
            Console = Console
        };
        Context.SetCurrentDrive(drive);
        FileSystem.AddDir(drive, []);
        if (path != null) Context.SetPath(drive, path);

        // Clear the shared REPL singleton to ensure test isolation
        ReplBatchContext.Reset();
    }

    public async Task<bool> Execute(string command, int timeoutMs = 5000)
    {
        var cmd = Parser.Parse(command);
        var task = Dispatcher.ExecuteCommandAsync(Context, cmd);
        
        if (await Task.WhenAny(task, Task.Delay(timeoutMs)) == task)
        {
            return await task;
        }

        throw new TimeoutException($"Command execution timed out after {timeoutMs}ms: {command}");
    }
}
