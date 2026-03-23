#pragma warning disable CS8892, IDE0060
using Context;

namespace XCopy;

public static class Program
{
    public static int Main()
    {
        Console.WriteLine("This application needs Bat to run");
        return 1;
    }

    public static async Task<int> Main(IContext context, params string[] args)
    {
        Console.WriteLine("XCopy Main (through {0})", context);
        return 0;
    }
}
#pragma warning restore CS8892, IDE0060