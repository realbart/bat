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
            { "/home/bart/test.txt", new MockFileData("hello") },
            { "/home/bart/subdir/file.txt", new MockFileData("world") }
        });

        var service = new FileSystemService(fs);
        
        // Initial state: C: is /home/bart (assumed by FileSystemService constructor logic in this project)
        // Let's verify what C:\ resolves to.
        var resolvedC = service.ResolvePath("C:\\");
        // In FileSystemService constructor: _driveCurrentDirs["C"] = GetDosPath(currentDir);
        // If currentDir is /, then C: is /. 
        // MockFileSystem current dir is usually / if not specified.
        
        var result = service.SetSubst("D:", "C:\\subdir");
        Assert.True(result.IsSuccess);

        var resolvedD = service.ResolvePath("D:\\file.txt");
        // Should resolve to /home/bart/subdir/file.txt (assuming C: is /home/bart)
        
        Assert.Contains("subdir/file.txt", resolvedD.Replace('\\', '/'));
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
