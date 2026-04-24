namespace Bat.Daemon;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // batd runs as a windowless daemon (WinExe).
        // No console is needed - ConPTY creates its own pseudo-console for PTY sessions.
        // Non-PTY child processes inherit no console, which is correct for a daemon.

        using var server = new DaemonServer();
        var started = await server.ListenAsync();
        return started ? 0 : 0;
    }
}
