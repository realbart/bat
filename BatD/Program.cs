namespace BatD;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // batd runs as a windowless daemon (WinExe).
        // ConPTY creates its own pseudo-console - no AllocConsole() needed.

        using var server = new BatD.DaemonServer();
        var started = await server.ListenAsync();
        return started ? 0 : 0;
    }
}
