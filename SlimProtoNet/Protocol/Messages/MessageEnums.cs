using System;

namespace SlimProtoNet.Protocol.Messages;

/// <summary>
/// Audio stream auto-start behavior.
/// </summary>
public enum AutoStart
{
    None,
    Auto,
    Direct,
    AutoDirect
}

/// <summary>
/// Audio format codec types.
/// </summary>
public enum Format
{
    Pcm,
    Mp3,
    Flac,
    Wma,
    Ogg,
    Aac,
    Alac
}

/// <summary>
/// PCM audio sample size in bits.
/// </summary>
public enum PcmSampleSize
{
    Eight,
    Sixteen,
    Twenty,
    ThirtyTwo,
    SelfDescribing
}

/// <summary>
/// PCM audio sample rate in Hz.
/// </summary>
public enum PcmSampleRate
{
    Rate11000,
    Rate22000,
    Rate32000,
    Rate44100,
    Rate48000,
    Rate8000,
    Rate12000,
    Rate16000,
    Rate24000,
    Rate96000,
    SelfDescribing
}

/// <summary>
/// PCM audio channel configuration.
/// </summary>
public enum PcmChannels
{
    Mono,
    Stereo,
    SelfDescribing
}

/// <summary>
/// PCM audio byte order (endianness).
/// </summary>
public enum PcmEndian
{
    Big,
    Little,
    SelfDescribing
}

/// <summary>
/// S/PDIF digital output enable mode.
/// </summary>
public enum SpdifEnable
{
    Auto,
    On,
    Off
}

/// <summary>
/// Track transition type.
/// </summary>
public enum TransType
{
    None,
    Crossfade,
    FadeIn,
    FadeOut,
    FadeInOut
}

/// <summary>
/// Audio stream control flags.
/// </summary>
[Flags]
public enum StreamFlags : byte
{
    None = 0,
    InfiniteLoop = 0b10000000,
    NoRestartDecoder = 0b01000000,
    InvertPolarityLeft = 0b00000001,
    InvertPolarityRight = 0b00000010
}
