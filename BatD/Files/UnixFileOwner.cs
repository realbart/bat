using System.Runtime.InteropServices;

namespace Bat.Context;

/// <summary>
/// Resolves the owning username of a file on Unix via libc stat() + getpwuid().
///
/// Confirmed offsets:
///   Linux x86_64 : 28  (dev_t(8) + ino_t(8) + nlink_t(8) + mode_t(4))
///   Linux ARM64  : 24  (dev_t(8) + ino_t(8) + mode_t(4)  + nlink_t(4))
///   macOS        : 16  (dev_t(4) + mode_t(2) + nlink_t(2) + ino_t(8))
/// </summary>
// todo: only compile on linux/macos
// todo: file system naming should use eiter Ux or Dos prefix
public static partial class UnixFileOwner
{
    private const int StatBufferSize = 256;

    private static readonly int UidOffset = OperatingSystem.IsLinux()
        ? (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? 24 : 28)
        : 16;

    public static string GetOwner(string path)
    {
        try
        {
            var buf = new byte[StatBufferSize];
            if (StatNative(path, buf) != 0) return "";
            var uid = MemoryMarshal.Read<uint>(buf.AsSpan(UidOffset));
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
