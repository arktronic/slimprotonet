using System.Net;
using SlimProtoNet.Client;
using SlimProtoNet.Protocol.Messages;

namespace SqueezeNetCli
{
    /// <summary>
    /// Manages audio playback lifecycle and status reporting.
    /// </summary>
    public class PlaybackManager
    {
        private readonly MiniAudioPlayer _player = new MiniAudioPlayer();
        private readonly StatusData _status = new StatusData();
        private Timer? _statusTimer;
        private readonly SlimClient _client;

        public PlaybackManager(SlimClient client)
        {
            _client = client;
        }

        public StatusData Status => _status;

        public async Task HandleStreamAsync(StreamMessage stream)
        {
            try
            {
                // Stop any existing playback
                Stop();

                // Check format support (PCM, MP3, FLAC only)
                if (stream.Format != Format.Pcm && stream.Format != Format.Mp3 && stream.Format != Format.Flac)
                {
                    Console.WriteLine($"ERROR: Unsupported format '{stream.Format}' - only PCM, MP3, and FLAC are supported");
                    Console.WriteLine($"   Sending STMn (Not Supported) status to server...");

                    _status.ErrorCode = 1;
                    var notSupportedMsg = _status.CreateStatusMessage(StatusCode.NotSupported);
                    await _client.SendAsync(notSupportedMsg);

                    Console.WriteLine($"   Server should fall back to a supported format.");
                    return;
                }

                Console.WriteLine($"Stream format: {stream.Format}");

                // For PCM, log stream parameters
                if (stream.Format == Format.Pcm)
                {
                    var sampleRate = Helpers.GetSampleRate(stream.PcmSampleRate);
                    var channels = Helpers.GetChannels(stream.PcmChannels);
                    var bitsPerSample = Helpers.GetBitsPerSample(stream.PcmSampleSize);
                    Console.WriteLine($"PCM: {sampleRate} Hz, {channels} ch, {bitsPerSample} bit");
                }

                // Connect to audio stream
                var serverIp = stream.ServerIp.Equals(IPAddress.Any)
                    ? _client.ServerEndPoint?.Address ?? stream.ServerIp
                    : stream.ServerIp;

                Console.WriteLine($"Connecting to audio stream at {serverIp}:{stream.ServerPort}");

                await _player.ConnectAsync(serverIp, stream.ServerPort, stream.HttpHeaders ?? string.Empty);

                if (!string.IsNullOrEmpty(stream.HttpHeaders))
                {
                    // Track CRLF count for status reporting
                    var crlfCount = 2;
                    _status.AddCrlf((byte)crlfCount);
                }

                // Notify server we're connected
                var connectMsg = _status.CreateStatusMessage(StatusCode.Connect);
                await _client.SendAsync(connectMsg);

                // Setup audio decoder for the format
                await _player.SetupAsync(stream);

                // Set up callback for playback completion events
                _player.SetStatusCallback(async (statusCode) =>
                {
                    Console.WriteLine($"Playback event: {statusCode} ({statusCode.ToEventCode()})");
                    var statusMsg = _status.CreateStatusMessage(statusCode);
                    await _client.SendAsync(statusMsg);
                });

                // Notify server that stream is established
                var establishedMsg = _status.CreateStatusMessage(StatusCode.StreamEstablished);
                await _client.SendAsync(establishedMsg);

                // Start playback if autostart enabled
                if (stream.AutoStart != AutoStart.None)
                {
                    _player.Play();

                    var startedMsg = _status.CreateStatusMessage(StatusCode.TrackStarted);
                    await _client.SendAsync(startedMsg);
                }
            }
            catch (StreamInterruptedException ex)
            {
                Console.WriteLine($"Stream interrupted: {ex.Message}");
                Console.WriteLine($"   Likely rapid seeking - waiting for new stream request...");
                // Don't send error status - server is already sending new stream
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling stream: {ex.Message}");
                Console.WriteLine($"   Stack trace: {ex.StackTrace}");
                Console.WriteLine($"   Sending STMn (Not Supported) status to server...");

                _status.ErrorCode = 2;
                var errorMsg = _status.CreateStatusMessage(StatusCode.NotSupported);
                await _client.SendAsync(errorMsg);
            }
        }

        public void StartStatusTimer(uint intervalMilliseconds)
        {
            _statusTimer?.Dispose();

            if (intervalMilliseconds > 0)
            {
                _statusTimer = new Timer(async _ =>
                {
                    try
                    {
                        _player.UpdateStatus(_status);
                        var msg = _status.CreateStatusMessage(StatusCode.Timer);
                        await _client.SendAsync(msg);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending status: {ex.Message}");
                    }
                }, null, TimeSpan.FromMilliseconds(intervalMilliseconds),
                TimeSpan.FromMilliseconds(intervalMilliseconds));
            }
        }

        public void Stop()
        {
            _player.Stop();
        }

        public void Pause()
        {
            if (_player.IsSetUp())
            {
                _player.Pause();
            }
        }

        public void Resume()
        {
            if (_player.IsPaused())
            {
                _player.Play();
            }
        }

        public void UpdateStatus()
        {
            _player.UpdateStatus(_status);
        }

        public bool IsSetUp() => _player.IsSetUp();

        public void SetVolume(float leftGain, float rightGain)
        {
            _player.SetVolume(leftGain, rightGain);
        }

        public void Dispose()
        {
            _statusTimer?.Dispose();
            Stop();
        }
    }
}
