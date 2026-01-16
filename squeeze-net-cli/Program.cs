using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using MiniAudioEx.Core.StandardAPI;
using SlimProtoNet.Client;
using SlimProtoNet.Discovery;
using SlimProtoNet.Protocol;
using SlimProtoNet.Protocol.Messages;

namespace SqueezeNetCli
{
    /// <summary>
    /// Cross-platform SlimProto player example using MiniAudioExNET.
    ///
    /// Demonstrates:
    /// - Server discovery
    /// - Connection management
    /// - Status reporting
    /// - Audio playback (PCM/MP3/FLAC)
    /// </summary>
    public class Program
    {
        private static SlimClient? _client;
        private static PlaybackManager? _playback;
        private static MessageHandler? _messageHandler;
        private static CancellationTokenSource? _cts;

        public static async Task<int> Main(string[] args)
        {
            Console.WriteLine("SqueezeNetCli - Cross-Platform SlimProto Player");
            Console.WriteLine("================================================\n");

            if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h" || args[0] == "/?" || args[0] == "/h"))
            {
                ShowUsage();
                return 0;
            }

            // Initialize MiniAudio audio context
            Console.WriteLine("Initializing audio system...");
            AudioContext.Initialize(44100, 2, 2048);
            Console.WriteLine("Audio system initialized\n");

            try
            {
                await RunPlayerAsync(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nFatal error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
            finally
            {
                Cleanup();
                AudioContext.Deinitialize();
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
            return 0;
        }

        private static async Task RunPlayerAsync(string[] args)
        {
            IPEndPoint? serverEndPoint = null;

            // Parse server address from command-line if provided
            if (args.Length > 0)
            {
                serverEndPoint = ParseServerAddress(args[0]);
                if (serverEndPoint == null)
                {
                    Console.WriteLine($"Invalid server address: {args[0]}");
                    Console.WriteLine("Expected format: hostname:port or ip:port (default port is 3483)\n");
                    ShowUsage();
                    return;
                }
                Console.WriteLine($"Using specified server: {serverEndPoint}");
                Console.WriteLine();
            }
            else
            {
                // Discover LMS server via UDP
                Console.WriteLine("Discovering LMS...");
                var server = await DiscoverServerAsync();

                if (server == null)
                {
                    Console.WriteLine("No server found on the network.");
                    Console.WriteLine("Try specifying the server address manually: squeeze-net-cli <server:port>\n");
                    ShowUsage();
                    return;
                }

                serverEndPoint = server.EndPoint;
                Console.WriteLine($"Found server at {serverEndPoint}");
                Console.WriteLine();
            }

            // Configure client capabilities - format order matters! LMS uses first matching format
            var capabilities = new Capabilities(false);
            capabilities.Add(new CapabilityValue(Capability.Model, "SqueezeNetCli"));
            capabilities.Add(new CapabilityValue(Capability.ModelName, "Cross-Platform Player"));
            capabilities.Add(Capability.AccuratePlayPoints);
            capabilities.Add(Capability.HasDigitalOut);
            // List native formats first (preferred), then transcoded PCM as fallback
            capabilities.Add(Capability.Flc); // FLAC - preferred for lossless
            capabilities.Add(Capability.Mp3); // MP3 - preferred for lossy
            capabilities.Add(Capability.Pcm); // PCM - fallback (requires server transcoding)
            capabilities.Add(new CapabilityValue(Capability.MaxSampleRate, "96000"));

            // Connect to server
            Console.WriteLine("Connecting to server...");
            Console.WriteLine($"Capabilities: {capabilities}");

            var macAddress = GetMacAddress();
            _client = new SlimClient();

            await _client.ConnectAsync(serverEndPoint, capabilities, macAddress);

            Console.WriteLine($"Connected and HELO sent!");
            Console.WriteLine($"MAC address: {BitConverter.ToString(macAddress)}");
            Console.WriteLine();

            // Initialize playback manager and message handler
            _playback = new PlaybackManager(_client);
            _messageHandler = new MessageHandler(_client, _playback);

            _cts = new CancellationTokenSource();

            // Start message processing loop
            await ProcessMessagesAsync(_cts.Token);
        }

        private static async Task ProcessMessagesAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Ready to receive commands from server.\n");

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("Waiting for message...");
                    ServerMessage? message;
                    try
                    {
                        message = await _client!.ReceiveAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error receiving message: {ex.GetType().Name} - {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"  Inner: {ex.InnerException.Message}");
                        }
                        throw;
                    }

                    if (message == null)
                    {
                        Console.WriteLine("Connection closed by server.");
                        break;
                    }

                    Console.WriteLine($"Received: {message.GetType().Name}");
                    await _messageHandler!.HandleAsync(message);
                }
            }
            catch (EndOfStreamException)
            {
                Console.WriteLine("Connection closed by server.");
            }
            catch (IOException ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine($"Connection error: {ex.Message}");
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in message loop: {ex.GetType().Name} - {ex.Message}");
            }
        }

