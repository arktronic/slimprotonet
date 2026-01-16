using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SlimProtoNet.Protocol;
using static SlimProtoNet.Discovery.Server;

namespace SlimProtoNet.Discovery;

/// <summary>
/// Discovers LMS instances on the local network via UDP broadcast.
/// </summary>
public class ServerDiscovery
{
    private static readonly byte[] DiscoveryMessage = Encoding.ASCII.GetBytes("eNAME\0IPAD\0JSON\0VERS");

    /// <summary>
    /// Discovers one LMS instance on the local network.
    /// </summary>
    /// <param name="bindAddress">The local address to bind the UDP socket to. Defaults to "0.0.0.0".</param>
    /// <param name="timeout">Optional timeout for discovery. If null, waits indefinitely.</param>
    /// <returns>The first discovered server, or null if timeout expires or no server responds.</returns>
    public virtual async Task<Server?> DiscoverAsync(string bindAddress = "0.0.0.0", TimeSpan? timeout = null)
    {
        using var udpClient = new UdpClient(new IPEndPoint(IPAddress.Parse(bindAddress), 0));
        udpClient.EnableBroadcast = true;

        var cancellationTokenSource = timeout.HasValue
            ? new CancellationTokenSource(timeout.Value)
            : new CancellationTokenSource();

        var sendTask = SendDiscoveryBroadcastsAsync(udpClient, cancellationTokenSource.Token);
        
        try
        {
            var receiveResult = await ReceiveDiscoveryResponseAsync(udpClient, cancellationTokenSource.Token);
            cancellationTokenSource.Cancel();
            
            if (receiveResult != null)
            {
                return ParseDiscoveryResponse(receiveResult.Value.Buffer, receiveResult.Value.RemoteEndPoint);
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            cancellationTokenSource.Dispose();
        }
    }

    protected virtual async Task SendDiscoveryBroadcastsAsync(UdpClient udpClient, CancellationToken cancellationToken)
    {
        var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, Constants.SLIM_PORT);
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await udpClient.SendAsync(DiscoveryMessage, DiscoveryMessage.Length, broadcastEndpoint);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    protected virtual async Task<UdpReceiveResult?> ReceiveDiscoveryResponseAsync(UdpClient udpClient, CancellationToken cancellationToken)
    {
        try
        {
            var receiveTask = udpClient.ReceiveAsync();
            var completedTask = await Task.WhenAny(receiveTask, Task.Delay(-1, cancellationToken));
            
            if (completedTask == receiveTask)
            {
                return await receiveTask;
            }
            
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    protected virtual Server? ParseDiscoveryResponse(byte[] buffer, IPEndPoint remoteEndPoint)
    {
        if (buffer.Length == 0 || buffer[0] != (byte)'E')
        {
            return null;
        }

        var tlvMap = DecodeTlv(buffer, 1);
        var serverEndPoint = new IPEndPoint(remoteEndPoint.Address, Constants.SLIM_PORT);
        
        return new Server(serverEndPoint, tlvMap);
    }

    protected virtual Dictionary<string, ServerTlv> DecodeTlv(byte[] buffer, int offset)
    {
        var result = new Dictionary<string, ServerTlv>();
        var position = offset;

        while (position < buffer.Length - 4 && IsAscii(buffer[position]))
        {
            var token = Encoding.ASCII.GetString(buffer, position, 4);
            var valueLength = buffer[position + 4];
            position += 5;

            if (position + valueLength > buffer.Length)
            {
                break;
            }

            var value = Encoding.ASCII.GetString(buffer, position, valueLength);

            ServerTlv tlv;
            switch (token)
            {
                case "NAME":
                    tlv = ServerTlv.Name(value);
                    break;
                case "VERS":
                    tlv = ServerTlv.Version(value);
                    break;
                case "IPAD":
                    if (IPAddress.TryParse(value, out var addr))
                    {
                        tlv = ServerTlv.Address(addr);
                    }
                    else
                    {
                        position += valueLength;
                        continue;
                    }
                    break;
                case "JSON":
                    if (ushort.TryParse(value, out var port))
                    {
                        tlv = ServerTlv.Port(port);
                    }
                    else
                    {
                        position += valueLength;
                        continue;
                    }
                    break;
                default:
                    position += valueLength;
                    continue;
            }

            result[token] = tlv;
            position += valueLength;
        }

        return result;
    }

    private static bool IsAscii(byte b)
    {
        return b >= 32 && b <= 126;
    }
}
