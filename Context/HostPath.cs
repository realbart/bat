namespace Context
{
    public readonly struct HostPath(string path)
    {
        string Path { get; } = path;
        public override string ToString() => Path;
    }
}
