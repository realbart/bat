using System.Runtime.InteropServices;

namespace Bat.Context;

/// <summary>
/// Resolves the owning username of a file on Unix via libc stat() + getpwuid().
/// Intentionally free of runtime OS detection: the uidOffset (position of st_uid
/// in struct stat) is supplied by the caller (ContextFactory) which is allowed to
/// inspect the platform.
///
/// Confirmed offsets:
///   Linux x86_64 : 28  (dev_t(8) + ino_t(8) + nlink_t(8) + mode_t(4))
///   Linux ARM64  : 24  (dev_t(8) + ino_t(8) + mode_t(4)  + nlink_t(4))
///   macOS        : 16  (dev_t(4) + mode_t(2) + nlink_t(2) + ino_t(8))
/// </summary>
internal static partial class UnixFileOwner
{
    private const int StatBufferSize = 256;

    public static string GetOwner(string path, int uidOffset)
    {
        try
        {
            var buf = new byte[StatBufferSize];
            if (StatNative(path, buf) != 0) return "";
            var uid = MemoryMarshal.Read<uint>(buf.AsSpan(uidOffset));
            var passwdPtr = GetPwuidNative(uid);
            if (passwdPtr == nint.Zero) return "";
            var namePtr = Marshal.ReadIntPtr(passwdPtr);  // pw_name is first field
            return Marshal.PtrToStringUTF8(namePtr) ?? "";
        }
        catch
        {
            return "";
        }
    }

    [LibraryImport("libc", EntryPoint = "stat", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int StatNative(string path, byte[] buf);

    [LibraryImport("libc", EntryPoint = "getpwuid")]
    private static partial nint GetPwuidNative(uint uid);
}
