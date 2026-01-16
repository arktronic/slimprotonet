using System.Net;
using NSubstitute;
using SlimProtoNet.Client;
using SlimProtoNet.Protocol;
using SlimProtoNet.Protocol.Messages;
using SlimProtoNet.Wrappers;

namespace SlimProtoNet.UnitTests.Client;

[TestClass]
public class SlimClientTests
{
    private TcpClientFactory _tcpClientFactory = null!;
    private TcpClientWrapper _tcpClientWrapper = null!;
    private SlimCodec _mockCodec = null!;
    private MemoryStream _mockStream = null!;

    [TestInitialize]
    public void Setup()
    {
        _tcpClientFactory = Substitute.For<TcpClientFactory>();
        _tcpClientWrapper = Substitute.For<TcpClientWrapper>();
        _mockCodec = Substitute.For<SlimCodec>();
        _mockStream = new MemoryStream();

        _tcpClientFactory.CreateTcpClient().Returns(_tcpClientWrapper);
        _tcpClientWrapper.GetStream().Returns(_mockStream);
        _tcpClientWrapper.Connected.Returns(true);
        _tcpClientWrapper.ConnectAsync(Arg.Any<IPAddress>(), Arg.Any<int>()).Returns(Task.CompletedTask);
    }

    [TestCleanup]
    public void Teardown()
    {
        _mockStream?.Dispose();
        _mockStream = null!;
        _tcpClientFactory = null!;
        _tcpClientWrapper?.Dispose();
        _tcpClientWrapper = null!;
        _mockCodec = null!;
    }

    [TestMethod]
    public async Task ConnectAsyncShouldSendHeloMessage()
    {
        // Arrange
        var macAddress = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF };
        _mockCodec.Encode(Arg.Any<HeloMessage>()).Returns(new byte[] { 0x01, 0x02, 0x03 });

        var client = new SlimClient(_mockCodec, _tcpClientFactory);
        var capabilities = new Capabilities();
        var endpoint = new IPEndPoint(IPAddress.Loopback, 3483);

        // Act
        await client.ConnectAsync(endpoint, capabilities, macAddress);

        // Assert
        _mockCodec.Received(1).Encode(Arg.Is<HeloMessage>(h =>
            h.MacAddress[0] == 0xAA &&
            h.MacAddress[5] == 0xFF &&
            h.Capabilities == capabilities
        ));

