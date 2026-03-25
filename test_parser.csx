using Bat.Parsing;
var result = Parser.Parse(\"dir/q \\\\windows\");
Console.WriteLine($\"Head: {result.Root.Head.Raw}\");
Console.WriteLine($\"Tail count: {result.Root.Tail.Count}\");
foreach (var t in result.Root.Tail) Console.WriteLine($\"  {t.GetType().Name}: {t.Raw}\");
