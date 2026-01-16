using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SlimProtoNet.Protocol;
using SlimProtoNet.Protocol.Messages;
using SlimProtoNet.Wrappers;

namespace SlimProtoNet.Client;

/// <summary>
/// Main client for connecting to LMS using the SlimProto protocol.
/// </summary>
public class SlimClient : IDisposable
{
    private readonly SlimCodec _codec;
    private readonly TcpClientFactory _tcpClientFactory;
    private TcpClientWrapper? _tcpClient;
    private Stream? _networkStream;
    private byte[] _macAddress = new byte[6];
    private bool _disposed;

    /// <summary>
    /// Gets the server endpoint we are connected to, or null if not connected.
    /// </summary>
    public virtual IPEndPoint? ServerEndPoint { get; private set; }

    /// <summary>
    /// Gets the MAC address used for this client connection.
    /// </summary>
    public virtual byte[] MacAddress => _macAddress;

    /// <summary>
    /// Gets whether the client is currently connected to a server.
    /// </summary>
    public virtual bool IsConnected => _tcpClient?.Connected == true;

    /// <summary>
    /// Creates a new SlimProto client
    /// </summary>
    public SlimClient() : this(new SlimCodec(), new TcpClientFactory())
    {
    }

    /// <summary>
    /// Creates a new SlimProto client with injected dependencies.
    /// </summary>
    protected internal SlimClient(SlimCodec codec, TcpClientFactory tcpClientFactory)
    {
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _tcpClientFactory = tcpClientFactory ?? throw new ArgumentNullException(nameof(tcpClientFactory));
    }

    /// <summary>
    /// Connect to the LMS server and send the HELO handshake.
    /// </summary>
    /// <param name="server">Server endpoint to connect to</param>
    /// <param name="capabilities">Client capabilities to announce</param>
    /// <param name="macAddress">MAC address to identify this client (6 bytes). If null, uses fallback 01:02:03:04:05:06</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public virtual async Task ConnectAsync(IPEndPoint server, Capabilities capabilities, byte[]? macAddress = null, CancellationToken cancellationToken = default)
    {
        var helo = new HeloMessage
        {
            DeviceId = 12,
            Revision = 99,
            MacAddress = macAddress ?? [0x01, 0x02, 0x03, 0x04, 0x05, 0x06],
            Uuid = new byte[16], // Zero UUID
            WlanChannelList = 0,
            BytesReceived = 0,
            Language = ['e', 'n'],
            Capabilities = capabilities
        };

        await ConnectAsync(server, helo, cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task ConnectAsync(IPEndPoint server, HeloMessage heloMessage, CancellationToken cancellationToken = default)
    {
        if (server == null)
        {
            throw new ArgumentNullException(nameof(server));
        }

        if (string.IsNullOrWhiteSpace(heloMessage.Capabilities.ToString()))
        {
            throw new ArgumentNullException(nameof(heloMessage.Capabilities));
        }

        _macAddress = heloMessage.MacAddress;
        if (_macAddress.Length != 6)
        {
            throw new ArgumentException("MAC address must be exactly 6 bytes", nameof(heloMessage.MacAddress));
        }

        // Clean up any existing connection to allow reconnection
        CleanupConnection();

        // Connect TCP
        _tcpClient = _tcpClientFactory.CreateTcpClient();
        await _tcpClient.ConnectAsync(server.Address, server.Port).ConfigureAwait(false);

        // Store the server endpoint
        ServerEndPoint = server;

        // Get the network stream
        _networkStream = _tcpClient.GetStream();

        await SendAsync(heloMessage, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Send a client message to the server.
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public virtual async Task SendAsync(ClientMessage message, CancellationToken cancellationToken = default)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        if (_networkStream == null)
        {
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");
        }

        // Client messages are sent RAW (no 2-byte frame wrapper)
        // Format: [opcode(4)][length(4)][payload]
        var encoded = _codec.Encode(message);

        await _networkStream.WriteAsync(encoded, 0, encoded.Length, cancellationToken).ConfigureAwait(false);
        await _networkStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Receive a server message.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The received server message</returns>
    public virtual async Task<ServerMessage> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        if (_networkStream == null)
        {
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");
        }

        var frameData = await _networkStream.ReadFrameAsync(cancellationToken).ConfigureAwait(false);
        return _codec.Decode(frameData);
    }

    /// <summary>
    /// Disconnect from the server gracefully by sending BYE message and closing the connection.
    /// The client can be reconnected after calling this method.
    /// </summary>
    /// <param name="reason">Disconnect reason code (default 0)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public virtual async Task DisconnectAsync(byte reason = 0, CancellationToken cancellationToken = default)
    {
        if (_networkStream != null)
        {
            try
            {
                var bye = new ByeMessage { DisconnectReason = reason };
                await SendAsync(bye, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors during disconnect
            }
        }

        CleanupConnection();
    }

    /// <summary>
    /// Close and cleanup the current connection without sending BYE message.
    /// The client can be reconnected after calling this method.
    /// </summary>
    private void CleanupConnection()
    {
        _networkStream?.Dispose();
        _networkStream = null;

        _tcpClient?.Dispose();
        _tcpClient = null;

        ServerEndPoint = null;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                CleanupConnection();
            }
            _disposed = true;
        }
    }
}
