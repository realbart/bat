namespace Context;

/// <summary>
/// Declares the flags and options a .NET library command accepts,
/// so the host can parse arguments correctly before invoking Main.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CommandAttribute : Attribute
{
    /// <summary>Space-separated uppercase flag names (boolean switches).</summary>
    public string Flags { get; set; } = "";

    /// <summary>Space-separated uppercase option names (switches that carry a value).</summary>
    public string Options { get; set; } = "";
}
