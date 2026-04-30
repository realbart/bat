namespace Context
{
    public readonly struct HostPath(string path)
    {
        public string Path { get; } = path;
        public override string ToString() => Path;
    }
}
