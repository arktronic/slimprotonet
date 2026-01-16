using System;
using SlimProtoNet.Protocol.Messages;
using SlimProtoNet.Wrappers;

namespace SlimProtoNet.Client;

/// <summary>
/// Tracks playback status information sent periodically to LMS.
/// </summary>
public class StatusData
{
    private readonly StopwatchWrapper _stopwatch;

    public byte Crlf { get; set; }
    public uint BufferSize { get; set; }
    public uint Fullness { get; set; }
    public ulong BytesReceived { get; set; }
    public ushort SignalStrength { get; set; }

    /// <summary>
    /// Player uptime (time elapsed since StatusData was created).
    /// Automatically updated when CreateStatusMessage() is called.
    /// This is NOT playback position - use ElapsedSeconds/ElapsedMilliseconds for that.
    /// </summary>
    public TimeSpan Jiffies { get; protected internal set; }

    public uint OutputBufferSize { get; set; }
    public uint OutputBufferFullness { get; set; }

    /// <summary>
    /// Elapsed playback time in seconds (track position).
    /// </summary>
    public uint ElapsedSeconds { get; set; }

    public ushort Voltage { get; set; }

    /// <summary>
    /// Elapsed playback time in milliseconds (track position).
    /// </summary>
    public uint ElapsedMilliseconds { get; set; }
    public TimeSpan Timestamp { get; set; }
    public ushort ErrorCode { get; set; }

    /// <summary>
    /// Creates a new status tracker.
    /// </summary>
    public StatusData() : this(new StopwatchWrapper())
    {
    }

    /// <summary>
    /// Creates a new status tracker with injected stopwatch provider.
    /// </summary>
    /// <param name="stopwatch">Stopwatch provider.</param>
    protected internal StatusData(StopwatchWrapper stopwatch)
    {
        _stopwatch = stopwatch;
        _stopwatch.Restart();
    }

    public virtual void AddCrlf(byte numCrlf)
    {
        unchecked
        {
            Crlf = (byte)(Crlf + numCrlf);
        }
    }

    public virtual void SetFullness(uint fullness)
    {
        Fullness = fullness;
    }

    public virtual void AddBytesReceived(ulong bytesReceived)
    {
        unchecked
        {
            BytesReceived += bytesReceived;
        }
    }

    public virtual void SetJiffies(TimeSpan jiffies)
    {
        Jiffies = jiffies;
    }

    public virtual void SetOutputBufferSize(uint outputBufferSize)
    {
        OutputBufferSize = outputBufferSize;
    }

    public virtual void SetOutputBufferFullness(uint outputBufferFullness)
    {
        OutputBufferFullness = outputBufferFullness;
    }

    public virtual void SetElapsedSeconds(uint elapsedSeconds)
    {
        ElapsedSeconds = elapsedSeconds;
    }

    public virtual void SetElapsedMilliseconds(uint elapsedMilliseconds)
    {
        ElapsedMilliseconds = elapsedMilliseconds;
    }

    public virtual void SetBufferSize(uint size)
    {
        BufferSize = size;
    }

    public virtual void SetTimestamp(TimeSpan timestamp)
    {
        Timestamp = timestamp;
    }

    public virtual TimeSpan GetJiffies()
    {
        return Jiffies;
    }

    /// <summary>
    /// Creates a status message for sending to the server.
    /// </summary>
    /// <param name="statusCode">The status event code.</param>
    /// <returns>A STAT message with current status data.</returns>
    public virtual StatMessage CreateStatusMessage(StatusCode statusCode)
    {
        SetJiffies(_stopwatch.Elapsed);
        return new StatMessage
        {
            EventCode = statusCode.ToEventCode(),
            StatusData = this
        };
    }
}
