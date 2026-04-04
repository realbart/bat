using System.Runtime.InteropServices;
using Context;

namespace Bat.Context;

internal static class ContextFactory
{
    public static IContext CreateContext()
    {
        IContext context = OperatingSystem.IsWindows()
            ? new DosContext(new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" }))
            : new UxContextAdapter(new UxFileSystemAdapter(
                new Dictionary<char, string> { ['Z'] = "/" },
                UnixOwnerGetter()));
        return context;
    }

    private static Func<string, string> UnixOwnerGetter()
    {
        // st_uid offset inside struct stat differs by platform and architecture.
        // All detection is confined here per the architecture rule.
        //   Linux x86_64 : 28  (dev_t 8 + ino_t 8 + nlink_t 8 + mode_t 4)
        //   Linux ARM64  : 24  (dev_t 8 + ino_t 8 + mode_t 4  + nlink_t 4)
        //   macOS        : 16  (dev_t 4 + mode_t 2 + nlink_t 2 + ino_t 8)
        var uidOffset = OperatingSystem.IsLinux()
            ? (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? 24 : 28)
            : 16;
        return path => UnixFileOwner.GetOwner(path, uidOffset);
    }
}
