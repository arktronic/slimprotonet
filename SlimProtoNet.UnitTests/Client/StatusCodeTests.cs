using SlimProtoNet.Client;

namespace SlimProtoNet.UnitTests.Client;

[TestClass]
public class StatusCodeTests
{
    [TestMethod]
    public void ToEventCodeShouldReturnSTMcWhenConnect()
    {
        var result = StatusCode.Connect.ToEventCode();
        Assert.AreEqual("STMc", result);
    }

    [TestMethod]
    public void ToEventCodeShouldReturnSTMdWhenDecoderReady()
    {
        var result = StatusCode.DecoderReady.ToEventCode();
        Assert.AreEqual("STMd", result);
    }

    [TestMethod]
    public void ToEventCodeShouldReturnSTMeWhenStreamEstablished()
    {
        var result = StatusCode.StreamEstablished.ToEventCode();
        Assert.AreEqual("STMe", result);
    }

    [TestMethod]
    public void ToEventCodeShouldReturnSTMfWhenFlushed()
    {
        var result = StatusCode.Flushed.ToEventCode();
        Assert.AreEqual("STMf", result);
    }

    [TestMethod]
    public void ToEventCodeShouldReturnSTMhWhenHeadersReceived()
    {
        var result = StatusCode.HeadersReceived.ToEventCode();
        Assert.AreEqual("STMh", result);
    }

    [TestMethod]
    public void ToEventCodeShouldReturnSTMlWhenBufferThreshold()
    {
        var result = StatusCode.BufferThreshold.ToEventCode();
        Assert.AreEqual("STMl", result);
    }

    [TestMethod]
    public void ToEventCodeShouldReturnSTMnWhenNotSupported()
    {
        var result = StatusCode.NotSupported.ToEventCode();
        Assert.AreEqual("STMn", result);
    }

    [TestMethod]
    public void ToEventCodeShouldReturnSTMoWhenOutputUnderrun()
    {
        var result = StatusCode.OutputUnderrun.ToEventCode();
        Assert.AreEqual("STMo", result);
    }

    [TestMethod]
    public void ToEventCodeShouldReturnSTMpWhenPause()
    {
        var result = StatusCode.Pause.ToEventCode();
        Assert.AreEqual("STMp", result);
    }

    [TestMethod]
    public void ToEventCodeShouldReturnSTMrWhenResume()
    {
        var result = StatusCode.Resume.ToEventCode();
        Assert.AreEqual("STMr", result);
    }

    [TestMethod]
    public void ToEventCodeShouldReturnSTMsWhenTrackStarted()
    {
        var result = StatusCode.TrackStarted.ToEventCode();
        Assert.AreEqual("STMs", result);
    }

    [TestMethod]
    public void ToEventCodeShouldReturnSTMtWhenTimer()
    {
        var result = StatusCode.Timer.ToEventCode();
        Assert.AreEqual("STMt", result);
    }

    [TestMethod]
    public void ToEventCodeShouldReturnSTMuWhenUnderrun()
    {
        var result = StatusCode.Underrun.ToEventCode();
        Assert.AreEqual("STMu", result);
    }
}
