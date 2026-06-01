using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BatD;

public static partial class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--die-with-parent" && int.TryParse(args[1], out var ppid))
        {
            SetupDieWithParent(ppid);
        }

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

    private static void SetupDieWithParent(int ppid)
    {
#if WINDOWS
        try
        {
            var parent = Process.GetProcessById(ppid);
            parent.EnableRaisingEvents = true;
            parent.Exited += (s, e) => Environment.Exit(0);
            if (parent.HasExited) Environment.Exit(0);
        }
        catch { Environment.Exit(0); }
#else
        UnixDieWithParent();
#endif
    }

#if !WINDOWS
    [LibraryImport("libc", EntryPoint = "prctl", SetLastError = true)]
    private static partial int prctl(int option, nint arg2, nint arg3, nint arg4, nint arg5);

    private static void UnixDieWithParent()
    {
        const int PR_SET_PDEATHSIG = 1;
        const int SIGTERM = 15;
        _ = prctl(PR_SET_PDEATHSIG, SIGTERM, 0, 0, 0);
    }
#endif
}
