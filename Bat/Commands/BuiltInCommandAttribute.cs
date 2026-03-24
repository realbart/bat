namespace Bat.Commands;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
internal class BuiltInCommandAttribute(string name) : Attribute
{
    public string Name { get; } = name;

    /// <summary>Space-separated flag names (boolean, no value). E.g. "B W L S"</summary>
    public string Flags { get; init; } = "";

    /// <summary>Space-separated option names (take a value after ':' or as next word). E.g. "A O T"</summary>
    public string Options { get; init; } = "";
}
