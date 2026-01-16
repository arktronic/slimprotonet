using System.Net;
using System.Text;
using SlimProtoNet.Discovery;
using SlimProtoNet.Protocol;
using static SlimProtoNet.Discovery.Server;

namespace SlimProtoNet.UnitTests.Discovery;

[TestClass]
public class ServerDiscoveryTests
{
    private TestableServerDiscovery _discovery = null!;

    [TestInitialize]
    public void Setup()
    {
        _discovery = new TestableServerDiscovery();
    }

    [TestMethod]
    public void ParseDiscoveryResponseShouldReturnNullWhenBufferEmpty()
    {
        var buffer = Array.Empty<byte>();
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.100"), 3483);

        var result = _discovery.ParseDiscoveryResponsePublic(buffer, endpoint);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseDiscoveryResponseShouldReturnNullWhenFirstByteNotE()
    {
        var buffer = new byte[] { (byte)'X', 0, 0, 0 };
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.100"), 3483);

        var result = _discovery.ParseDiscoveryResponsePublic(buffer, endpoint);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseDiscoveryResponseShouldParseValidResponse()
    {
        var buffer = BuildDiscoveryResponse();
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.100"), 9000);

        var result = _discovery.ParseDiscoveryResponsePublic(buffer, endpoint);

        Assert.IsNotNull(result);
        Assert.AreEqual(IPAddress.Parse("192.168.1.100"), result.EndPoint.Address);
        Assert.AreEqual(Constants.SLIM_PORT, result.EndPoint.Port);
    }

    [TestMethod]
    public void DecodeTlvShouldParseNameField()
    {
        var buffer = BuildTlvField("NAME", "MyServer");

        var result = _discovery.DecodeTlvPublic(buffer, 0);

        Assert.IsTrue(result.ContainsKey("NAME"));
        Assert.AreEqual(ServerTlvType.Name, result["NAME"].Type);
        Assert.AreEqual("MyServer", result["NAME"].Value);
    }

    [TestMethod]
    public void DecodeTlvShouldParseVersionField()
    {
        var buffer = BuildTlvField("VERS", "8.3.1");

        var result = _discovery.DecodeTlvPublic(buffer, 0);

        Assert.IsTrue(result.ContainsKey("VERS"));
        Assert.AreEqual(ServerTlvType.Version, result["VERS"].Type);
        Assert.AreEqual("8.3.1", result["VERS"].Value);
    }

    [TestMethod]
    public void DecodeTlvShouldParseAddressField()
    {
        var buffer = BuildTlvField("IPAD", "192.168.1.50");

        var result = _discovery.DecodeTlvPublic(buffer, 0);

        Assert.IsTrue(result.ContainsKey("IPAD"));
        Assert.AreEqual(ServerTlvType.Address, result["IPAD"].Type);
        Assert.AreEqual(IPAddress.Parse("192.168.1.50"), result["IPAD"].Value);
    }

    [TestMethod]
    public void DecodeTlvShouldParsePortField()
    {
        var buffer = BuildTlvField("JSON", "9000");

        var result = _discovery.DecodeTlvPublic(buffer, 0);

        Assert.IsTrue(result.ContainsKey("JSON"));
        Assert.AreEqual(ServerTlvType.Port, result["JSON"].Type);
        Assert.AreEqual((ushort)9000, result["JSON"].Value);
    }

    [TestMethod]
    public void DecodeTlvShouldParseMultipleFields()
    {
        var buffer = CombineTlvFields(
            BuildTlvField("NAME", "TestServer"),
            BuildTlvField("VERS", "1.0.0"),
            BuildTlvField("IPAD", "10.0.0.1"),
            BuildTlvField("JSON", "8080")
        );

        var result = _discovery.DecodeTlvPublic(buffer, 0);

        Assert.HasCount(4, result);
        Assert.IsTrue(result.ContainsKey("NAME"));
        Assert.IsTrue(result.ContainsKey("VERS"));
        Assert.IsTrue(result.ContainsKey("IPAD"));
        Assert.IsTrue(result.ContainsKey("JSON"));
    }

    [TestMethod]
    public void DecodeTlvShouldSkipUnknownTokens()
    {
        var buffer = CombineTlvFields(
            BuildTlvField("NAME", "Server"),
            BuildTlvField("UNKN", "Value"),
            BuildTlvField("VERS", "1.0")
        );

        var result = _discovery.DecodeTlvPublic(buffer, 0);

        Assert.HasCount(2, result);
        Assert.IsTrue(result.ContainsKey("NAME"));
        Assert.IsTrue(result.ContainsKey("VERS"));
        Assert.IsFalse(result.ContainsKey("UNKN"));
    }

    [TestMethod]
    public void DecodeTlvShouldHandleInvalidIpAddress()
    {
        var buffer = BuildTlvField("IPAD", "not-an-ip");

        var result = _discovery.DecodeTlvPublic(buffer, 0);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void DecodeTlvShouldHandleInvalidPort()
    {
        var buffer = BuildTlvField("JSON", "not-a-port");

        var result = _discovery.DecodeTlvPublic(buffer, 0);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void DecodeTlvShouldStopAtBufferEnd()
    {
        var buffer = new byte[10];
        buffer[0] = (byte)'N';
        buffer[1] = (byte)'A';
        buffer[2] = (byte)'M';
        buffer[3] = (byte)'E';
        buffer[4] = 20; // Length exceeds buffer

        var result = _discovery.DecodeTlvPublic(buffer, 0);

        Assert.IsEmpty(result);
    }

    private static byte[] BuildDiscoveryResponse()
    {
        var response = new List<byte> { (byte)'E' };
        response.AddRange(BuildTlvField("NAME", "Server"));
        return response.ToArray();
    }

    private static byte[] BuildTlvField(string token, string value)
    {
        var buffer = new List<byte>();
        buffer.AddRange(Encoding.ASCII.GetBytes(token));
        buffer.Add((byte)value.Length);
        buffer.AddRange(Encoding.ASCII.GetBytes(value));
        return buffer.ToArray();
    }

    private static byte[] CombineTlvFields(params byte[][] fields)
    {
        var combined = new List<byte>();
        foreach (var field in fields)
        {
            combined.AddRange(field);
        }
        return combined.ToArray();
    }

    private class TestableServerDiscovery : ServerDiscovery
    {
        public Server? ParseDiscoveryResponsePublic(byte[] buffer, IPEndPoint remoteEndPoint)
        {
            return ParseDiscoveryResponse(buffer, remoteEndPoint);
        }

        public Dictionary<string, ServerTlv> DecodeTlvPublic(byte[] buffer, int offset)
        {
            return DecodeTlv(buffer, offset);
        }
    }
}
