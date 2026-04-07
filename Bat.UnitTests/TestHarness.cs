using Bat.Console;
using Bat.Execution;
using Bat.Parsing;
using Context;

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
        Console = new TestConsole(input);
        Context = new TestCommandContext(FileSystem);
        Context.SetCurrentDrive(drive);
        FileSystem.AddDir(drive, []);
        if (path != null) Context.SetPath(drive, path);

        // Clear the shared REPL singleton to ensure test isolation
        ReplBatchContext.Value.SetLocalStack.Clear();
    }

    public async Task<bool> Execute(string command)
    {
        var cmd = Parser.Parse(command);
        return await Dispatcher.ExecuteCommandAsync(Context, Console, cmd);
    }
}
