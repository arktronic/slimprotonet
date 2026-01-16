using SlimProtoNet.Client;
using SlimProtoNet.Protocol;
using SlimProtoNet.Protocol.Messages;
using System.Net;

namespace SlimProtoNet.UnitTests.Protocol;

[TestClass]
public class SlimCodecTests
{
    private SlimCodec _codec = null!;

    [TestInitialize]
    public void Setup()
    {
        _codec = new SlimCodec();
    }

    #region HELO Message Tests

    [TestMethod]
    public void EncodeShouldProduceCorrectBytesWhenHeloMessage()
    {
        var helo = new HeloMessage
        {
            DeviceId = 0,
            Revision = 1,
            MacAddress = [1, 2, 3, 4, 5, 6],
            Uuid = [7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7],
            WlanChannelList = 0x89AB,
            BytesReceived = 1234,
            Language = ['u', 'k'],
            Capabilities = new Capabilities(false)
        };
        helo.Capabilities.Add(Capability.Wmal);

        var result = _codec.Encode(helo);

        // Expected: HELO opcode + 4-byte length + 40 bytes payload
        byte[] expected = new byte[]
        {
            // Opcode
            (byte)'H', (byte)'E', (byte)'L', (byte)'O',
            // Payload length (big-endian 40)
            0, 0, 0, 40,
            // DeviceId, Revision
            0, 1,
            // MAC address
            1, 2, 3, 4, 5, 6,
            // UUID
            7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
            // WlanChannelList (big-endian 0x89AB)
            0x89, 0xAB,
            // BytesReceived (big-endian 1234)
            0, 0, 0, 0, 0, 0, 4, 210,
            // Language
            (byte)'u', (byte)'k',
            // Capabilities
            (byte)'w', (byte)'m', (byte)'a', (byte)'l'
        };

        CollectionAssert.AreEqual(expected, result);
    }

    #endregion

    #region BYE Message Tests

    [TestMethod]
    public void EncodeShouldProduceCorrectBytesWhenByeMessage()
    {
        var bye = new ByeMessage { DisconnectReason = 55 };

        var result = _codec.Encode(bye);

        byte[] expected = new byte[]
        {
            (byte)'B', (byte)'Y', (byte)'E', (byte)'!',
            // Payload length (big-endian 1)
            0, 0, 0, 1,
            // Disconnect reason
            55
        };

        CollectionAssert.AreEqual(expected, result);
    }

    #endregion

    #region STAT Message Tests

    [TestMethod]
    public void EncodeShouldProduceCorrectBytesWhenStatMessage()
    {
        var statData = new StatusData
        {
            Crlf = 0,
            BufferSize = 1234,
            Fullness = 5678,
            BytesReceived = 9123,
            SignalStrength = 45,
            Jiffies = TimeSpan.FromMilliseconds(6789),
            OutputBufferSize = 1234,
            OutputBufferFullness = 5678,
            ElapsedSeconds = 9012,
            Voltage = 3456,
            ElapsedMilliseconds = 7890,
            Timestamp = TimeSpan.FromMilliseconds(1234),
            ErrorCode = 5678
        };

        var stat = new StatMessage
        {
            EventCode = "STMt",
            StatusData = statData
        };

        var result = _codec.Encode(stat);

        byte[] expected = new byte[]
        {
            // Opcode
            (byte)'S', (byte)'T', (byte)'A', (byte)'T',
            // Payload length (big-endian 53 = 4 event code + 49 status data bytes)
            0, 0, 0, 53,
            // Event code
            (byte)'S', (byte)'T', (byte)'M', (byte)'t',
            // Crlf
            0,
            // Reserved (2 bytes)
            0, 0,
            // BufferSize (1234)
            0, 0, 4, 210,
            // Fullness (5678)
            0, 0, 22, 46,
            // BytesReceived (9123)
            0, 0, 0, 0, 0, 0, 35, 163,
            // SignalStrength (45)
            0, 45,
            // Jiffies (6789 ms)
            0, 0, 26, 133,
            // OutputBufferSize (1234)
            0, 0, 4, 210,
            // OutputBufferFullness (5678)
            0, 0, 22, 46,
            // ElapsedSeconds (9012)
            0, 0, 35, 52,
            // Voltage (3456)
            13, 128,
            // ElapsedMilliseconds (7890)
            0, 0, 30, 210,
            // Timestamp (1234 ms)
            0, 0, 4, 210,
            // ErrorCode (5678)
            22, 46
        };

        CollectionAssert.AreEqual(expected, result);
    }

