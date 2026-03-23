namespace Bat.Commands;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
internal class BuiltInCommandAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
