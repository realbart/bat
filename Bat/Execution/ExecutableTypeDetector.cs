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
        if (IsDotNetAssembly(path))
            return ExecutableType.DotNetAssembly;

        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            if (br.BaseStream.Length < 4)
                return ExecutableType.Unknown;

            var firstBytes = br.ReadUInt16();

            if (firstBytes == 0x2123)
                return ExecutableType.WindowsConsole;

            if (br.BaseStream.Length < 64)
                return ExecutableType.Unknown;

            fs.Seek(0, SeekOrigin.Begin);
            var signature = br.ReadUInt32();

            if (signature == ELF_SIGNATURE)
                return ExecutableType.WindowsConsole;

            if ((signature & 0xFFFF) != IMAGE_DOS_SIGNATURE)
                return ExecutableType.Unknown;

            fs.Seek(0x3C, SeekOrigin.Begin);
            var peHeaderOffset = br.ReadInt32();

            if (peHeaderOffset < 0 || peHeaderOffset > fs.Length - 4)
                return ExecutableType.Unknown;

            fs.Seek(peHeaderOffset, SeekOrigin.Begin);
            var peSignature = br.ReadUInt32();
            if (peSignature != IMAGE_NT_SIGNATURE)
                return ExecutableType.Unknown;

            fs.Seek(peHeaderOffset + 4 + 20, SeekOrigin.Begin);
            var magic = br.ReadUInt16();

            var subsystemOffset = peHeaderOffset + 4 + 20 + 68;
            fs.Seek(subsystemOffset, SeekOrigin.Begin);
            var subsystem = br.ReadUInt16();

            return subsystem switch
            {
                IMAGE_SUBSYSTEM_WINDOWS_GUI => ExecutableType.WindowsGui,
                IMAGE_SUBSYSTEM_WINDOWS_CUI => ExecutableType.WindowsConsole,
                _ => ExecutableType.Unknown
            };
        }
        catch
        {
            return ExecutableType.Unknown;
        }
    }

    private static bool IsDotNetAssembly(string path)
    {
        var dllPath = Path.ChangeExtension(path, ".dll");
        if (File.Exists(dllPath) && TryGetAssemblyName(dllPath))
            return true;

        return TryGetAssemblyName(path);
    }

    private static bool TryGetAssemblyName(string path)
    {
        try
        {
            var assemblyName = System.Reflection.AssemblyName.GetAssemblyName(path);
            return assemblyName != null;
        }
        catch
        {
            return false;
        }
    }
}
