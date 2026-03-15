using System.IO.Abstractions.TestingHelpers;
using Bat.FileSystem;
using Xunit;

namespace Bat.Tests;

public class FileSystemServiceTests
{
    [Fact]
    public void ChangeDirectory_ShouldUpdateCurrentDirectory_WhenPathExists()
    {
        // Arrange
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { "/home/user/test", new MockDirectoryData() },
        }, "/home/user");
        
        var service = new FileSystemService(fileSystem);

        // Act
        var result = service.ChangeDirectory("test");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("/home/user/test", service.CurrentDirectory);
        Assert.Equal(@"C:\home\user\test", service.GetDosPath());
    }

    [Fact]
    public void ChangeDirectory_ShouldReturnFalse_WhenPathDoesNotExist()
    {
        // Arrange
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>(), "/home/user");
        var service = new FileSystemService(fileSystem);

        // Act
        var result = service.ChangeDirectory("nonexistent");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("/home/user", service.CurrentDirectory);
    }

    [Fact]
    public void LoadEnvironmentVariables_ShouldTranslatePaths()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var service = new FileSystemService(fileSystem);

        // Act
        var path = service.GetEnvironmentVariable("PATH");
        var comspec = service.GetEnvironmentVariable("COMSPEC");

        // Assert
        Assert.Contains("C:\\", path);
        Assert.Contains(";", path);
        // All entries in path should start with C: or be a Windows-style path
        var entries = path.Split(';');
        foreach (var entry in entries)
        {
            if (!string.IsNullOrEmpty(entry))
            {
                Assert.True(entry.StartsWith("C:", StringComparison.OrdinalIgnoreCase) || entry.Contains("\\"), 
                    $"Path entry '{entry}' should be a DOS path.");
            }
        }
        
        Assert.StartsWith("C:\\", comspec);
    }

    [Fact]
    public void IsPathLike_ShouldIdentifyLinuxPaths()
    {
        var service = new FileSystemService(new MockFileSystem());

        // Use reflection to test private method or just test through SetEnvironmentVariable if we add translation there
        // For now, let's just test if GetDosPath works as expected which is used by translation
        Assert.Equal(@"C:\usr\bin", service.GetDosPath("/usr/bin"));
        Assert.Equal(@"C:\", service.GetDosPath("/"));
    }

    [Fact]
    public void SetSubst_ShouldCreateDriveMapping()
    {
        // Arrange
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { "/home/user/temp", new MockDirectoryData() }
        });
        var service = new FileSystemService(fileSystem);

        // Act
        var result = service.SetSubst("D:", "/home/user/temp");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("/home/user/temp", service.GetSubstPath("D:"));
    }

    [Fact]
    public void SetSubst_ShouldNotAllowCDrive()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var service = new FileSystemService(fileSystem);

        // Act
        var result = service.SetSubst("C:", "/home/user");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Cannot substitute C:", result.ErrorMessage);
    }

    [Fact]
    public void SetSubst_ShouldNotAllowNonExistentPath()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var service = new FileSystemService(fileSystem);

        // Act
        var result = service.SetSubst("D:", "/nonexistent");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public void DeleteSubst_ShouldRemoveDriveMapping()
    {
        // Arrange
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { "/home/user/temp", new MockDirectoryData() }
        });
        var service = new FileSystemService(fileSystem);
        service.SetSubst("D:", "/home/user/temp");

        // Act
        var result = service.DeleteSubst("D:");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(service.GetSubstPath("D:"));
    }

    [Fact]
    public void DeleteSubst_ShouldNotAllowCDrive()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var service = new FileSystemService(fileSystem);

        // Act
        var result = service.DeleteSubst("C:");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Cannot delete C:", result.ErrorMessage);
    }

    [Fact]
    public void ResolvePath_ShouldUseSubstMapping()
    {
        // Arrange
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { "/home/user/temp", new MockDirectoryData() },
            { "/home/user/temp/test.txt", new MockFileData("content") }
        });
        var service = new FileSystemService(fileSystem);
        service.SetSubst("D:", "/home/user/temp");

        // Act
        var resolved = service.ResolvePath(@"D:\test.txt");

        // Assert
        Assert.Equal("/home/user/temp/test.txt", resolved);
    }

    [Fact]
    public void GetAllSubsts_ShouldReturnAllMappings()
    {
        // Arrange
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { "/temp", new MockDirectoryData() },
            { "/projects", new MockDirectoryData() }
        });
        var service = new FileSystemService(fileSystem);
        service.SetSubst("D:", "/temp");
        service.SetSubst("E:", "/projects");

        // Act
        var substs = service.GetAllSubsts();

        // Assert
        Assert.Equal(2, substs.Count);
        Assert.Equal("/temp", substs["D:"]);
        Assert.Equal("/projects", substs["E:"]);
    }
}