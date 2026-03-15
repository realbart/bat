using System.IO.Abstractions;
using System.Runtime.InteropServices;

namespace Bat.FileSystem;

public class FileSystemService
{
    private readonly IFileSystem _fileSystem;
    private string _currentDrive = "C";
    private readonly Dictionary<string, string> _driveCurrentDirs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stack<string> _directoryStack = new();
    private readonly Dictionary<string, string> _environmentVariables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _rawEnvironmentVariables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _substMounts = new(StringComparer.OrdinalIgnoreCase); // drive -> path
    private static readonly System.Text.RegularExpressions.Regex PathRegex = new(@"^/([^/]+(/|$))*$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public FileSystemService(IFileSystem? fileSystem = null)
    {
        var innerFileSystem = fileSystem ?? new global::System.IO.Abstractions.FileSystem();
        _fileSystem = new DosFileSystem(innerFileSystem, this);
        
        LoadEnvironmentVariables();
        var currentDir = System.IO.Directory.GetCurrentDirectory();
        _driveCurrentDirs["C"] = GetDosPath(currentDir);
    }

    private void LoadEnvironmentVariables()
    {
        var envVars = System.Environment.GetEnvironmentVariables();
        foreach (System.Collections.DictionaryEntry de in envVars)
        {
            var key = de.Key.ToString() ?? string.Empty;
            var value = de.Value?.ToString() ?? string.Empty;
            
            _rawEnvironmentVariables[key] = value;

            if (key.Equals("PATH", StringComparison.OrdinalIgnoreCase))
            {
                value = TranslatePathVariable(value);
            }
            else if (IsPathLike(value))
            {
                value = GetDosPath(value);
            }

            _environmentVariables[key] = value;
        }

        // Set or override mandatory DOS-specific variables
        if (!_environmentVariables.ContainsKey("PROMPT"))
        {
            _environmentVariables["PROMPT"] = "$P$G";
        }
        
        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "bat.exe";
        _environmentVariables["COMSPEC"] = GetDosPath(exePath);
    }

    private string TranslatePathVariable(string value)
    {
        var paths = value.Split(':', StringSplitOptions.RemoveEmptyEntries);
        var dosPaths = paths.Select(p => {
            // Check if it's a Linux path or already looks like a DOS path (unlikely in env var on Linux)
            if (p.StartsWith("/")) return GetDosPath(p);
            return p;
        });
        return string.Join(";", dosPaths);
    }

    private bool IsPathLike(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        if (value.StartsWith("/") || (value.Length >= 3 && value[1] == ':' && value[2] == '\\'))
        {
             // Het lijkt op een Linux of DOS pad
             return true;
        }
        return PathRegex.IsMatch(value);
    }

    public Result PushDirectory(string path)
    {
        _directoryStack.Push(_driveCurrentDirs[_currentDrive]);
        return ChangeDirectory(path);
    }

    public Result<bool> PopDirectory()
    {
        if (_directoryStack.Count > 0)
        {
            _driveCurrentDirs[_currentDrive] = _directoryStack.Pop();
            return Result.Success(true);
        }
        return Result.Success(false);
    }

    public string GetEnvironmentVariable(string name)
    {
        return _environmentVariables.TryGetValue(name, out var value) ? value : string.Empty;
    }

    public Result SetEnvironmentVariable(string name, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            _environmentVariables.Remove(name);
            _rawEnvironmentVariables.Remove(name);
        }
        else
        {
            _environmentVariables[name] = value;
            // Voor de raw variant gebruiken we de originele Linux waarde als die beschikbaar is,
            // anders de nieuwe waarde (maar dan wel vertaald naar Linux stijl indien nodig)
            if (IsPathLike(value))
            {
                _rawEnvironmentVariables[name] = GetLinuxPath(value);
            }
            else
            {
                _rawEnvironmentVariables[name] = value;
            }
        }
        return Result.Success();
    }

    public string GetLinuxPath(string dosPath)
    {
        var path = dosPath;
        if (path.StartsWith("C:", StringComparison.OrdinalIgnoreCase))
        {
            path = path.Substring(2);
        }
        return path.Replace('\\', '/');
    }

    public IDictionary<string, string> GetAllEnvironmentVariables() => _environmentVariables;
    public IDictionary<string, string> GetRawEnvironmentVariables() => _rawEnvironmentVariables;

