using NSubstitute;
using SlimProtoNet.Client;
using SlimProtoNet.Protocol.Messages;
using SlimProtoNet.Wrappers;

namespace SlimProtoNet.UnitTests.Client;

[TestClass]
public class StatusDataTests
{
    private StopwatchWrapper _mockStopwatch = null!;
    private StatusData _statusData = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockStopwatch = Substitute.For<StopwatchWrapper>();
        _statusData = new StatusData(_mockStopwatch);
    }

    [TestMethod]
    public void AddCrlfShouldIncrementCrlf()
    {
        _statusData.AddCrlf(5);
        Assert.AreEqual((byte)5, _statusData.Crlf);
        
        _statusData.AddCrlf(3);
        Assert.AreEqual((byte)8, _statusData.Crlf);
    }

    [TestMethod]
    public void AddCrlfShouldWrapAround()
    {
        _statusData.Crlf = 250;
        _statusData.AddCrlf(10);
        Assert.AreEqual((byte)4, _statusData.Crlf);
    }

    [TestMethod]
    public void AddBytesReceivedShouldIncrementCounter()
    {
        _statusData.AddBytesReceived(1000);
        Assert.AreEqual(1000UL, _statusData.BytesReceived);
        
        _statusData.AddBytesReceived(500);
        Assert.AreEqual(1500UL, _statusData.BytesReceived);
    }

    [TestMethod]
    public void AddBytesReceivedShouldWrapAround()
    {
        _statusData.BytesReceived = ulong.MaxValue - 100;
        _statusData.AddBytesReceived(200);
        Assert.AreEqual(99UL, _statusData.BytesReceived);
    }

    [TestMethod]
    public void SetFullnessShouldUpdateFullness()
    {
        _statusData.SetFullness(12345);
        Assert.AreEqual(12345U, _statusData.Fullness);
    }

    [TestMethod]
    public void SetJiffiesShouldUpdateJiffies()
    {
        var jiffies = TimeSpan.FromMilliseconds(5000);
        _statusData.SetJiffies(jiffies);
        Assert.AreEqual(jiffies, _statusData.Jiffies);
    }

    [TestMethod]
    public void GetJiffiesShouldReturnCurrentJiffies()
    {
        var jiffies = TimeSpan.FromSeconds(10);
        _statusData.SetJiffies(jiffies);
        Assert.AreEqual(jiffies, _statusData.GetJiffies());
    }

    [TestMethod]
    public void SetOutputBufferSizeShouldUpdateOutputBufferSize()
    {
        _statusData.SetOutputBufferSize(8192);
        Assert.AreEqual(8192U, _statusData.OutputBufferSize);
    }

    [TestMethod]
    public void SetOutputBufferFullnessShouldUpdateOutputBufferFullness()
    {
        _statusData.SetOutputBufferFullness(4096);
        Assert.AreEqual(4096U, _statusData.OutputBufferFullness);
    }

    [TestMethod]
    public void SetElapsedSecondsShouldUpdateElapsedSeconds()
    {
        _statusData.SetElapsedSeconds(120);
        Assert.AreEqual(120U, _statusData.ElapsedSeconds);
    }

    [TestMethod]
    public void SetElapsedMillisecondsShouldUpdateElapsedMilliseconds()
    {
        _statusData.SetElapsedMilliseconds(500);
        Assert.AreEqual(500U, _statusData.ElapsedMilliseconds);
    }

    [TestMethod]
    public void SetBufferSizeShouldUpdateBufferSize()
    {
        _statusData.SetBufferSize(16384);
        Assert.AreEqual(16384U, _statusData.BufferSize);
    }

    [TestMethod]
    public void SetTimestampShouldUpdateTimestamp()
    {
        var timestamp = TimeSpan.FromMinutes(5);
        _statusData.SetTimestamp(timestamp);
        Assert.AreEqual(timestamp, _statusData.Timestamp);
    }

    [TestMethod]
    public void CreateStatusMessageShouldCreateStatMessageWithEventCode()
    {
        var elapsed = TimeSpan.FromSeconds(30);
        _mockStopwatch.Elapsed.Returns(elapsed);

        var message = _statusData.CreateStatusMessage(StatusCode.Timer);

        Assert.IsInstanceOfType<StatMessage>(message);
        var statMessage = (StatMessage)message;
        Assert.AreEqual("STMt", statMessage.EventCode);
        Assert.AreEqual(elapsed, statMessage.StatusData.Jiffies);
    }

    [TestMethod]
    public void CreateStatusMessageShouldUpdateJiffiesFromStopwatch()
    {
        var elapsed = TimeSpan.FromMilliseconds(12345);
        _mockStopwatch.Elapsed.Returns(elapsed);

        _statusData.CreateStatusMessage(StatusCode.Connect);

        Assert.AreEqual(elapsed, _statusData.Jiffies);
    }

    [TestMethod]
    public void CreateStatusMessageShouldReturnMessageWithSameStatusDataInstance()
    {
        var elapsed = TimeSpan.FromSeconds(5);
        _mockStopwatch.Elapsed.Returns(elapsed);
        
        _statusData.BufferSize = 1024;
        _statusData.Fullness = 512;

        var message = _statusData.CreateStatusMessage(StatusCode.Pause);
        var statMessage = (StatMessage)message;

        Assert.AreSame(_statusData, statMessage.StatusData);
        Assert.AreEqual(1024U, statMessage.StatusData.BufferSize);
        Assert.AreEqual(512U, statMessage.StatusData.Fullness);
    }

    [TestMethod]
    public void ConstructorShouldInitializeStopwatch()
    {
        _mockStopwatch.Received(1).Restart();
    }
}
