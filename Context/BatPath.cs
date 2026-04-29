namespace Context
{
    using System.Text;

    public readonly struct BatPath(char drive, string[] segments)
    {
        public char Drive { get; } = drive;
        public string[] Segments { get; } = segments;

        public static BatPath Parse(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length < 2 || path[1] != ':')
                throw new ArgumentException($"Invalid path format: {path}");
            var drive = char.ToUpper(path[0]);
            var segments = path.Substring(2).Split(['\\'], StringSplitOptions.RemoveEmptyEntries);
            return new BatPath(drive, segments);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Drive);
            sb.Append(':');
            foreach (var segment in Segments)
            {
                sb.Append('\\');
                sb.Append(segment);
            }
            return sb.ToString();
        }
    }
}