    #endregion

    #region SETD Message Tests

    [TestMethod]
    public void EncodeShouldProduceCorrectBytesWhenSetNameMessage()
    {
        var setName = new SetNameMessage { Name = "BadBoy" };

        var result = _codec.Encode(setName);

        byte[] expected = new byte[]
        {
            (byte)'S', (byte)'E', (byte)'T', (byte)'D',
            // Payload length (big-endian 7 = 1 tag + 6 name chars)
            0, 0, 0, 7,
            // Tag
            0,
            // Name
            (byte)'B', (byte)'a', (byte)'d', (byte)'B', (byte)'o', (byte)'y'
        };

        CollectionAssert.AreEqual(expected, result);
    }

    #endregion

    #region SERV Message Tests

    [TestMethod]
    public void DecodeShouldParseCorrectlyWhenServMessageWithSyncGroup()
    {
        byte[] data = new byte[]
        {
            (byte)'s', (byte)'e', (byte)'r', (byte)'v',
            172, 16, 1, 2, // IP: 172.16.1.2
            (byte)'s', (byte)'y', (byte)'n', (byte)'c'
        };

        var result = _codec.Decode(data);

        Assert.IsInstanceOfType(result, typeof(ServMessage));
        var serv = (ServMessage)result;
        Assert.AreEqual(IPAddress.Parse("172.16.1.2"), serv.IpAddress);
        Assert.AreEqual("sync", serv.SyncGroupId);
    }

    [TestMethod]
    public void DecodeShouldParseCorrectlyWhenServMessageWithoutSyncGroup()
    {
        byte[] data = new byte[]
        {
            (byte)'s', (byte)'e', (byte)'r', (byte)'v',
            192, 168, 1, 100
        };

        var result = _codec.Decode(data);

        Assert.IsInstanceOfType(result, typeof(ServMessage));
        var serv = (ServMessage)result;
        Assert.AreEqual(IPAddress.Parse("192.168.1.100"), serv.IpAddress);
        Assert.IsNull(serv.SyncGroupId);
    }

    #endregion

    #region STRM Message Tests

    [TestMethod]
    public void DecodeShouldParseCorrectlyWhenStrmStatusMessage()
    {
        byte[] data = new byte[]
        {
            (byte)'s', (byte)'t', (byte)'r', (byte)'m',
            (byte)'t', // Status command
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, // Padding
            15, 16, 17, 18 // Timestamp: 252711186 ms
        };

        var result = _codec.Decode(data);

        Assert.IsInstanceOfType(result, typeof(StatusRequestMessage));
        var status = (StatusRequestMessage)result;
        Assert.AreEqual(252711186, status.Interval.TotalMilliseconds);
    }

    [TestMethod]
    public void DecodeShouldParseCorrectlyWhenStrmStopMessage()
    {
        byte[] data = new byte[]
        {
            (byte)'s', (byte)'t', (byte)'r', (byte)'m',
            (byte)'q', // Stop command
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24
        };

        var result = _codec.Decode(data);

        Assert.IsInstanceOfType(result, typeof(StopMessage));
    }

    [TestMethod]
    public void DecodeShouldParseCorrectlyWhenStrmFlushMessage()
    {
        byte[] data = new byte[]
        {
            (byte)'s', (byte)'t', (byte)'r', (byte)'m',
            (byte)'f' // Flush command
        };

        var result = _codec.Decode(data);

        Assert.IsInstanceOfType(result, typeof(FlushMessage));
    }

