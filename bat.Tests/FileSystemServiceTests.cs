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
        });
        
        var service = new FileSystemService(fileSystem);
        service.ChangeDirectory("C:\\home\\user");

        // Act
        var result = service.ChangeDirectory("test");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(@"C:\home\user\test", service.CurrentDirectory);
        Assert.Equal(@"C:\home\user\test", service.GetDosPath());
    }

    [Fact]
    public void ChangeDirectory_ShouldReturnFalse_WhenPathDoesNotExist()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory("/home/user");
        var service = new FileSystemService(fileSystem);
        service.ChangeDirectory("C:\\home\\user");

        // Act
        var result = service.ChangeDirectory("nonexistent");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(@"C:\home\user", service.CurrentDirectory);
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
        Assert.Equal(@"D:\test.txt", resolved);
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
        service.ChangeDirectory("D:\\sub");
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
    public void GetDosPath_ShouldPreferCurrentDrive_WhenPathIsOnCAndAlsoSubsted()
    {
        // Arrange
        var fs = new MockFileSystem();
        fs.Directory.CreateDirectory("/home/user/data");
        var service = new FileSystemService(fs);
        
        // We are on C:, and we subst D: to /home/user/data
        service.ChangeDirectory("C:\\");
        service.SetSubst("D:", "/home/user/data");

        // Act
        var dosPathOnC = service.GetDosPath("/home/user/data");
        
        service.ChangeDirectory("D:\\");
        var dosPathOnD = service.GetDosPath("/home/user/data");

        // Assert
        Assert.Equal("C:\\home\\user\\data", dosPathOnC);
        Assert.Equal("D:\\", dosPathOnD);
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
    [Fact]
    public void GetPhysicalPath_ShouldReturnLinuxPath()
    {
        // Arrange
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { "/home/user/TestDir/File.txt", new MockFileData("test content") }
        });
        var service = new FileSystemService(fileSystem);

        // Act & Assert
        // 1. C: Drive
        Assert.Equal("/home/user/TestDir/File.txt", service.GetPhysicalPath(@"C:\home\user\TestDir\File.txt"));
        
        // 2. Relative path
        service.ChangeDirectory(@"C:\home\user");
        Assert.Equal("/home/user/TestDir/File.txt", service.GetPhysicalPath(@"TestDir\File.txt"));

        // 3. Substituted drive
        service.SetSubst("D:", "/home/user/TestDir");
        Assert.Equal("/home/user/TestDir/File.txt", service.GetPhysicalPath(@"D:\File.txt"));
    }

    [Fact]
    public void GetPhysicalPath_ShouldPreserveCaseIfFileExists()
    {
        // Arrange
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { "/home/user/Project/Source.cs", new MockFileData("code") }
        });
        var service = new FileSystemService(fileSystem);

        // Act & Assert
        // Request with wrong case, should return correct case from filesystem
        Assert.Equal("/home/user/Project/Source.cs", service.GetPhysicalPath(@"C:\home\USER\project\source.CS"));
    }

    [Fact]
    public void ChangeDirectory_ShouldRemainOnSubstDrive_WhenGoingToParent()
    {
        // Arrange
        var fs = new MockFileSystem();
        fs.Directory.CreateDirectory("/home/bart/Videos");
        fs.Directory.CreateDirectory("/home/bart/snap");
        var service = new FileSystemService(fs);
        
        // 1. D: points to /home/bart
        service.SetSubst("D:", "/home/bart");
        
        // 2. Go to D:\Videos
        service.ChangeDirectory("D:\\Videos");
        Assert.Equal("D:\\Videos", service.GetCurrentDosPath());
        
        // 3. subst e: ..\  (from D:\Videos, this should be /home/bart)
        service.SetSubst("E:", "..\\"); 
        
        // 4. switch to E:
        service.ChangeDirectory("E:");
        Assert.Equal("E:\\", service.GetCurrentDosPath());
        
        // 5. cd snap (where /home/bart/snap exists)
        var resCd = service.ChangeDirectory("snap");
        Assert.True(resCd.IsSuccess, "CD snap failed: " + resCd.ErrorMessage);
        
        // Assert
        Assert.Equal("E:\\snap", service.GetCurrentDosPath());
        Assert.Equal("E", service.GetType().GetField("_currentDrive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(service));
    }

    [Fact]
    public void SubstOutput_ShouldShowDosPaths()
    {
        // Arrange
        var fs = new MockFileSystem();
        fs.Directory.CreateDirectory("/home/bart/Videos");
        var service = new FileSystemService(fs);
        service.ChangeDirectory("C:\\home\\bart");
        
        // Act
        service.SetSubst("D:", "C:\\home\\bart");
        
        // Assert
        var substs = service.GetAllSubsts();
        Assert.Equal("C:\\home\\bart", substs["D:"]);
    }
}