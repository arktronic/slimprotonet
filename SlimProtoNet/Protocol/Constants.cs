namespace SlimProtoNet.Protocol;

/// <summary>
/// Protocol constants for SlimProto communication.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Default TCP/UDP port for SlimProto communication (3483).
    /// </summary>
    public const ushort SLIM_PORT = 3483;
    
    /// <summary>
    /// Client hello/handshake message opcode.
    /// </summary>
    public const string CLIENT_HELO = "HELO";
    
    /// <summary>
    /// Client disconnect message opcode.
    /// </summary>
    public const string CLIENT_BYE = "BYE!";
    
    /// <summary>
    /// Client status update message opcode.
    /// </summary>
    public const string CLIENT_STAT = "STAT";
    
    /// <summary>
    /// Client update preferences message opcode.
    /// </summary>
    public const string CLIENT_SETD = "SETD";
    
    /// <summary>
    /// Server announcement message opcode.
    /// </summary>
    public const string SERVER_SERV = "serv";

    /// <summary>
    /// Server get/set settings message opcode.
    /// </summary>
    public const string SERVER_SETD = "setd";

    /// <summary>
    /// Server stream control message opcode.
    /// </summary>
    public const string SERVER_STRM = "strm";
    
    /// <summary>
    /// Server audio enable/disable message opcode.
    /// </summary>
    public const string SERVER_AUDE = "aude";
    
    /// <summary>
    /// Server audio gain (volume) message opcode.
    /// </summary>
    public const string SERVER_AUDG = "audg";
    
    /// <summary>
    /// Server version information message opcode.
    /// </summary>
    public const string SERVER_VERS = "vers";

    /// <summary>
    /// Stream command for status request.
    /// </summary>
    public const char STREAM_COMMAND_STATUS = 't';
    
    /// <summary>
    /// Stream command to start playback.
    /// </summary>
    public const char STREAM_COMMAND_START = 's';
    
    /// <summary>
    /// Stream command to stop playback.
    /// </summary>
    public const char STREAM_COMMAND_STOP = 'q';
    
    /// <summary>
    /// Stream command to flush buffers.
    /// </summary>
    public const char STREAM_COMMAND_FLUSH = 'f';
    
    /// <summary>
    /// Stream command to pause playback.
    /// </summary>
    public const char STREAM_COMMAND_PAUSE = 'p';
    
    /// <summary>
    /// Stream command to unpause playback.
    /// </summary>
    public const char STREAM_COMMAND_UNPAUSE = 'u';
    
    /// <summary>
    /// Stream command to skip ahead.
    /// </summary>
    public const char STREAM_COMMAND_SKIP = 'a';
}
