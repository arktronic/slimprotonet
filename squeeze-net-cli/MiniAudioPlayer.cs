using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using MiniAudioEx.Core.StandardAPI;
using MiniAudioEx.Core.AdvancedAPI;
using MiniAudioEx.Native;
using SlimProtoNet.Client;
using SlimProtoNet.Protocol.Messages;

namespace SqueezeNetCli
{
    /// <summary>
    /// Audio playback engine using MiniAudioExNET.
    /// Handles network streaming, format decoding, and audio output.
    /// </summary>
    public class MiniAudioPlayer
    {
        private const int CHUNK_SIZE = 8192;
        private const int BUFFER_SIZE = 262144; // 256KB circular buffer
        private const int INIT_BUFFER_SIZE = 131072; // 128KB initialization buffer

        // Audio components
        private AudioSource? _audioSource;
        private MaDecoder? _decoder;

        // Network components
        private TcpClient? _audioStream;
        private NetworkStream? _audioNetworkStream;

        // Buffer management
        private CircularBuffer? _streamBuffer;
        private byte[]? _initBuffer;
        private int _initBufferSize;
        private int _initBufferPosition;

        // Background tasks
        private Task? _bufferFillTask;
        private Task? _playbackMonitoringTask;
        private CancellationTokenSource? _cancellationTokenSource;

        // State tracking
        private bool _isSetUp;
        private bool _isPaused;
        private bool _bufferFillStarted;
        private bool _endOfStream;
        private long _totalBytesReadFromNetwork;
        private bool _isPcmMode; // True for raw PCM, false for compressed formats
        private bool _decoderFinished; // Set when decoder returns 0 frames
        private ulong _totalDecodedFrames; // Total PCM frames decoded
        private readonly object _decoderLock = new object(); // Lock for decoder state

        // Audio format info
        private uint _sampleRate;
        private uint _channels;
        
        // Volume control (0.0 to 1.0, separate for left/right channels)
        private float _leftGain = 1.0f;
        private float _rightGain = 1.0f;
        
        // Playback completion callback
        private Func<StatusCode, Task>? _statusCallback;

        // Decoder callbacks - stored as fields to prevent GC collection
        private ma_decoder_read_proc? _decoderReadProc;
        private ma_decoder_seek_proc? _decoderSeekProc;