    [TestMethod]
    public void DecodeShouldParseCorrectlyWhenStrmPauseMessage()
    {
        byte[] data = new byte[]
        {
            (byte)'s', (byte)'t', (byte)'r', (byte)'m',
            (byte)'p', // Pause command
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, // Padding
            14, 15, 16, 17 // Timestamp: 235868177 ms
        };

        var result = _codec.Decode(data);

        Assert.IsInstanceOfType(result, typeof(PauseMessage));
        var pause = (PauseMessage)result;
        Assert.AreEqual(235868177, pause.Timestamp.TotalMilliseconds);
    }

    [TestMethod]
    public void DecodeShouldParseCorrectlyWhenStrmUnpauseMessage()
    {
        byte[] data = new byte[]
        {
            (byte)'s', (byte)'t', (byte)'r', (byte)'m',
            (byte)'u', // Unpause command
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, // Padding
            14, 15, 16, 17 // Timestamp
        };

        var result = _codec.Decode(data);

        Assert.IsInstanceOfType(result, typeof(UnpauseMessage));
        var unpause = (UnpauseMessage)result;
        Assert.AreEqual(235868177, unpause.Timestamp.TotalMilliseconds);
    }

    [TestMethod]
    public void DecodeShouldParseCorrectlyWhenStrmSkipMessage()
    {
        byte[] data = new byte[]
        {
            (byte)'s', (byte)'t', (byte)'r', (byte)'m',
            (byte)'a', // Skip command
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, // Padding
            14, 15, 16, 17 // Timestamp
        };

        var result = _codec.Decode(data);

        Assert.IsInstanceOfType(result, typeof(SkipMessage));
        var skip = (SkipMessage)result;
        Assert.AreEqual(235868177, skip.Timestamp.TotalMilliseconds);
    }

    [TestMethod]
    public void DecodeShouldReturnUnknownWhenStrmUnrecognisedCommand()
    {
        byte[] data = new byte[]
        {
            (byte)'s', (byte)'t', (byte)'r', (byte)'m',
            (byte)'x', // Unknown command
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24
        };

        var result = _codec.Decode(data);

        Assert.IsInstanceOfType(result, typeof(UnknownServerMessage));
        var unknown = (UnknownServerMessage)result;
        Assert.AreEqual("strm_x", unknown.Opcode);
    }

    #endregion

    #region AUDE Message Tests

    [TestMethod]
    public void DecodeShouldParseCorrectlyWhenEnableMessage()
    {
        byte[] data = new byte[]
        {
            (byte)'a', (byte)'u', (byte)'d', (byte)'e',
            0, // SPDIF disabled
            1  // DAC enabled
        };

        var result = _codec.Decode(data);

        Assert.IsInstanceOfType(result, typeof(EnableMessage));
        var enable = (EnableMessage)result;
        Assert.IsFalse(enable.SpdifEnabled);
        Assert.IsTrue(enable.DacEnabled);
    }

    #endregion

    #region AUDG Message Tests

    [TestMethod]
    public void DecodeShouldParseCorrectlyWhenGainMessage()
    {
        byte[] data = new byte[]
        {
            (byte)'a', (byte)'u', (byte)'d', (byte)'g',
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // Padding (10 bytes)
            0, 1, 0, 0, // Left gain: 65536 / 65536 = 1.0
            0, 0, 128, 0 // Right gain: 32768 / 65536 = 0.5
        };

        var result = _codec.Decode(data);

        Assert.IsInstanceOfType(result, typeof(GainMessage));
        var gain = (GainMessage)result;
        Assert.AreEqual(1.0, gain.LeftGain, 0.0001);
        Assert.AreEqual(0.5, gain.RightGain, 0.0001);
    }

    #endregion

    #region SETD Server Message Tests

    [TestMethod]
    public void DecodeShouldParseCorrectlyWhenSetdQueryName()
    {
        byte[] data = new byte[]
        {
            (byte)'s', (byte)'e', (byte)'t', (byte)'d',
            0 // Query name
        };

        var result = _codec.Decode(data);

        Assert.IsInstanceOfType(result, typeof(QueryNameMessage));
    }

