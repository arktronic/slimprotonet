using System;

namespace SlimProtoNet.Client;

/// <summary>
/// Status event codes sent to LMS in STAT messages.
/// </summary>
public enum StatusCode
{
    /// <summary>
    /// Connecting to audio stream (STMc)
    /// </summary>
    Connect,
    
    /// <summary>
    /// Decoder finished processing stream (STMd).
    /// Sent when no more data to decode, but buffer may still be playing.
    /// For track completion: send STMd first, then STMu when buffer drains.
    /// </summary>
    DecoderReady,
    
    /// <summary>
    /// Stream established and ready (STMe)
    /// </summary>
    StreamEstablished,
    
    /// <summary>
    /// Connection flushed (STMf)
    /// </summary>
    Flushed,
    
    /// <summary>
    /// HTTP headers received (STMh)
    /// </summary>
    HeadersReceived,
    
    /// <summary>
    /// Buffer threshold reached (STMl)
    /// </summary>
    BufferThreshold,
    
    /// <summary>
    /// Format not supported (STMn)
    /// </summary>
    NotSupported,
    
    /// <summary>
    /// Output buffer underrun (STMo)
    /// </summary>
    OutputUnderrun,
    
    /// <summary>
    /// Playback paused (STMp)
    /// </summary>
    Pause,
    
    /// <summary>
    /// Playback resumed (STMr)
    /// </summary>
    Resume,
    
    /// <summary>
    /// Track playback started (STMs)
    /// </summary>
    TrackStarted,
    
    /// <summary>
    /// Timer status update (STMt)
    /// </summary>
    Timer,
    
    /// <summary>
    /// Buffer underrun - playback stopped due to empty buffer (STMu).
    /// For track completion: send STMd when decoder finishes, then STMu when buffer drains.
    /// This signals to LMS that track playback is complete.
    /// </summary>
    Underrun
}

/// <summary>
/// Extension methods for StatusCode enum.
/// </summary>
public static class StatusCodeExtensions
{
    /// <summary>
    /// Converts a status code to its 4-character protocol event code.
    /// </summary>
    /// <param name="code">The status code.</param>
    /// <returns>The 4-character event code (e.g., "STMc", "STMt").</returns>
    public static string ToEventCode(this StatusCode code)
    {
        return code switch
        {
            StatusCode.Connect => "STMc",
            StatusCode.DecoderReady => "STMd",
            StatusCode.StreamEstablished => "STMe",
            StatusCode.Flushed => "STMf",
            StatusCode.HeadersReceived => "STMh",
            StatusCode.BufferThreshold => "STMl",
            StatusCode.NotSupported => "STMn",
            StatusCode.OutputUnderrun => "STMo",
            StatusCode.Pause => "STMp",
            StatusCode.Resume => "STMr",
            StatusCode.TrackStarted => "STMs",
            StatusCode.Timer => "STMt",
            StatusCode.Underrun => "STMu",
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, null)
        };
    }
}
