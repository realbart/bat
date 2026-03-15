using System.IO.Pipes;
using Bat.Protocol.Client;
using Bat.Protocol.Models;

var handshake = Environment.GetEnvironmentVariable("DOS_HANDSHAKE");
var pipeName = Environment.GetEnvironmentVariable("BAT_PIPE");

if (!string.IsNullOrEmpty(handshake) && !string.IsNullOrEmpty(pipeName))
{
    using var pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
    await pipeClient.ConnectAsync(1000);
    
    var client = new DosProtocolClient(pipeClient);

    // Perform handshake
    if (await client.PerformHandshakeAsync(handshake))
    {
        // Parse arguments according to Microsoft docs
        // SUBST [drive1: [drive2:]path]
        // SUBST drive1: /D

        if (args.Length == 0)
        {
            // List all substitutions
            await client.SendCommandAsync(new DosCommand("get_subst"));
            var response = await client.ReadResponseAsync();
            
            if (response?.Success == true && response.Macros != null)
            {
                if (response.Macros.Count == 0)
                {
                    // No substitutions - silent like real SUBST
                }
                else
                {
                    foreach (var subst in response.Macros.OrderBy(m => m.Key))
                    {
                        Console.WriteLine($"{subst.Key}: => {subst.Value}");
                    }
                }
            }
        }
        else if (args.Length == 2 && args[1].Equals("/D", StringComparison.OrdinalIgnoreCase))
        {
            // Delete substitution: SUBST drive: /D
            var drive = args[0].ToUpper();
            if (!drive.EndsWith(":"))
                drive += ":";

            if (drive == "C:")
            {
                Console.WriteLine("Cannot delete C: drive.");
                return;
            }

            await client.SendCommandAsync(new DosCommand("delete_subst") { Text = drive });
            var response = await client.ReadResponseAsync();

            if (response?.Success == false)
            {
                Console.WriteLine(response.Error ?? "Failed to delete substitution.");
            }
        }
        else if (args.Length >= 2)
        {
            // Create substitution: SUBST drive: path
            var drive = args[0].ToUpper();
            if (!drive.EndsWith(":"))
                drive += ":";

            if (drive == "C:")
            {
                Console.WriteLine("Cannot substitute C: drive.");
                return;
            }

            // Validate drive letter
            if (drive.Length != 2 || !char.IsLetter(drive[0]))
            {
                Console.WriteLine("Invalid drive specification.");
                return;
            }

            // Path is everything after the drive
            var path = string.Join(" ", args.Skip(1));

            await client.SendCommandAsync(new DosCommand("set_subst")
            {
                Text = drive,
                FileName = path
            });
            var response = await client.ReadResponseAsync();

            if (response?.Success == false)
            {
                Console.WriteLine(response.Error ?? "Invalid path or drive already substituted.");
            }
        }
        else
        {
            ShowHelp();
        }
    }
}
else
{
    Console.WriteLine("DOS_HANDSHAKE or BAT_PIPE not found. This application should be run from bat.");
}

void ShowHelp()
{
    Console.WriteLine("Associates a path with a drive letter.");
    Console.WriteLine();
    Console.WriteLine("SUBST [drive1: [drive2:]path]");
    Console.WriteLine("SUBST drive1: /D");
    Console.WriteLine();
    Console.WriteLine("  drive1:        Specifies a virtual drive to which you want to assign a path.");
    Console.WriteLine("  [drive2:]path  Specifies a physical drive and path you want to assign to");
    Console.WriteLine("                 a virtual drive.");
    Console.WriteLine("  /D             Deletes a substituted (virtual) drive.");
    Console.WriteLine();
    Console.WriteLine("Type SUBST with no parameters to display a list of current virtual drives.");
}