        private static void Cleanup()
        {
            _cts?.Cancel();
            _playback?.Dispose();
            _client?.Dispose();
        }

        private static async Task<Server?> DiscoverServerAsync()
        {
            var bindAddresses = new List<string> { "0.0.0.0" };
            bindAddresses.AddRange(GetLocalIPAddresses());

            Console.WriteLine($"Trying discovery on {bindAddresses.Count} interface(s): {string.Join(", ", bindAddresses)}");

            var tasks = bindAddresses.Select(TryDiscoverAsync).ToList();

            while (tasks.Count > 0)
            {
                var completed = await Task.WhenAny(tasks);
                var server = await completed;

                if (server != null)
                    return server;

                tasks.Remove(completed);
            }

            return null;
        }

        private static async Task<Server?> TryDiscoverAsync(string bindAddress)
        {
            try
            {
                var discovery = new ServerDiscovery();
                return await discovery.DiscoverAsync(bindAddress, TimeSpan.FromSeconds(5));
            }
            catch (SocketException)
            {
                return null;
            }
        }

        private static List<string> GetLocalIPAddresses()
        {
            var addresses = new List<string>();
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var iface in interfaces)
            {
                // Skip loopback, tunnel, and down interfaces
                if (iface.OperationalStatus != OperationalStatus.Up ||
                    iface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    iface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var properties = iface.GetIPProperties();
                foreach (var unicast in properties.UnicastAddresses)
                {
                    // Only include IPv4 addresses
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        addresses.Add(unicast.Address.ToString());
                    }
                }
            }

            return addresses;
        }

        private static void ShowUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  squeeze-net-cli [server:port]");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  server:port  Optional. Server address and port (default port: 3483)");
            Console.WriteLine("               Examples: 192.168.1.100:3483, myserver.local, 10.0.0.5");
            Console.WriteLine();
            Console.WriteLine("If no server is specified, UDP discovery will be used to find LMS on the network.");
        }

        private static byte[] GetMacAddress()
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            // Prefer physical, operational interfaces
            var validInterface = interfaces
                .Where(i => i.OperationalStatus == OperationalStatus.Up)
                .Where(i => i.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(i => i.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .FirstOrDefault();

            if (validInterface != null)
            {
                var physicalAddress = validInterface.GetPhysicalAddress();
                var bytes = physicalAddress.GetAddressBytes();
                if (bytes.Length == 6)
                {
                    return bytes;
                }
            }

            // Fallback MAC address if none found
            return [0x01, 0x02, 0x03, 0x04, 0x05, 0x06];
        }

        private static IPEndPoint? ParseServerAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return null;

            string host;
            int port = Constants.SLIM_PORT;

            // Check if port is specified
            var parts = address.Split(':');
            if (parts.Length == 2)
            {
                host = parts[0];
                if (!int.TryParse(parts[1], out port) || port <= 0 || port > 65535)
                {
                    return null;
                }
            }
            else if (parts.Length == 1)
            {
                host = parts[0];
            }
            else
            {
                return null;
            }

            try
            {
                // Try to resolve hostname
                var addresses = Dns.GetHostAddresses(host);
                if (addresses.Length == 0)
                    return null;

                // Prefer IPv4 addresses
                var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                var ip = ipv4 ?? addresses[0];

                return new IPEndPoint(ip, port);
            }
            catch
            {
                return null;
            }
        }
    }
}
