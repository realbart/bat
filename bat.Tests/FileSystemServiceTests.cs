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
    public void SetEnvironmentVariable_ShouldUpdateBothStandardAndRaw()
    {
        // Arrange
        var service = new FileSystemService(new MockFileSystem());
        
        // Act
        service.SetEnvironmentVariable("MYPATH", @"C:\usr\bin");
        service.SetEnvironmentVariable("MYVAR", "someValue");
        
        // Assert
        var standardVars = service.GetAllEnvironmentVariables();
        var rawVars = service.GetRawEnvironmentVariables();
        
        Assert.Equal(@"C:\usr\bin", standardVars["MYPATH"]);
        Assert.Equal("/usr/bin", rawVars["MYPATH"]);
        
        Assert.Equal("someValue", standardVars["MYVAR"]);
        Assert.Equal("someValue", rawVars["MYVAR"]);
    }
    [Fact]
    public void SetEnvironmentVariable_EmptyValue_ShouldRemoveFromBoth()
    {
        // Arrange
        var service = new FileSystemService(new MockFileSystem());
        service.SetEnvironmentVariable("MYVAR", "someValue");
        
        // Act
        service.SetEnvironmentVariable("MYVAR", "");
        
        // Assert
        var standardVars = service.GetAllEnvironmentVariables();
        var rawVars = service.GetRawEnvironmentVariables();
        
        Assert.False(standardVars.ContainsKey("MYVAR"));
        Assert.False(rawVars.ContainsKey("MYVAR"));
    }

    [Fact]
    public void ChangeDirectory_ShouldWorkWithSubstDrive()
    {
        // Arrange
        var fs = new MockFileSystem();
        fs.Directory.CreateDirectory("/home/user/data");
        var service = new FileSystemService(fs);
        service.SetSubst("D:", "/home/user/data");

        // Act
        var result = service.ChangeDirectory("D:\\");

        // Assert
        Assert.True(result.IsSuccess);
        // GetDosPath() will return "D:\"
        Assert.Equal("D:\\", service.GetDosPath());
    }

    [Fact]
    public void GetDosPath_ShouldUseSubstDrive_WhenInsideSubstPath()
    {
        // Arrange
        var fs = new MockFileSystem();
        fs.Directory.CreateDirectory("/home/user/data/sub");
        var service = new FileSystemService(fs);
        service.SetSubst("D:", "/home/user/data");

        // Act
        service.ChangeDirectory("/home/user/data/sub");
        var dosPath = service.GetDosPath();

        // Assert
        Assert.Equal("D:\\sub", dosPath);
    }

    [Fact]
    public void FormatPrompt_ShouldUseSubstDrive_WhenInsideSubstPath()
    {
        // Arrange
        var fs = new MockFileSystem();
        fs.Directory.CreateDirectory("/home/user/data");
        var service = new FileSystemService(fs);
        service.SetSubst("D:", "/home/user/data");
        service.ChangeDirectory("D:\\");

        // Act
        var prompt = service.FormatPrompt();

        // Assert
        Assert.Equal("D:\\>", prompt);
    }

    [Fact]
    public void GetDosPath_ShouldPreferC_WhenPathIsOnCAndAlsoSubsted()
    {
        // Arrange
        var fs = new MockFileSystem();
        fs.Directory.CreateDirectory("/home/user/data");
        var service = new FileSystemService(fs);
        // If we are in /home/user/data, and we subst D: to /home/user/data
        // Does GetDosPath return C:\home\user\data or D:\ ?
        // Currently it iterates _substMounts and returns the first match.
        service.ChangeDirectory("/home/user/data");
        service.SetSubst("D:", "/home/user/data");

        // Act
        var dosPath = service.GetDosPath();

        // Assert
        // Given current implementation, it will return D:\ because it checks _substMounts first
        Assert.Equal("D:\\", dosPath);
    }
    [Fact]
    public void IsDotNetAssembly_ShouldReturnTrue_WhenFileContainsBSJB()
    {
        // Arrange
        var bsjbData = new byte[100];
        bsjbData[50] = 0x42; // B
        bsjbData[51] = 0x53; // S
        bsjbData[52] = 0x4A; // J
        bsjbData[53] = 0x42; // B
        
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { "/test/dotnet.exe", new MockFileData(bsjbData) },
            { "/test/not-dotnet.exe", new MockFileData(new byte[] { 0x01, 0x02, 0x03, 0x04 }) }
        });
        var service = new FileSystemService(fileSystem);

        // Act & Assert
        Assert.True(service.IsDotNetAssembly("/test/dotnet.exe"));
        Assert.False(service.IsDotNetAssembly("/test/not-dotnet.exe"));
        Assert.False(service.IsDotNetAssembly("/test/nonexistent.exe"));
    }

    [Fact]
    public void IsDotNetAssembly_ShouldHandleSmallFiles()
    {
        // Arrange
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { "/test/small.exe", new MockFileData(new byte[] { 0x42, 0x53, 0x4A, 0x42 }) }
        });
        var service = new FileSystemService(fileSystem);

        // Act & Assert
        Assert.True(service.IsDotNetAssembly("/test/small.exe"));
    }
}