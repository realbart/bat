namespace Bat.Daemon;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var server = new DaemonServer();
        var started = await server.ListenAsync();
        // If another healthy daemon is running, exit silently (code 0)
        return started ? 0 : 0;
    }
}
