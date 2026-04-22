namespace Bat.Execution;

/// <summary>
/// Type of executable determined from file header inspection.
/// </summary>
internal enum ExecutableType
{
    /// <summary>File could not be opened or parsed (I/O error, access denied, etc.).</summary>
    Unknown,
    /// <summary>Readable file with no recognised executable header — open via ShellExecute.</summary>
    Document,
    WindowsConsole,
    WindowsGui,
    DotNetAssembly,
    /// <summary>File prefixed with the bat launcher stub (first 2 KB) followed by a .NET assembly.</summary>
    PrefixedDotNetAssembly
}