    public string GetBatDirectory()
    {
        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        if (exePath != null)
        {
            return Path.GetDirectoryName(exePath) ?? "/";
        }
        return "/";
    }

    public IFileSystem FileSystem => _fileSystem;

    public bool EchoOn { get; set; } = true;

    public string CurrentDirectory => _driveCurrentDirs[_currentDrive];

    /// <summary>
    /// Resolves a path while preserving the case if it exists on the file system.
    /// If there's an exact match, it's used. 
    /// If there's multiple matches, it returns the exact match or an error if none match exactly.
    /// </summary>
    public IEnumerable<string> ResolvePaths(string path)
    {
        var targetPath = path.Replace('\\', '/');

        if (targetPath.StartsWith("C:", StringComparison.OrdinalIgnoreCase))
        {
            targetPath = targetPath.Substring(2);
        }

        if (string.IsNullOrEmpty(targetPath))
        {
            return new[] { "/" };
        }

        string directory;
        string pattern;

        if (targetPath.Contains('*') || targetPath.Contains('?'))
        {
            // If it's a wildcard path, we split it into the fixed part and the wildcard part
            // For simplicity, we only support wildcards in the last part of the path for now
            // e.g., /test/*.txt or *.dll
            var lastSlash = targetPath.LastIndexOf('/');
            if (lastSlash >= 0)
            {
                directory = ResolvePath(targetPath.Substring(0, lastSlash));
                pattern = targetPath.Substring(lastSlash + 1);
            }
            else
            {
                directory = CurrentDirectory;
                pattern = targetPath;
            }

            if (!_fileSystem.Directory.Exists(directory))
            {
                return Enumerable.Empty<string>();
            }

            // Case-insensitive wildcard matching for Linux
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            var regex = new System.Text.RegularExpressions.Regex(regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return _fileSystem.Directory.GetFileSystemEntries(directory)
                .Where(e => regex.IsMatch(_fileSystem.Path.GetFileName(e)));
        }

        var resolved = ResolvePath(path);
        return resolved != null ? new[] { resolved } : Enumerable.Empty<string>();
    }

    public string ResolvePath(string path)
    {
        var targetPath = path.Replace('\\', '/');

        // Check for SUBST drives first (D:, E:, etc.)
        if (targetPath.Length >= 2 && char.IsLetter(targetPath[0]) && targetPath[1] == ':')
        {
            var drive = targetPath.Substring(0, 2).ToUpper();

            if (drive != "C:" && _substMounts.TryGetValue(drive, out var substPath))
            {
                // Replace drive with substituted path
                if (targetPath.Length > 2)
                {
                    targetPath = substPath + targetPath.Substring(2);
                }
                else
                {
                    targetPath = substPath;
                }
            }
            else if (drive == "C:")
            {
                targetPath = targetPath.Substring(2);
            }
        }

        if (string.IsNullOrEmpty(targetPath))
        {
            targetPath = "/";
        }

        string absolutePath;
        if (_fileSystem.Path.IsPathRooted(targetPath))
        {
            // On Linux, GetFullPath("/") returns "/", but GetFullPath("C:/") might return something else depending on the FS implementation.
            // Since we already stripped "C:", we should treat it as rooted.
            absolutePath = _fileSystem.Path.GetFullPath(targetPath);
        }
        else
        {
            var linuxCurrent = GetLinuxPath(CurrentDirectory);
            absolutePath = _fileSystem.Path.GetFullPath(_fileSystem.Path.Combine(linuxCurrent, targetPath));
        }

        // Now we need to handle case preservation.
        // We go through the parts of the path and find the matching case.
        return FindCasePreservedPath(absolutePath) ?? absolutePath;
    }

    public string GetCaseInsensitiveMatch(string path)
    {
        // Use IFileSystem directly here to avoid recursion if DosFileSystem calls this
        // But DosFileSystem is what's passed in.
        // We need to use the RAW inner filesystem for matching.
        var fs = (_fileSystem as DosFileSystem)?.InnerFileSystem ?? _fileSystem;
        
        var dir = fs.Path.GetDirectoryName(path);
        var fileName = fs.Path.GetFileName(path);

        if (string.IsNullOrEmpty(dir)) dir = CurrentDirectory;
        else
        {
            // ResolvePath uses _fileSystem which is DosFileSystem.
            // We should be careful.
            dir = ResolvePath(dir);
        }

        if (!fs.Directory.Exists(dir)) return path;

        var entries = fs.Directory.GetFileSystemEntries(dir);
        var match = entries.FirstOrDefault(e => fs.Path.GetFileName(e).Equals(fileName, StringComparison.OrdinalIgnoreCase));

        return match ?? path;
    }

    private string? FindCasePreservedPath(string path)
    {
        if (path == "/" || string.IsNullOrEmpty(path)) return "/";

        var fs = (_fileSystem as DosFileSystem)?.InnerFileSystem ?? _fileSystem;
        var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        var current = "/";

        foreach (var part in parts)
        {
            if (!fs.Directory.Exists(current))
            {
                return null;
            }

            var entries = fs.Directory.GetFileSystemEntries(current);
            var matches = entries.Where(e => {
                var entryName = fs.Path.GetFileName(e);
                if (string.IsNullOrEmpty(entryName)) entryName = e.TrimEnd('/').Split('/').Last();
                if (entryName.Contains("/") || entryName.Contains("\\")) entryName = entryName.Replace('\\', '/').Split('/').Last();
                
                return entryName.Equals(part, StringComparison.OrdinalIgnoreCase);
            }).ToList();

            if (matches.Count == 0)
            {
                // SPECIAL CASE: MockFileSystem on Linux might return paths with / at the beginning even for GetFileSystemEntries
                // Or it might use \ as separator internally in some data structures.
                matches = entries.Where(e => {
                    var entryName = e.Replace('\\', '/').TrimEnd('/').Split('/').Last();
                    return entryName.Equals(part, StringComparison.OrdinalIgnoreCase);
                }).ToList();
                
                if (matches.Count == 0) return null;
            }

            if (matches.Count > 1)
            {
                var exactMatch = matches.FirstOrDefault(e => {
                    var entryName = fs.Path.GetFileName(e);
                    if (string.IsNullOrEmpty(entryName)) entryName = e.TrimEnd('/').Split('/').Last();
                    if (entryName.Contains("/") || entryName.Contains("\\")) entryName = entryName.Replace('\\', '/').Split('/').Last();
                    if (string.IsNullOrEmpty(entryName) && current == "/") entryName = e.TrimStart('/');
                    return entryName.Equals(part, StringComparison.Ordinal);
                });
                
                if (exactMatch != null)
                {
                    current = exactMatch;
                }
                else
                {
                    current = matches[0]; // Pick first if no exact match
                }
            }
            else
            {
                current = matches[0];
            }
        }

        return current;
    }

    public Result ChangeDirectory(string path)
    {
        var resolvedPath = ResolvePath(path);
        
        if (resolvedPath != null && _fileSystem.Directory.Exists(resolvedPath))
        {
            _driveCurrentDirs[_currentDrive] = GetDosPath(resolvedPath);
            return Result.Success();
        }

        return Result.Failure("The system cannot find the path specified.");
    }

    public Result CopyFile(string source, string destination, bool overwrite = true)
    {
        try
        {
            _fileSystem.File.Copy(source, destination, overwrite);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public Result MoveFile(string source, string destination)
    {
        try
        {
            if (_fileSystem.File.Exists(destination)) _fileSystem.File.Delete(destination);
            _fileSystem.File.Move(source, destination);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public Result MoveDirectory(string source, string destination)
    {
        try
        {
            _fileSystem.Directory.Move(source, destination);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public Result DeleteFile(string path)
    {
        try
        {
            if (_fileSystem.File.Exists(path))
            {
                _fileSystem.File.Delete(path);
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public Result CreateDirectory(string path)
    {
        try
        {
            if (_fileSystem.Directory.Exists(path))
            {
                return Result.Failure("A subdirectory or file already exists.");
            }
            _fileSystem.Directory.CreateDirectory(path);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public Result RemoveDirectory(string path, bool recursive = false)
    {
        try
        {
            _fileSystem.Directory.Delete(path, recursive);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public string GetDosPath(string? linuxPath = null)
    {
        linuxPath ??= CurrentDirectory;

        // Check if linuxPath is actually a substituted path
        foreach (var kvp in _substMounts)
        {
            if (linuxPath.StartsWith(kvp.Value, StringComparison.OrdinalIgnoreCase))
            {
                var relative = linuxPath.Substring(kvp.Value.Length).Replace('/', '\\');
                if (!relative.StartsWith("\\") && relative.Length > 0) relative = "\\" + relative;
                var result = kvp.Key + relative;
                if (result.Length == 2) result += "\\";
                return result;
            }
        }
        
        var dosPath = linuxPath.Replace('/', '\\');
        if (dosPath.StartsWith("\\"))
        {
            dosPath = "C:" + dosPath;
        }
        else if (!dosPath.StartsWith("C:", StringComparison.OrdinalIgnoreCase))
        {
             dosPath = "C:\\" + dosPath;
        }

        if (dosPath.Length == 2) dosPath += "\\";
        
        return dosPath;
    }

    public string FormatPrompt()
    {
        var prompt = GetEnvironmentVariable("PROMPT");
        if (string.IsNullOrEmpty(prompt)) prompt = "$P$G";

        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < prompt.Length; i++)
        {
            if (prompt[i] == '$' && i + 1 < prompt.Length)
            {
                var code = char.ToUpper(prompt[i + 1]);
                switch (code)
                {
                    case 'P': sb.Append(GetDosPath()); break;
                    case 'G': sb.Append('>'); break;
                    case 'L': sb.Append('<'); break;
                    case 'B': sb.Append('|'); break;
                    case 'Q': sb.Append('='); break;
                    case 'D': sb.Append(DateTime.Now.ToShortDateString()); break;
                    case 'T': sb.Append(DateTime.Now.ToShortTimeString()); break;
                    case 'V': sb.Append("0.1.0"); break;
                    case 'N': 
                        {
                            var currentDos = GetDosPath();
                            if (currentDos.Length >= 2 && currentDos[1] == ':')
                                sb.Append(currentDos.Substring(0, 2));
                            else
                                sb.Append("C:");
                        }
                        break;
                    case '_': sb.AppendLine(); break;
                    case '$': sb.Append('$'); break;
                    case 'S': sb.Append(' '); break;
                    case 'E': sb.Append('\x1b'); break;
                    default: break; // Ignore unknown
                }
                i++;
            }
            else
            {
                sb.Append(prompt[i]);
            }
        }
        return sb.ToString();
    }

    public string? FindExecutable(string commandName)
    {
        var extensions = new[] { "", ".exe", ".com", ".bat", ".cmd" };
        
        // 1. Zoek in huidige map
        var linuxCurrent = GetLinuxPath(CurrentDirectory);
        foreach (var ext in extensions)
        {
            var fullPath = _fileSystem.Path.Combine(linuxCurrent, commandName + ext);
            if (IsExecutable(fullPath))
            {
                return fullPath;
            }
        }

        // 2. Zoek in PATH
        var pathVar = GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathVar))
        {
            var paths = pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var path in paths)
            {
                // De path variabelen kunnen DOS-stijl zijn, dus we moeten ze resolven
                var resolvedPath = ResolvePath(path);
                foreach (var ext in extensions)
                {
                    var fullPath = _fileSystem.Path.Combine(resolvedPath, commandName + ext);
                    if (IsExecutable(fullPath))
                    {
                        return fullPath;
                    }
                }
            }
        }

        return null;
    }

    public bool IsDotNetAssembly(string path)
    {
        if (!_fileSystem.File.Exists(path)) return false;

        try
        {
            // We zoeken naar de BSJB magic string (0x42 0x53 0x4A 0x42)
            // die in de CLI metadata header van een .NET assembly voorkomt.
            // Voor native executables (.NET AppHost) staat dit ergens in het bestand.
            using var stream = _fileSystem.File.OpenRead(path);
            
            // Optimalisatie: we scannen niet het hele bestand als het heel groot is, 
            // maar voor de meeste .NET executables staat het relatief aan het begin of einde.
            // Een AppHost executable is meestal klein (~100-200 KB).
            var buffer = new byte[1024 * 512]; // Scan de eerste 512 KB
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            
            for (int i = 0; i < bytesRead - 4; i++)
            {
                if (buffer[i] == 0x42 && buffer[i + 1] == 0x53 && buffer[i + 2] == 0x4A && buffer[i + 3] == 0x42)
                {
                    return true;
                }
            }
        }
        catch { }

        return false;
    }

    private bool IsExecutable(string path)
    {
        if (!_fileSystem.File.Exists(path)) return false;

        // Op Linux kijken we naar executable permissies
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var fileInfo = _fileSystem.FileInfo.New(path);
                // Dit is een simpele check. In een echte Linux omgeving zouden we 
                // Mono.Posix of interop gebruiken voor nauwkeurige permissies.
                // Voor .NET 7+ kunnen we File.GetUnixFileMode gebruiken.
                var mode = _fileSystem.File.GetUnixFileMode(path);
                return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
            }
            catch
            {
                // Fallback: als het een bekende extensie heeft, beschouwen we het als executable
                var ext = _fileSystem.Path.GetExtension(path).ToLower();
                return ext == ".exe" || ext == ".com" || ext == ".bat" || ext == ".cmd";
            }
        }

        return true;
    }

    public string? GetShebang(string path)
    {
        try
        {
            using var stream = _fileSystem.File.OpenRead(path);
            using var reader = new StreamReader(stream);
            var firstLine = reader.ReadLine();
            if (firstLine != null)
            {
                var trimmed = firstLine.Trim();
                // Direct shebang: #!
                if (trimmed.StartsWith("#!"))
                {
                    return trimmed.Substring(2).Trim();
                }
                // Polyglot shebang: ::#! or ::;#! or REM#!
                if (trimmed.StartsWith("::#!") || trimmed.StartsWith("REM#!", StringComparison.OrdinalIgnoreCase))
                {
                    var index = trimmed.IndexOf("#!");
                    return trimmed.Substring(index + 2).Trim();
                }
                if (trimmed.StartsWith("::;#!") || trimmed.StartsWith("REM ;#!", StringComparison.OrdinalIgnoreCase))
                {
                    var index = trimmed.IndexOf("#!");
                    return trimmed.Substring(index + 2).Trim();
                }
            }
        }
        catch { }
        return null;
    }

    // SUBST drive management
    public Result SetSubst(string drive, string path)
    {
        // Normalize drive letter
        drive = drive.ToUpper();
        if (!drive.EndsWith(":"))
            drive += ":";

        // Validate drive
        if (drive == "C:")
            return Result.Failure("Cannot substitute C: drive.");

        if (drive.Length != 2 || !char.IsLetter(drive[0]))
            return Result.Failure("Invalid drive specification.");

        // Resolve and validate path
        var resolvedPath = ResolvePath(path);
        if (!_fileSystem.Directory.Exists(resolvedPath))
            return Result.Failure("Path not found.");

        // Check if drive is already substituted
        if (_substMounts.ContainsKey(drive))
            return Result.Failure("Drive already substituted.");

        _substMounts[drive] = resolvedPath;
        _driveCurrentDirs[drive.TrimEnd(':')] = drive;
        return Result.Success();
    }

    public Result DeleteSubst(string drive)
    {
        drive = drive.ToUpper();
        if (!drive.EndsWith(":"))
            drive += ":";

        if (drive == "C:")
            return Result.Failure("Cannot delete C: drive.");

        if (!_substMounts.ContainsKey(drive))
            return Result.Failure("Drive not substituted.");

        _substMounts.Remove(drive);

        var driveLetter = drive.TrimEnd(':');
        if (_driveCurrentDirs.ContainsKey(driveLetter))
        {
            _driveCurrentDirs.Remove(driveLetter);
            if (_currentDrive == driveLetter)
            {
                _currentDrive = "C";
            }
        }

        return Result.Success();
    }

    public Dictionary<string, string> GetAllSubsts()
    {
        return new Dictionary<string, string>(_substMounts, StringComparer.OrdinalIgnoreCase);
    }

    public string? GetSubstPath(string drive)
    {
        drive = drive.ToUpper();
        if (!drive.EndsWith(":"))
            drive += ":";

        return _substMounts.TryGetValue(drive, out var path) ? path : null;
    }

    public void SetCurrentDrive(string drive)
    {
        if (_driveCurrentDirs.ContainsKey(drive))
        {
            _currentDrive = drive;
        }
    }
}
