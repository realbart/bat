using System;
using System.IO;
using System.Runtime.InteropServices;

class Program
{
    static void Main()
    {
        string testDir = Path.Combine(Directory.GetCurrentDirectory(), "test_loop");
        if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        Directory.CreateDirectory(testDir);
        
        string subDir = Path.Combine(testDir, "subdir");
        Directory.CreateDirectory(subDir);
        
        string linkPath = Path.Combine(subDir, "link_to_parent");
        
        Console.WriteLine($"Creating symlink from {linkPath} to {testDir}");
        
        try {
            File.CreateSymbolicLink(linkPath, testDir);
        } catch (Exception ex) {
            Console.WriteLine($"Failed to create symlink: {ex.Message}");
        }

        if (!File.Exists(linkPath) && !Directory.Exists(linkPath))
        {
             Console.WriteLine("Symlink not created via C#, trying shell...");
             var process = System.Diagnostics.Process.Start("ln", $"-s \"{testDir}\" \"{linkPath}\"");
             process?.WaitForExit();
        }

        var attributes = File.GetAttributes(linkPath);
        Console.WriteLine($"Attributes of {linkPath}: {attributes}");
        Console.WriteLine($"Has ReparsePoint flag: {(attributes & FileAttributes.ReparsePoint) != 0}");
        
        var linfo = new FileInfo(linkPath);
        Console.WriteLine($"FileInfo Attributes: {linfo.Attributes}");
        Console.WriteLine($"FileInfo Has ReparsePoint flag: {(linfo.Attributes & FileAttributes.ReparsePoint) != 0}");

        // Test Directory.EnumerateFileSystemEntries
        foreach (var entry in Directory.EnumerateFileSystemEntries(subDir))
        {
            var entryInfo = new FileInfo(entry);
            Console.WriteLine($"Entry: {entry}, Attributes: {entryInfo.Attributes}, IsReparse: {(entryInfo.Attributes & FileAttributes.ReparsePoint) != 0}");
        }

        Directory.Delete(testDir, true);
    }
}
