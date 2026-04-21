using System.Text.Json;
using Bat.Shared.Ipc;

namespace Bat.UnitTests;

[TestClass]
public class IpcProtocolTests
{
    [TestMethod]
    [Timeout(4000)]
    public async Task Request_RoundTrip_PreservesAllFields()
    {
        var request = new IpcRequest
        {
            Id = 42,
            Type = IpcOperations.RegisterSession,
            Payload = IpcProtocol.ToPayload(new { SessionId = "abc-123", DriveMappings = new Dictionary<char, string> { ['C'] = "/" } })
        };

        using var stream = new MemoryStream();
        await IpcProtocol.WriteRequestAsync(stream, request);
        stream.Position = 0;

        var result = await IpcProtocol.ReadRequestAsync(stream);

        Assert.IsNotNull(result);
        Assert.AreEqual(42, result.Id);
        Assert.AreEqual(IpcOperations.RegisterSession, result.Type);
        Assert.IsNotNull(result.Payload);
    }

    [TestMethod]
    [Timeout(4000)]
    public async Task Response_RoundTrip_Success()
    {
        var response = new IpcResponse
        {
            Id = 7,
            Success = true,
            Payload = IpcProtocol.ToPayload(true)
        };

        using var stream = new MemoryStream();
        await IpcProtocol.WriteResponseAsync(stream, response);
        stream.Position = 0;

        var result = await IpcProtocol.ReadResponseAsync(stream);

        Assert.IsNotNull(result);
        Assert.AreEqual(7, result.Id);
        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Payload);
        Assert.IsTrue(result.Payload.Value.GetBoolean());
    }

    [TestMethod]
    [Timeout(4000)]
    public async Task Response_RoundTrip_Error()
    {
        var response = new IpcResponse
        {
            Id = 3,
            Success = false,
            Error = "File not found"
        };

        using var stream = new MemoryStream();
        await IpcProtocol.WriteResponseAsync(stream, response);
        stream.Position = 0;

        var result = await IpcProtocol.ReadResponseAsync(stream);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Id);
        Assert.IsFalse(result.Success);
        Assert.AreEqual("File not found", result.Error);
        Assert.IsNull(result.Payload);
    }

    [TestMethod]
    [Timeout(4000)]
    public async Task ReadRequest_OnEmptyStream_ReturnsNull()
    {
        using var stream = new MemoryStream();
        var result = await IpcProtocol.ReadRequestAsync(stream);
        Assert.IsNull(result);
    }

    [TestMethod]
    [Timeout(4000)]
    public async Task MultipleMessages_RoundTrip()
    {
        using var stream = new MemoryStream();

        var req1 = new IpcRequest { Id = 1, Type = IpcOperations.Ping };
        var req2 = new IpcRequest { Id = 2, Type = IpcOperations.GetSubsts };
        var req3 = new IpcRequest { Id = 3, Type = IpcOperations.MergeSubsts, Payload = IpcProtocol.ToPayload(new Dictionary<char, string> { ['D'] = "/data" }) };

        await IpcProtocol.WriteRequestAsync(stream, req1);
        await IpcProtocol.WriteRequestAsync(stream, req2);
        await IpcProtocol.WriteRequestAsync(stream, req3);
        stream.Position = 0;

        var r1 = await IpcProtocol.ReadRequestAsync(stream);
        var r2 = await IpcProtocol.ReadRequestAsync(stream);
        var r3 = await IpcProtocol.ReadRequestAsync(stream);
        var r4 = await IpcProtocol.ReadRequestAsync(stream);

        Assert.IsNotNull(r1);
        Assert.AreEqual(1, r1.Id);
        Assert.AreEqual(IpcOperations.Ping, r1.Type);

        Assert.IsNotNull(r2);
        Assert.AreEqual(2, r2.Id);
        Assert.AreEqual(IpcOperations.GetSubsts, r2.Type);

        Assert.IsNotNull(r3);
        Assert.AreEqual(3, r3.Id);
        Assert.AreEqual(IpcOperations.MergeSubsts, r3.Type);

        Assert.IsNull(r4, "Should return null after all messages consumed");
    }

    [TestMethod]
    [Timeout(4000)]
    public async Task Payload_ComplexObject_RoundTrip()
    {
        var payload = new { Drive = 'Z', NativePath = "/mnt/data" };
        var request = new IpcRequest
        {
            Id = 10,
            Type = IpcOperations.AddSubst,
            Payload = IpcProtocol.ToPayload(payload)
        };

        using var stream = new MemoryStream();
        await IpcProtocol.WriteRequestAsync(stream, request);
        stream.Position = 0;

        var result = await IpcProtocol.ReadRequestAsync(stream);
        Assert.IsNotNull(result?.Payload);

        var drive = result.Payload.Value.GetProperty("Drive").GetString();
        var nativePath = result.Payload.Value.GetProperty("NativePath").GetString();

        Assert.AreEqual("Z", drive);
        Assert.AreEqual("/mnt/data", nativePath);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ToPayload_FromPayload_RoundTrip()
    {
        var original = new Dictionary<string, string> { ["PATH"] = "/usr/bin", ["HOME"] = "/home/test" };
        var element = IpcProtocol.ToPayload(original);
        var restored = IpcProtocol.FromPayload<Dictionary<string, string>>(element);

        Assert.IsNotNull(restored);
        Assert.AreEqual("/usr/bin", restored["PATH"]);
        Assert.AreEqual("/home/test", restored["HOME"]);
    }

    [TestMethod]
    public void GetPipeName_ContainsUsername()
    {
        var pipeName = IpcProtocol.GetPipeName();
        Assert.IsTrue(pipeName.StartsWith("batd-"));
        Assert.IsTrue(pipeName.Length > 5);
    }

    [TestMethod]
    [Timeout(4000)]
    public async Task Request_WithNullPayload_RoundTrip()
    {
        var request = new IpcRequest { Id = 1, Type = IpcOperations.Ping };

        using var stream = new MemoryStream();
        await IpcProtocol.WriteRequestAsync(stream, request);
        stream.Position = 0;

        var result = await IpcProtocol.ReadRequestAsync(stream);
        Assert.IsNotNull(result);
        Assert.AreEqual(IpcOperations.Ping, result.Type);
        Assert.IsNull(result.Payload);
    }
}
