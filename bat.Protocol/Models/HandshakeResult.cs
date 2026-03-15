namespace Bat.Protocol.Models;

public class HandshakeResult
{
    public bool Success { get; set; }
    public byte[]? ConsumedBytes { get; set; }
}
