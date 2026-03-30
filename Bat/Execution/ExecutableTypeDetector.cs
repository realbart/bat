namespace Bat.Execution;

/// <summary>
/// Detects executable type by inspecting file headers (cross-platform).
/// Supports:
/// - .NET assemblies (managed DLLs, apphosts, self-contained)
/// - Windows PE executables (GUI vs Console subsystem)
/// - Unix ELF binaries
/// - Shell scripts with shebang (#!)
/// </summary>
internal static class ExecutableTypeDetector
{
    private const ushort IMAGE_DOS_SIGNATURE = 0x5A4D;
    private const uint IMAGE_NT_SIGNATURE = 0x00004550;
    private const ushort IMAGE_SUBSYSTEM_WINDOWS_GUI = 2;
    private const ushort IMAGE_SUBSYSTEM_WINDOWS_CUI = 3;
    private const uint ELF_SIGNATURE = 0x464C457F;

    /// <summary>
    /// Determines executable type by reading file header.
    /// Returns Unknown if file is not a valid executable.
    /// </summary>
    public static ExecutableType GetExecutableType(string path)
    {
        if (IsDotNetAssembly(path)) return ExecutableType.DotNetAssembly;

        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);
            return DetectFromHeaders(fs, br);
        }
        catch
        {
            return ExecutableType.Unknown;
        }
    }

    private static ExecutableType DetectFromHeaders(FileStream fs, BinaryReader br)
    {
        if (br.BaseStream.Length < 4) return ExecutableType.Document;

        var firstBytes = br.ReadUInt16();
        if (firstBytes == 0x2123) return ExecutableType.WindowsConsole;
        if (br.BaseStream.Length < 64) return ExecutableType.Document;

        fs.Seek(0, SeekOrigin.Begin);
        var signature = br.ReadUInt32();

        if (signature == ELF_SIGNATURE) return ExecutableType.WindowsConsole;
        if ((signature & 0xFFFF) != IMAGE_DOS_SIGNATURE) return ExecutableType.Document;

        return DetectPeSubsystem(fs, br);
    }

    private static ExecutableType DetectPeSubsystem(FileStream fs, BinaryReader br)
    {
        fs.Seek(0x3C, SeekOrigin.Begin);
        var peHeaderOffset = br.ReadInt32();
        if (peHeaderOffset < 0 || peHeaderOffset > fs.Length - 4) return ExecutableType.Unknown;

        fs.Seek(peHeaderOffset, SeekOrigin.Begin);
        if (br.ReadUInt32() != IMAGE_NT_SIGNATURE) return ExecutableType.Unknown;

        fs.Seek(peHeaderOffset + 4 + 20 + 68, SeekOrigin.Begin);
        return br.ReadUInt16() switch
        {
            IMAGE_SUBSYSTEM_WINDOWS_GUI => ExecutableType.WindowsGui,
            IMAGE_SUBSYSTEM_WINDOWS_CUI => ExecutableType.WindowsConsole,
            _ => ExecutableType.Unknown
        };
    }

    private static bool IsDotNetAssembly(string path)
    {
        var dllPath = Path.ChangeExtension(path, ".dll");
        return (File.Exists(dllPath) && TryGetAssemblyName(dllPath)) || TryGetAssemblyName(path);
    }

    private static bool TryGetAssemblyName(string path)
    {
        try { return System.Reflection.AssemblyName.GetAssemblyName(path) != null; }
        catch { return false; }
    }
}
