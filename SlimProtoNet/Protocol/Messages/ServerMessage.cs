using System;
using System.Net;

namespace SlimProtoNet.Protocol.Messages;

/// <summary>
/// Base class for messages sent from server to client.
/// </summary>
public abstract class ServerMessage
{
}

/// <summary>
/// Server announcement with IP address.
/// </summary>
public class ServMessage : ServerMessage
{
    public IPAddress IpAddress { get; set; } = IPAddress.Any;
    public string? SyncGroupId { get; set; }
}

/// <summary>
/// Server request for periodic status updates.
/// </summary>
public class StatusRequestMessage : ServerMessage
{
    public TimeSpan Interval { get; set; }
}

/// <summary>
/// Server stream control message with audio parameters.
/// </summary>
public class StreamMessage : ServerMessage
{
    public AutoStart AutoStart { get; set; }
    public Format Format { get; set; }
    public PcmSampleSize PcmSampleSize { get; set; }
    public PcmSampleRate PcmSampleRate { get; set; }
    public PcmChannels PcmChannels { get; set; }
    public PcmEndian PcmEndian { get; set; }
    public uint Threshold { get; set; }
    public SpdifEnable SpdifEnable { get; set; }
    public TimeSpan TransitionPeriod { get; set; }
    public TransType TransitionType { get; set; }
    public StreamFlags Flags { get; set; }
    public TimeSpan OutputThreshold { get; set; }
    public double ReplayGain { get; set; }
    public ushort ServerPort { get; set; }
    public IPAddress ServerIp { get; set; } = IPAddress.Any;
    public string? HttpHeaders { get; set; }
}

/// <summary>
/// Server volume adjustment message.
/// </summary>
public class GainMessage : ServerMessage
{
    public double LeftGain { get; set; }
    public double RightGain { get; set; }
}

/// <summary>
/// Server enable/disable audio output message.
/// </summary>
public class EnableMessage : ServerMessage
{
    public bool SpdifEnabled { get; set; }
    public bool DacEnabled { get; set; }
}

/// <summary>
/// Server command to flush playback buffers.
/// </summary>
public class FlushMessage : ServerMessage
{
}

/// <summary>
/// Server command to stop playback.
/// </summary>
public class StopMessage : ServerMessage
{
}

/// <summary>
/// Server command to pause playback.
/// </summary>
public class PauseMessage : ServerMessage
{
    public TimeSpan Timestamp { get; set; }
}

/// <summary>
/// Server command to unpause playback.
/// </summary>
public class UnpauseMessage : ServerMessage
{
    public TimeSpan Timestamp { get; set; }
}

/// <summary>
/// Server query for client name.
/// </summary>
public class QueryNameMessage : ServerMessage
{
}

/// <summary>
/// Server request to set client name.
/// </summary>
public class SetNameRequestMessage : ServerMessage
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Server command to disable DAC.
/// </summary>
public class DisableDacMessage : ServerMessage
{
}

/// <summary>
/// Server command to skip to next track.
/// </summary>
public class SkipMessage : ServerMessage
{
    public TimeSpan Timestamp { get; set; }
}

/// <summary>
/// Server version information message.
/// </summary>
public class VersMessage : ServerMessage
{
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Fallback for unrecognized server messages.
/// </summary>
public class UnknownServerMessage : ServerMessage
{
    public string Opcode { get; set; } = string.Empty;
    public byte[] RawData { get; set; } = [];
}