        /// <summary>
        /// Decoder read callback - called by MiniAudio when it needs more compressed audio data.
        /// Reads from init buffer during initialization, then from StreamBuffer after buffering starts.
        /// </summary>
        private ma_result DecoderReadCallback(ma_decoder_ptr pDecoder, IntPtr pBufferOut, size_t bytesToRead, out size_t pBytesRead)
        {
            pBytesRead = new size_t(0);

            try
            {
                UIntPtr bytesToReadPtr = bytesToRead;
                var requestedBytes = (int)bytesToReadPtr.ToUInt64();
                var tempBuffer = new byte[requestedBytes];

                var totalBytesRead = 0;

                // During initialization, read from init buffer
                if (!_bufferFillStarted && _initBuffer != null)
                {
                    var availableBytes = _initBufferSize - _initBufferPosition;
                    var bytesToCopy = Math.Min(requestedBytes, availableBytes);

                    if (bytesToCopy > 0)
                    {
                        Array.Copy(_initBuffer, _initBufferPosition, tempBuffer, 0, bytesToCopy);
                        _initBufferPosition += bytesToCopy;
                        totalBytesRead = bytesToCopy;
                    }
                }
                else if (_streamBuffer != null)
                {
                    // After buffering started, read from circular buffer with retry logic
                    var maxRetries = 200;
                    var retries = 0;

                    while (totalBytesRead < requestedBytes && retries < maxRetries)
                    {
                        var bytesRead = _streamBuffer.Read(tempBuffer, totalBytesRead, requestedBytes - totalBytesRead);

                        if (bytesRead == 0)
                        {
                            if (_endOfStream)
                                break;

                            Thread.Sleep(10);
                            retries++;
                            continue;
                        }

                        totalBytesRead += bytesRead;
                        retries = 0;
                    }
                }

                if (totalBytesRead > 0)
                {
                    Marshal.Copy(tempBuffer, 0, pBufferOut, totalBytesRead);
                    pBytesRead = (size_t)totalBytesRead;
                    return ma_result.success;
                }
                else
                {
                    // No more data available - signal end of stream to decoder
                    pBytesRead = new size_t(0);
                    return _endOfStream && (_streamBuffer?.AvailableBytes ?? 0) == 0 
                        ? ma_result.at_end 
                        : ma_result.success;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in decoder read callback: {ex.Message}");
                return ma_result.error;
            }
        }

        /// <summary>
        /// Decoder seek callback - supports seeking within init buffer only.
        /// Streaming data from network cannot be seeked.
        /// </summary>
        private ma_result DecoderSeekCallback(ma_decoder_ptr pDecoder, long byteOffset, ma_seek_origin origin)
        {
            // Only support seeking in init buffer before streaming starts
            if (!_bufferFillStarted && _initBuffer != null)
            {
                try
                {
                    var newPosition = origin switch
                    {
                        ma_seek_origin.start => (int)byteOffset,
                        ma_seek_origin.current => _initBufferPosition + (int)byteOffset,
                        _ => (int)byteOffset
                    };

                    if (newPosition < 0 || newPosition > _initBufferSize)
                    {
                        return ma_result.error;
                    }

                    _initBufferPosition = newPosition;
                    return ma_result.success;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in decoder seek callback: {ex.Message}");
                    return ma_result.error;
                }
            }

            // Seeking not supported for streaming data
            return ma_result.error;
        }

        public async Task SetupAsync(StreamMessage stream)
        {
            Console.WriteLine($"SetupAsync: Initializing decoder for {stream.Format}");

            // For raw PCM, we don't use the decoder - just pass samples directly
            if (stream.Format == Format.Pcm)
            {
                _isPcmMode = true;
                _sampleRate = (uint)Helpers.GetSampleRate(stream.PcmSampleRate);
                _channels = (uint)Helpers.GetChannels(stream.PcmChannels);
                
                Console.WriteLine($"PCM mode: {_sampleRate} Hz, {_channels} channels");
                
                // Move init buffer to stream buffer
                if (_initBuffer != null && _initBufferSize > 0)
                {
                    _streamBuffer!.Write(_initBuffer, 0, _initBufferSize);
                    Console.WriteLine($"Moved {_initBufferSize} bytes from init buffer to stream buffer");
                }
                _initBuffer = null;
                
                // Start background task to fill buffer from network
                StartBufferFillTask();
                
                // Wait for buffer to fill up a bit
                Console.WriteLine("Waiting for buffer to fill...");
                var targetBytes = BUFFER_SIZE / 2;
                var waitCount = 0;
                while (_streamBuffer!.AvailableBytes < targetBytes && !_endOfStream && waitCount < 100)
                {
                    await Task.Delay(50);
                    waitCount++;
                }
                Console.WriteLine($"Buffer ready: {_streamBuffer.AvailableBytes} bytes buffered");
            }
            else
            {
                // For compressed formats (MP3, FLAC), use decoder
                _isPcmMode = false;
                
                // Create decoder with our custom read/seek callbacks
                _decoder = new MaDecoder();

                // Configure decoder for the expected format
                var config = MiniAudioNative.ma_decoder_config_init(ma_format.f32, 2, 44100);

                // Store callback delegates as fields to prevent GC collection
                _decoderReadProc = DecoderReadCallback;
                _decoderSeekProc = DecoderSeekCallback;

                // Initialize decoder with callbacks
                var result = MiniAudioNative.ma_decoder_init(
                    _decoderReadProc,
                    _decoderSeekProc,
                    IntPtr.Zero,
                    ref config,
                    _decoder.Handle);

                if (result != ma_result.success)
                {
                    Console.WriteLine($"Failed to initialize decoder: {result}");
                    throw new InvalidOperationException($"Failed to initialize decoder: {result}");
                }

                // Get actual format detected by decoder
                result = _decoder.GetDataFormat(
                    out ma_format format,
                    out _channels,
                    out _sampleRate,
                    new ma_channel_ptr(IntPtr.Zero),
                    0);

                if (result == ma_result.success)
                {
                    Console.WriteLine($"Detected format: {format}, Channels: {_channels}, Sample Rate: {_sampleRate} Hz");
                }

                // Move any unused init buffer data to stream buffer
                if (_initBufferPosition < _initBufferSize)
                {
                    var remainingBytes = _initBufferSize - _initBufferPosition;
                    _streamBuffer!.Write(_initBuffer!, _initBufferPosition, remainingBytes);
                    Console.WriteLine($"Moved {remainingBytes} unused bytes from init buffer to stream buffer");
                }

                // Clear init buffer - we're done with it
                _initBuffer = null;

                // Start background task to fill buffer from network
                StartBufferFillTask();

                // Wait for buffer to fill up a bit before starting playback
                Console.WriteLine("Waiting for buffer to fill...");
                var targetBytes = BUFFER_SIZE / 2;
                var waitCount = 0;
                while (_streamBuffer!.AvailableBytes < targetBytes && !_endOfStream && waitCount < 100)
                {
                    await Task.Delay(50);
                    waitCount++;
                }
                Console.WriteLine($"Buffer ready: {_streamBuffer.AvailableBytes} bytes buffered");
            }

            _isSetUp = true;
        }

        public void SetStatusCallback(Func<StatusCode, Task> callback)
        {
            _statusCallback = callback;
        }

        public void SetVolume(float leftGain, float rightGain)
        {
            _leftGain = Math.Clamp(leftGain, 0.0f, 1.0f);
            _rightGain = Math.Clamp(rightGain, 0.0f, 1.0f);
            Console.WriteLine($"Volume set: L={_leftGain:F2} R={_rightGain:F2}");
        }

        public async Task ConnectAsync(IPAddress serverIp, ushort port, string httpHeaders)
        {
            Console.WriteLine($"ConnectAsync: Connecting to {serverIp}:{port}");

            // Create TCP connection
            _audioStream = new TcpClient();
            await _audioStream.ConnectAsync(serverIp, port);
            _audioNetworkStream = _audioStream.GetStream();

            // Send HTTP headers if provided
            if (!string.IsNullOrEmpty(httpHeaders))
            {
                var headerBytes = System.Text.Encoding.ASCII.GetBytes(httpHeaders);
                await _audioNetworkStream.WriteAsync(headerBytes, 0, headerBytes.Length);
                await _audioNetworkStream.FlushAsync();

                // Read and discard HTTP response headers
                await ReadHttpResponseHeadersAsync(_audioNetworkStream);
            }

            // Initialize stream buffer
            _streamBuffer = new CircularBuffer(BUFFER_SIZE);

            // Buffer initial data before decoder setup
            Console.WriteLine("Buffering initial data...");
            _initBuffer = new byte[INIT_BUFFER_SIZE];
            _initBufferSize = 0;
            _initBufferPosition = 0;

            using var initBufferFillCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // Fill init buffer
            while (_initBufferSize < INIT_BUFFER_SIZE)
            {
                int bytesRead = await _audioNetworkStream.ReadAsync(_initBuffer, _initBufferSize, INIT_BUFFER_SIZE - _initBufferSize, initBufferFillCts.Token);
                if (bytesRead == 0)
                    break;
                _initBufferSize += bytesRead;
                _totalBytesReadFromNetwork += bytesRead;
            }

            Console.WriteLine($"Buffered {_initBufferSize} bytes for format detection");
            
            // Verify we have enough data for format detection
            if (_initBufferSize < 4096) // Minimum bytes needed for format detection
            {
                throw new StreamInterruptedException(_initBufferSize);
            }
        }

        /// <summary>
        /// Reads and discards HTTP response headers from the stream.
        /// </summary>
        private static async Task ReadHttpResponseHeadersAsync(NetworkStream stream)
        {
            // Read HTTP response headers line by line until we hit a blank line
            using var reader = new StreamReader(stream, System.Text.Encoding.ASCII, false, 1024, true);
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    // Empty line marks end of headers
                    break;
                }
            }
        }

