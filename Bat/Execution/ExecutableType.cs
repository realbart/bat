namespace Bat.Execution;

/// <summary>
/// Type of executable determined from PE header inspection.
/// </summary>
internal enum ExecutableType
{
    Unknown,
    WindowsConsole,
    WindowsGui,
    DotNetAssembly
}
