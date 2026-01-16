using SlimProtoNet.Client;

namespace SlimProtoNet.Protocol.Messages;

/// <summary>
/// Base class for messages sent from client to server.
/// </summary>
public abstract class ClientMessage
{
}

/// <summary>
/// Client handshake message announcing capabilities.
/// </summary>
public class HeloMessage : ClientMessage
{
    public byte DeviceId { get; set; }
    public byte Revision { get; set; }
    public byte[] MacAddress { get; set; } = new byte[6];
    public byte[] Uuid { get; set; } = new byte[16];
    public ushort WlanChannelList { get; set; }
    public ulong BytesReceived { get; set; }
    public char[] Language { get; set; } = new char[2];
    public Capabilities Capabilities { get; set; } = new(false);
}

/// <summary>
/// Client status update message sent periodically.
/// </summary>
public class StatMessage : ClientMessage
{
    public string EventCode { get; set; } = string.Empty;
    public StatusData StatusData { get; set; } = new StatusData();
}

/// <summary>
/// Client disconnect notification.
/// </summary>
public class ByeMessage : ClientMessage
{
    public byte DisconnectReason { get; set; }
}

/// <summary>
/// Client name change request.
/// </summary>
public class SetNameMessage : ClientMessage
{
    public string Name { get; set; } = string.Empty;
}
