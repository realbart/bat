#pragma warning disable CS8892, IDE0060
namespace Doskey;

using Context;

public static class Program
{
    public static int Main()
    {
        Console.WriteLine("This application needs Bat to run");
        return 1;
    }

    public static async Task<int> Main(IContext context, IArgumentSet args)
    {
        Console.WriteLine("Doskey Main (through {0})", context);
        return 0;
    }
}
#pragma warning restore CS8892, IDE0060
