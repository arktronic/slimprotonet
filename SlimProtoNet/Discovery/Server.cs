using System.Collections.Generic;
using System.Net;

namespace SlimProtoNet.Discovery;

/// <summary>
/// Represents a discovered LMS instance.
/// </summary>
public class Server
{
    /// <summary>
    /// TLV (Type-Length-Value) field types in server discovery responses.
    /// </summary>
    public enum ServerTlvType
    {
        Name,
        Version,
        Address,
        Port
    }

    /// <summary>
    /// Represents a TLV field from a server discovery response.
    /// </summary>
    public class ServerTlv
    {
        public ServerTlvType Type { get; set; }
        public object Value { get; set; }

        /// <summary>
        /// Creates a new TLV field.
        /// </summary>
        /// <param name="type">The field type.</param>
        /// <param name="value">The field value.</param>
        public ServerTlv(ServerTlvType type, object value)
        {
            Type = type;
            Value = value;
        }

        public static ServerTlv Name(string name) => new(ServerTlvType.Name, name);
        public static ServerTlv Version(string version) => new(ServerTlvType.Version, version);
        public static ServerTlv Address(IPAddress address) => new(ServerTlvType.Address, address);
        public static ServerTlv Port(ushort port) => new(ServerTlvType.Port, port);
    }

    /// <summary>
    /// The server's network endpoint.
    /// </summary>
    public IPEndPoint EndPoint { get; set; }
    
    /// <summary>
    /// TLV fields from the discovery response (name, version, etc.).
    /// </summary>
    public Dictionary<string, ServerTlv> TlvMap { get; set; }

    /// <summary>
    /// Creates a new server instance.
    /// </summary>
    /// <param name="endPoint">The server's network endpoint.</param>
    /// <param name="tlvMap">Optional TLV fields from discovery.</param>
    public Server(IPEndPoint endPoint, Dictionary<string, ServerTlv>? tlvMap = null)
    {
        EndPoint = endPoint;
        TlvMap = tlvMap ?? new Dictionary<string, ServerTlv>();
    }
}
