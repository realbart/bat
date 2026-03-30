#pragma warning disable CS8892, IDE0060
using Context;

namespace Subst;

public static class Program
{
    private const string HelpText =
        """
        Associates a path with a drive letter.

        SUBST [drive1: [drive2:]path]
        SUBST drive1: /D

          drive1:        Specifies a virtual drive to which you want to assign a path.
          [drive2:]path  Specifies a physical drive and path you want to assign to
                         a virtual drive.
          /D             Deletes a substituted (virtual) drive.

        Type SUBST with no parameters to display a list of current virtual drives.
        """;

    public static int Main()
    {
        Console.WriteLine("This application needs Bat to run");
        return 1;
    }

    public static async Task<int> Main(IContext context, IArgumentSet args)
    {
        if (args.IsHelpRequest) { await Console.Out.WriteLineAsync(HelpText); return 0; }

        // TODO: implement subst list, assign and /D delete
        return 0;
    }
}
#pragma warning restore CS8892, IDE0060
