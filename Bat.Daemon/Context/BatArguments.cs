namespace Bat.Context;

public enum BatMode
{
    Windows,
    Unix
}

public enum BatExitBehavior
{
    Repl,
    TerminateAfterCommand,
    KeepAliveAfterCommand
}

public enum OutputEncoding
{
    Default,
    Ansi,
    Unicode
}

public record BatArguments
{
    public BatMode Mode { get; init; }
    public BatExitBehavior ExitBehavior { get; init; } = BatExitBehavior.Repl;
    public string? Command { get; init; }
    public string? BatchFile { get; init; }
    public bool EchoEnabled { get; init; } = true;
    public bool DelayedExpansion { get; init; }
    public bool ExtensionsEnabled { get; init; } = true;
    public bool FilenameCompletion { get; init; }
    public bool ShowHelp { get; init; }
    public bool SuppressBanner { get; init; }
    public OutputEncoding OutputEncoding { get; init; } = OutputEncoding.Default;
    public Dictionary<char, string>? DriveMappings { get; init; }
    public string? ColorSpec { get; init; }
    public string? NativeCwd { get; init; }
}
