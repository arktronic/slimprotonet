using SlimProtoNet.Client;
using SlimProtoNet.Protocol.Messages;

namespace SqueezeNetCli
{
    /// <summary>
    /// Handles incoming SlimProto messages from the server.
    /// </summary>
    public class MessageHandler
    {
        private readonly SlimClient _client;
        private readonly PlaybackManager _playback;
        private string _clientName;

        public MessageHandler(SlimClient client, PlaybackManager playback, string initialName = "SqueezeNetCli")
        {
            _client = client;
            _playback = playback;
            _clientName = initialName;
        }

        public string ClientName => _clientName;

        public async Task HandleAsync(ServerMessage message)
        {
            switch (message)
            {
                case ServMessage serv:
                    Console.WriteLine($"Server announcement: {serv.IpAddress}");
                    break;

                case QueryNameMessage _:
                    Console.WriteLine("Server queried our name");
                    await _client.SendAsync(new SetNameMessage { Name = _clientName });
                    break;

                case SetNameRequestMessage setName:
                    _clientName = setName.Name;
                    Console.WriteLine($"Server set our name to: {_clientName}");
                    break;

                case StatusRequestMessage statusReq:
                    Console.WriteLine($"Status request - interval: {statusReq.Interval}");
                    // Interval of 0 means "send status now"
                    if (statusReq.Interval.TotalMilliseconds == 0)
                    {
                        _playback.UpdateStatus();
                        var statusMsg = _playback.Status.CreateStatusMessage(StatusCode.Timer);
                        await _client.SendAsync(statusMsg);
                        Console.WriteLine("Sent immediate status response");
                    }
                    else
                    {
                        _playback.StartStatusTimer((uint)statusReq.Interval.TotalMilliseconds);
                    }
                    break;

                case StreamMessage stream:
                    Console.WriteLine($"Stream request: {stream.Format} @ {stream.PcmSampleRate}Hz, {stream.PcmChannels}");
                    await _playback.HandleStreamAsync(stream);
                    break;

                case StopMessage _:
                    Console.WriteLine("Stop playback");
                    _playback.Stop();
                    var stopMsg = _playback.Status.CreateStatusMessage(StatusCode.DecoderReady);
                    await _client.SendAsync(stopMsg);
                    break;

                case FlushMessage _:
                    Console.WriteLine("Flush buffers");
                    _playback.Stop();
                    var flushMsg = _playback.Status.CreateStatusMessage(StatusCode.Flushed);
                    await _client.SendAsync(flushMsg);
                    break;

                case PauseMessage pause:
                    Console.WriteLine($"Pause: {pause.Timestamp}");
                    _playback.Pause();
                    if (_playback.IsSetUp())
                    {
                        var pauseMsg = _playback.Status.CreateStatusMessage(StatusCode.Pause);
                        await _client.SendAsync(pauseMsg);
                    }
                    break;

                case UnpauseMessage unpause:
                    Console.WriteLine($"Unpause: {unpause.Timestamp}");
                    _playback.Resume();
                    var resumeMsg = _playback.Status.CreateStatusMessage(StatusCode.Resume);
                    await _client.SendAsync(resumeMsg);
                    break;

                case GainMessage gain:
                    Console.WriteLine($"Volume: L={gain.LeftGain:F2} R={gain.RightGain:F2}");
                    _playback.SetVolume((float)gain.LeftGain, (float)gain.RightGain);
                    break;

                case VersMessage vers:
                    Console.WriteLine($"Server version: {vers.Version}");
                    break;

                case UnknownServerMessage unknown:
                    Console.WriteLine($"Unknown message: {unknown.Opcode}");
                    break;

                default:
                    Console.WriteLine($"Unhandled message: {message.GetType().Name}");
                    break;
            }
        }
    }
}
