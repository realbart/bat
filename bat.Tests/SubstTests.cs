using System.IO.Abstractions.TestingHelpers;
using Bat.FileSystem;
using Xunit;

namespace Bat.Tests;

public class SubstTests
{
    [Fact]
    public void Subst_ShouldWork_And_ResolvePath_ShouldUseIt()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { "/home/bart/test.txt", new MockFileData("hello") }
        }, "/home/bart");

        var service = new FileSystemService(fs);
        
        // Initial state: C: is /home/bart (assumed by FileSystemService constructor logic in this project)
        // Let's verify what C:\ resolves to.
        var resolvedC = service.ResolvePath("C:\\");
        
        // Since C: is /home/bart, C:\ resolves to /home/bart (on Linux mock fs)
        // But FileSystemService.GetLinuxPath might be translating it differently if it thinks C: is /
        
        // Ensure /subdir exists at the root, because GetLinuxPath(C:\subdir) might be /subdir
        fs.Directory.CreateDirectory("/subdir");
        fs.File.WriteAllText("/subdir/file.txt", "world");
        
        var result = service.SetSubst("D:", "C:\\subdir");
        Assert.True(result.IsSuccess, result.ErrorMessage);

        var resolvedD = service.ResolvePath("D:\\file.txt");
        // Should resolve to D:\file.txt
        
        Assert.Equal("D:\\file.txt", resolvedD);
        Assert.Equal("/subdir/file.txt", service.GetPhysicalPath(resolvedD));
    }

    [Fact]
    public void Subst_SetSubst_ShouldFail_IfPathDoesNotExist()
    {
        var fs = new MockFileSystem();
        var service = new FileSystemService(fs);

        var result = service.SetSubst("D:", "C:\\nonexistent");
        Assert.False(result.IsSuccess);
    }
}