        await _tcpClientWrapper.Received(1).ConnectAsync(endpoint.Address, endpoint.Port);
    }

    [TestMethod]
    public async Task SendAsyncShouldEncodeAndWriteMessage()
    {
        // Arrange
        var expectedBytes = new byte[] { 0x10, 0x20, 0x30 };
        _mockCodec.Encode(Arg.Any<HeloMessage>()).Returns(new byte[] { 0x01 });
        _mockCodec.Encode(Arg.Any<ByeMessage>()).Returns(expectedBytes);

        var client = new SlimClient(_mockCodec, _tcpClientFactory);
        var capabilities = new Capabilities();
        var macAddress = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };

        await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 3483), capabilities, macAddress);

        // Reset stream position after HELO
        _mockStream.Position = 0;
        _mockStream.SetLength(0);

        // Act
        var byeMessage = new ByeMessage { DisconnectReason = 1 };
        await client.SendAsync(byeMessage);

        // Assert
        _mockCodec.Received(1).Encode(byeMessage);

        // Verify raw bytes were written (client messages are not framed)
        _mockStream.Position = 0;
        var writtenBytes = _mockStream.ToArray();
        CollectionAssert.AreEqual(expectedBytes, writtenBytes);
    }

    [TestMethod]
    public async Task ReceiveAsyncShouldDecodeIncomingMessage()
    {
        // Arrange
        _mockCodec.Encode(Arg.Any<HeloMessage>()).Returns(new byte[] { 0x01 });

        var expectedMessage = new ServMessage { IpAddress = IPAddress.Parse("192.168.1.1") };
        _mockCodec.Decode(Arg.Any<byte[]>()).Returns(expectedMessage);

        var client = new SlimClient(_mockCodec, _tcpClientFactory);
        var macAddress = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };

        await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 3483), new Capabilities(), macAddress);

        // Write a test frame to the stream
        var testPayload = new byte[] { 0x11, 0x22, 0x33 };
        _mockStream.Position = 0;
        _mockStream.SetLength(0);
        var lengthPrefix = new byte[] { 0x00, (byte)testPayload.Length };
        await _mockStream.WriteAsync(lengthPrefix, 0, 2);
        await _mockStream.WriteAsync(testPayload, 0, testPayload.Length);
        _mockStream.Position = 0;

        // Act
        var result = await client.ReceiveAsync();

        // Assert
        Assert.AreEqual(expectedMessage, result);
        _mockCodec.Received(1).Decode(Arg.Is<byte[]>(b =>
            b.Length == testPayload.Length &&
            b[0] == 0x11 &&
            b[1] == 0x22 &&
            b[2] == 0x33
        ));
    }

    [TestMethod]
    public async Task DisconnectAsyncShouldSendByeMessage()
    {
        // Arrange
        _mockCodec.Encode(Arg.Any<ClientMessage>()).Returns(new byte[] { 0x01 });

        var client = new SlimClient(_mockCodec, _tcpClientFactory);
        var macAddress = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };

        await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 3483), new Capabilities(), macAddress);

        // Act
        await client.DisconnectAsync(2);

        // Assert
        _mockCodec.Received(1).Encode(Arg.Is<ByeMessage>(b => b.DisconnectReason == 2));
    }

    [TestMethod]
    public async Task SendAsyncShouldThrowWhenNotConnected()
    {
        // Arrange
        var client = new SlimClient(_mockCodec, _tcpClientFactory);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await client.SendAsync(new ByeMessage());
        });
    }

    [TestMethod]
    public async Task ReceiveAsyncShouldThrowWhenNotConnected()
    {
        // Arrange
        var client = new SlimClient(_mockCodec, _tcpClientFactory);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await client.ReceiveAsync();
        });
    }

    [TestMethod]
    public async Task ConnectAsyncShouldUseFallbackMacWhenNullProvided()
    {
        // Arrange
        _mockCodec.Encode(Arg.Any<HeloMessage>()).Returns(new byte[] { 0x01 });

        var client = new SlimClient(_mockCodec, _tcpClientFactory);

        // Act - null MAC triggers fallback
        await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 3483), new Capabilities(), null);

        // Assert - fallback is 01:02:03:04:05:06
        _mockCodec.Received(1).Encode(Arg.Is<HeloMessage>(h =>
            h.MacAddress[0] == 0x01 &&
            h.MacAddress[5] == 0x06
        ));
    }

    [TestMethod]
    public async Task IsConnectedShouldReturnTrueWhenConnected()
    {
        // Arrange
        _mockCodec.Encode(Arg.Any<HeloMessage>()).Returns(new byte[] { 0x01 });

        var client = new SlimClient(_mockCodec, _tcpClientFactory);

        // Act & Assert - before connection
        Assert.IsFalse(client.IsConnected);

        // Connect
        await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 3483), new Capabilities());

        // After connection
        Assert.IsTrue(client.IsConnected);
    }

    [TestMethod]
    public async Task DisconnectAsyncShouldAllowReconnection()
    {
        // Arrange
        _mockCodec.Encode(Arg.Any<ClientMessage>()).Returns(new byte[] { 0x01 });

        // Create a new stream for reconnection
        var stream1 = new MemoryStream();
        var stream2 = new MemoryStream();
        _tcpClientWrapper.GetStream().Returns(stream1, stream2);

        var client = new SlimClient(_mockCodec, _tcpClientFactory);
        var macAddress = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };
        var endpoint = new IPEndPoint(IPAddress.Loopback, 3483);

        // Act - Connect, disconnect, reconnect
        await client.ConnectAsync(endpoint, new Capabilities(), macAddress);
        await client.DisconnectAsync();

        _mockCodec.ClearReceivedCalls();

        await client.ConnectAsync(endpoint, new Capabilities(), macAddress);

        // Assert - should be able to send HELO again after disconnect
        _mockCodec.Received(1).Encode(Arg.Any<HeloMessage>());
    }

    [TestMethod]
    public async Task ConnectAsyncShouldCleanupPreviousConnection()
    {
        // Arrange
        _mockCodec.Encode(Arg.Any<ClientMessage>()).Returns(new byte[] { 0x01 });

        // Create two separate streams for the two connections
        var stream1 = new MemoryStream();
        var stream2 = new MemoryStream();
        _tcpClientWrapper.GetStream().Returns(stream1, stream2);

        var client = new SlimClient(_mockCodec, _tcpClientFactory);
        var endpoint1 = new IPEndPoint(IPAddress.Loopback, 3483);
        var endpoint2 = new IPEndPoint(IPAddress.Parse("192.168.1.100"), 3483);

        // Act - Connect twice without explicit disconnect
        await client.ConnectAsync(endpoint1, new Capabilities());

        _mockCodec.ClearReceivedCalls();

        await client.ConnectAsync(endpoint2, new Capabilities());

        // Assert - should send HELO for second connection
        _mockCodec.Received(1).Encode(Arg.Any<HeloMessage>());

        // Verify old connection cleaned up and new one established
        await _tcpClientWrapper.Received(2).ConnectAsync(Arg.Any<IPAddress>(), 3483);
    }
}