    [TestMethod]
    public void DecodeShouldParseCorrectlyWhenSetdSetName()
    {
        byte[] data = new byte[]
        {
            (byte)'s', (byte)'e', (byte)'t', (byte)'d',
            0, // Set name command
            (byte)'n', (byte)'e', (byte)'w', (byte)'n', (byte)'a', (byte)'m', (byte)'e',
            0 // Null terminator
        };

        var result = _codec.Decode(data);

        Assert.IsInstanceOfType(result, typeof(SetNameRequestMessage));
        var setName = (SetNameRequestMessage)result;
        Assert.AreEqual("newname", setName.Name);
    }

    [TestMethod]
    public void DecodeShouldParseCorrectlyWhenSetdDisableDac()
    {
        byte[] data = new byte[]
        {
            (byte)'s', (byte)'e', (byte)'t', (byte)'d',
            4 // Disable DAC command
        };

        var result = _codec.Decode(data);

        Assert.IsInstanceOfType(result, typeof(DisableDacMessage));
    }

    #endregion

    #region Unknown Message Tests

    [TestMethod]
    public void DecodeShouldReturnUnknownMessageWhenUnknownOpcode()
    {
        byte[] data = new byte[]
        {
            (byte)'X', (byte)'Y', (byte)'Z', (byte)'Q',
            1, 2, 3, 4
        };

        var result = _codec.Decode(data);

        Assert.IsInstanceOfType(result, typeof(UnknownServerMessage));
        var unknown = (UnknownServerMessage)result;
        Assert.AreEqual("XYZQ", unknown.Opcode);
        CollectionAssert.AreEqual(data, unknown.RawData);
    }

    #endregion

    #region Round-trip Tests

    [TestMethod]
    public void RoundTripShouldSucceedWhenHeloMessage()
    {
        var original = new HeloMessage
        {
            DeviceId = 12,
            Revision = 5,
            MacAddress = [0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF],
            Uuid = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray(),
            WlanChannelList = 0x1234,
            BytesReceived = 999999,
            Language = ['e', 'n'],
            Capabilities = new Capabilities(),
        };

        var encoded = _codec.Encode(original);

        // Verify we can at least encode without throwing
        Assert.IsNotNull(encoded);
        Assert.IsGreaterThan(4, encoded.Length);
        Assert.AreEqual('H', (char)encoded[0]);
        Assert.AreEqual('E', (char)encoded[1]);
        Assert.AreEqual('L', (char)encoded[2]);
        Assert.AreEqual('O', (char)encoded[3]);
    }

    #endregion

    #region VERS Message Tests

    [TestMethod]
    public void DecodeShouldParseCorrectlyWhenVersMessage()
    {
        byte[] data = new byte[]
        {
            (byte)'v', (byte)'e', (byte)'r', (byte)'s',
            (byte)'8', (byte)'.', (byte)'5', (byte)'.', (byte)'2'
        };

        var result = _codec.Decode(data);

        Assert.IsInstanceOfType(result, typeof(VersMessage));
        var vers = (VersMessage)result;
        Assert.AreEqual("8.5.2", vers.Version);
    }

    [TestMethod]
    public void DecodeShouldParseCorrectlyWhenVersMessageWithLongVersion()
    {
        var versionString = "LMS Version: 9.0.4 - Manually compiled somewhere";
        byte[] data = new byte[4 + versionString.Length];
        data[0] = (byte)'v';
        data[1] = (byte)'e';
        data[2] = (byte)'r';
        data[3] = (byte)'s';
        System.Text.Encoding.ASCII.GetBytes(versionString, 0, versionString.Length, data, 4);

        var result = _codec.Decode(data);

        Assert.IsInstanceOfType(result, typeof(VersMessage));
        var vers = (VersMessage)result;
        Assert.AreEqual(versionString, vers.Version);
    }

    #endregion
}
