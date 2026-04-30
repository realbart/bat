namespace Bat.Commands;

// todo: make sure multu-char flags and arguments are also supported, 
// e.g. START /B /WAIT /D:dir "My Program.exe" arg1 arg2
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
internal class BuiltInCommandAttribute(string name) : Attribute
{
    public string Name { get; } = name;

    /// <summary>Space-separated flag names (boolean, no value). E.g. "B W L S"</summary>
    public string Flags { get; init; } = "";

    /// <summary>Space-separated option names (take a value after ':' or as next word). E.g. "A O T"</summary>
    public string Options { get; init; } = "";
}
