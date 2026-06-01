namespace BatD;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // batd runs as a windowless daemon (WinExe).
        // ConPTY creates its own pseudo-console - no AllocConsole() needed.

        global::Context.IFileSystem fileSystem;
        Func<global::Context.IFileSystem, global::Context.IConsole, global::Context.IContext> contextFactory;

#if WINDOWS
        fileSystem = new BatD.Context.Dos.DosFileSystem();
        contextFactory = (fs, console) => new BatD.Context.Dos.DosContext((BatD.Context.Dos.DosFileSystem)fs, console);
#else
        fileSystem = new BatD.Context.Ux.UxFileSystemAdapter();
        contextFactory = (fs, console) => new BatD.Context.Ux.UxContextAdapter((BatD.Context.Ux.UxFileSystemAdapter)fs, console);
#endif

        using var server = new BatD.DaemonServer(fileSystem, contextFactory);
        var started = await server.ListenAsync();
        return started ? 0 : 0;
    }
}