        /// <summary>
        /// Starts background task to fill stream buffer from network.
        /// </summary>
        private void StartBufferFillTask()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            _bufferFillTask = Task.Run(async () =>
            {
                try
                {
                    _bufferFillStarted = true;
                    var chunkBuffer = new byte[CHUNK_SIZE];

                    while (!token.IsCancellationRequested && !_endOfStream && _audioNetworkStream != null)
                    {
                        // Only read if there's space in the buffer
                        if (_streamBuffer!.FreeSpace >= CHUNK_SIZE)
                        {
                            var bytesRead = await _audioNetworkStream.ReadAsync(chunkBuffer, 0, CHUNK_SIZE, token);

                            if (bytesRead == 0)
                            {
                                _endOfStream = true;
                                Console.WriteLine($"End of network stream. Total bytes read: {_totalBytesReadFromNetwork}");
                                break;
                            }

                            var bytesWritten = _streamBuffer.Write(chunkBuffer, 0, bytesRead);
                            _totalBytesReadFromNetwork += bytesWritten;
                        }
                        else
                        {
                            // Buffer full, wait a bit
                            await Task.Delay(10, token);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal cancellation
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in buffer fill task: {ex.Message}");
                    _endOfStream = true;
                }
            }, token);
        }

        public void Play()
        {
            if (!_isSetUp)
            {
                Console.WriteLine("Play: Cannot play - player not set up");
                return;
            }

            Console.WriteLine("Play: Starting playback");

            // Create audio source if it doesn't exist
            if (_audioSource == null)
            {
                _audioSource = new AudioSource();

                if (_isPcmMode)
                {
                    // PCM mode - read raw bytes from buffer and convert to float samples
                    _audioSource.Read += (NativeArray<float> framesOut, ulong frameCount, int channels) =>
                    {
                        if (_endOfStream && (_streamBuffer?.AvailableBytes ?? 0) == 0)
                        {
                            // Zero-fill on end of stream
                            for (var i = 0; i < framesOut.Length; i++)
                                framesOut[i] = 0.0f;
                            return;
                        }

                        // Calculate how many bytes we need (assuming 16-bit samples)
                        var samplesNeeded = (int)(frameCount * (ulong)channels);
                        var bytesNeeded = samplesNeeded * 2; // 16-bit = 2 bytes per sample
                        var buffer = new byte[bytesNeeded];

                        var bytesRead = _streamBuffer?.Read(buffer, 0, bytesNeeded) ?? 0;
                        var samplesRead = bytesRead / 2;

                        // Convert 16-bit PCM to float (-1.0 to 1.0) and apply volume
                        for (var i = 0; i < samplesRead; i++)
                        {
                            var sample = (short)(buffer[i * 2] | (buffer[i * 2 + 1] << 8));
                            var floatSample = sample / 32768.0f;
                            
                            // Apply gain per channel (assuming stereo interleaved: L, R, L, R, ...)
                            var gain = (i % 2 == 0) ? _leftGain : _rightGain;
                            framesOut[i] = floatSample * gain;
                        }

                        // Zero-fill any remaining samples
                        for (var i = samplesRead; i < framesOut.Length; i++)
                        {
                            framesOut[i] = 0.0f;
                        }
                    };
                }
                else
                {
                    // Compressed format mode - use decoder
                    _audioSource.Read += (NativeArray<float> framesOut, ulong frameCount, int channels) =>
                    {
                        if (_decoder == null)
                        {
                            // Zero-fill if no decoder
                            for (var i = 0; i < framesOut.Length; i++)
                                framesOut[i] = 0.0f;
                            return;
                        }

                        // Call decoder to read PCM frames
                        ulong framesReadCount = 0;
                        
                        // We need to get the actual frames read - use a wrapper approach
                        // The decoder fills the buffer and we can detect by checking for non-zero data
                        MiniAudioNative.ma_decoder_read_pcm_frames(
                            _decoder.Handle,
                            framesOut.Pointer,
                            frameCount,
                            IntPtr.Zero);

                        // Count non-zero frames to determine how many were actually decoded
                        var totalSamples = (int)(frameCount * (ulong)channels);
                        var nonZeroSamples = 0;
                        for (var i = 0; i < totalSamples && i < framesOut.Length; i++)
                        {
                            if (Math.Abs(framesOut[i]) > 0.0001f)
                                nonZeroSamples = i + 1;
                        }
                        
                        framesReadCount = (ulong)(nonZeroSamples / channels);

                        // Track total decoded frames
                        lock (_decoderLock)
                        {
                            _totalDecodedFrames += framesReadCount;
                        }

                        // If decoder returned 0 frames, it's finished
                        if (framesReadCount == 0 && _endOfStream)
                        {
                            lock (_decoderLock)
                            {
                                if (!_decoderFinished)
                                {
                                    _decoderFinished = true;
                                    Console.WriteLine($"Decoder finished. Total frames decoded: {_totalDecodedFrames}");
                                }
                            }
                            
                            // Zero-fill to prevent noise
                            for (var i = 0; i < framesOut.Length; i++)
                                framesOut[i] = 0.0f;
                            return;
                        }

                        // Apply volume gain to all frames
                        for (var i = 0; i < totalSamples && i < framesOut.Length; i++)
                        {
                            var gain = (i % 2 == 0) ? _leftGain : _rightGain;
                            framesOut[i] *= gain;
                        }
                    };
                }
            }

            _audioSource.Play();
            _isPaused = false;

            // Start monitoring task for end of track detection
            StartPlaybackMonitoring();
        }

        /// <summary>
        /// Monitors playback to detect when track completes and send appropriate status codes.
        /// </summary>
        private void StartPlaybackMonitoring()
        {
            // Cancel previous monitoring task if still running
            _playbackMonitoringTask?.Wait(100);
            
            // Reuse the same cancellation token as buffer fill task
            var token = _cancellationTokenSource?.Token ?? CancellationToken.None;
            
            _playbackMonitoringTask = Task.Run(async () =>
            {
                try
                {
                    // Wait for end of stream from network
                    while (!_endOfStream && _bufferFillTask != null && !_bufferFillTask.IsCompleted && !token.IsCancellationRequested)
                    {
                        await Task.Delay(100, token);
                    }

                    if (_endOfStream && _statusCallback != null && !token.IsCancellationRequested)
                    {
                        Console.WriteLine("Network stream ended - sending STMd (DecoderReady)");
                        await _statusCallback(StatusCode.DecoderReady);

                        // Wait for decoder to actually finish decoding all buffered data
                        Console.WriteLine("Waiting for decoder to finish...");
                        var waitCount = 0;
                        while (!_decoderFinished && waitCount < 100 && !token.IsCancellationRequested)
                        {
                            await Task.Delay(100, token);
                            waitCount++;
                        }
                        
                        if (_decoderFinished && !token.IsCancellationRequested)
                        {
                            ulong targetFrames;
                            lock (_decoderLock)
                            {
                                targetFrames = _totalDecodedFrames;
                            }
                            
                            Console.WriteLine($"Decoder finished. Waiting for cursor to reach {targetFrames} frames...");
                            
                            // Wait for audio device cursor to catch up to decoded frames
                            waitCount = 0;
                            while (_audioSource != null && waitCount < 100 && !token.IsCancellationRequested)
                            {
                                var currentCursor = _audioSource.Cursor;
                                Console.WriteLine($"Current cursor: {currentCursor}, target: {targetFrames}");
                                
                                if (currentCursor >= targetFrames)
                                {
                                    Console.WriteLine("Playback reached final decoded frame");
                                    break;
                                }
                                
                                await Task.Delay(50, token);
                                waitCount++;
                            }
                            
                            // Small additional delay for audio device buffer
                            if (!token.IsCancellationRequested)
                                await Task.Delay(100, token);
                        }
                        else if (!token.IsCancellationRequested)
                        {
                            Console.WriteLine("Decoder did not finish in time, using fallback delay");
                            await Task.Delay(1000, token);
                        }

                        if (!token.IsCancellationRequested)
                        {
                            Console.WriteLine("Playback complete - sending STMu (Underrun)");
                            await _statusCallback(StatusCode.Underrun);
                            
                            // Stop audio source to prevent buzzing from continued decoder reads
                            if (_audioSource != null)
                            {
                                _audioSource.Stop();
                                Console.WriteLine("Audio source stopped");
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal cancellation - stream was stopped/restarted
                    Console.WriteLine("Playback monitoring cancelled");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in playback monitoring: {ex.Message}");
                }
            }, token);
        }

        public void Pause()
        {
            if (!_isSetUp || _audioSource == null)
            {
                Console.WriteLine("Pause: Cannot pause - player not set up");
                return;
            }

            Console.WriteLine("Pause: Pausing playback");
            _audioSource.Stop(); // MiniAudio doesn't have pause, so we stop
            _isPaused = true;
        }

        public void Stop()
        {
            Console.WriteLine("Stop: Stopping playback");

            // Stop audio source first
            if (_audioSource != null)
            {
                try
                {
                    _audioSource.Stop();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error stopping audio source: {ex.Message}");
                }
            }

            // Cancel background tasks
            _cancellationTokenSource?.Cancel();
            
            if (_bufferFillTask != null)
            {
                try
                {
                    _bufferFillTask.Wait(TimeSpan.FromSeconds(2));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error waiting for buffer fill task: {ex.Message}");
                }
            }
            
            if (_playbackMonitoringTask != null)
            {
                try
                {
                    _playbackMonitoringTask.Wait(TimeSpan.FromSeconds(1));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error waiting for monitoring task: {ex.Message}");
                }
            }

            // Dispose audio components
            _audioSource?.Dispose();
            _audioSource = null;

            _decoder?.Dispose();
            _decoder = null;

            // Close network streams
            _audioNetworkStream?.Close();
            _audioNetworkStream?.Dispose();
            _audioNetworkStream = null;

            _audioStream?.Close();
            _audioStream?.Dispose();
            _audioStream = null;

            // Clear buffers
            _streamBuffer?.Clear();
            _streamBuffer = null;

            _initBuffer = null;

            // Reset state
            _isSetUp = false;
            _isPaused = false;
            _bufferFillStarted = false;
            _endOfStream = false;
            _totalBytesReadFromNetwork = 0;
            _decoderFinished = false;
            _totalDecodedFrames = 0;
        }

        public bool IsSetUp() => _isSetUp;

        public bool IsPaused() => _isPaused;

        public void UpdateStatus(StatusData status)
        {
            if (!_isSetUp)
            {
                // No playback active
                status.SetOutputBufferSize(0);
                status.SetOutputBufferFullness(0);
                status.SetElapsedSeconds(0);
                status.SetElapsedMilliseconds(0);
                return;
            }

            // Report buffer metrics
            if (_streamBuffer != null)
            {
                status.SetOutputBufferSize(BUFFER_SIZE);
                status.SetOutputBufferFullness((uint)_streamBuffer.AvailableBytes);
            }

            // Report bytes received from network
            status.AddBytesReceived((uint)(_totalBytesReadFromNetwork - (long)status.BytesReceived));

            // Calculate elapsed time from actual audio device playback position
            // This is the position the user is actually hearing, not the decoder position
            // which can be ahead due to buffering
            if (_sampleRate > 0 && _audioSource != null)
            {
                try
                {
                    // Get actual playback cursor in PCM frames from audio device
                    ulong cursorInFrames = _audioSource.Cursor;
                    
                    // Calculate elapsed time from frames
                    ulong elapsedMilliseconds = cursorInFrames * 1000 / _sampleRate;
                    status.SetElapsedMilliseconds((uint)elapsedMilliseconds);
                    status.SetElapsedSeconds((uint)(elapsedMilliseconds / 1000));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting audio source cursor: {ex.Message}");
                    status.SetElapsedMilliseconds(0);
                    status.SetElapsedSeconds(0);
                }
            }
            else
            {
                status.SetElapsedMilliseconds(0);
                status.SetElapsedSeconds(0);
            }
        }
    }
}
