using System;
using System.Runtime.InteropServices;

public partial class LibcTest
{
    [DllImport("libc", EntryPoint = "Close", SetLastError = true)]
    public static extern int close(int fd);

    [DllImport("libc", EntryPoint = "posix_openpt", SetLastError = true)]
    public static extern int posix_openpt(int flags);
}

class Program
{
    static void Main()
    {
        Console.WriteLine("Testing posix_openpt...");
        try {
            int fd = LibcTest.posix_openpt(2); // O_RDWR
            if (fd < 0) {
                Console.WriteLine($"posix_openpt failed: {Marshal.GetLastPInvokeError()}");
            } else {
                Console.WriteLine($"posix_openpt succeeded, fd: {fd}");
                Console.WriteLine("Testing close...");
                int res = LibcTest.close(fd);
                if (res != 0) {
                    Console.WriteLine($"close failed: {Marshal.GetLastPInvokeError()}");
                } else {
                    Console.WriteLine("close succeeded");
                }
            }
        } catch (Exception ex) {
            Console.WriteLine($"Exception: {ex.Message}");
            if (ex.InnerException != null) {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
        }
    }
}
