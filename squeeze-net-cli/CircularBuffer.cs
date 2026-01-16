namespace SqueezeNetCli
{
    /// <summary>
    /// Thread-safe circular buffer for streaming audio data.
    /// Provides constant-time read/write operations with automatic wrapping.
    /// </summary>
    public class CircularBuffer
    {
        private readonly byte[] _buffer;
        private readonly int _capacity;
        private int _writePosition = 0;
        private int _readPosition = 0;
        private int _availableBytes = 0;
        private readonly object _lockObject = new object();

        public CircularBuffer(int capacity)
        {
            _capacity = capacity;
            _buffer = new byte[capacity];
        }

        public int AvailableBytes
        {
            get
            {
                lock (_lockObject)
                {
                    return _availableBytes;
                }
            }
        }

        public int FreeSpace
        {
            get
            {
                lock (_lockObject)
                {
                    return _capacity - _availableBytes;
                }
            }
        }

        public int Write(byte[] data, int offset, int count)
        {
            lock (_lockObject)
            {
                int bytesToWrite = Math.Min(count, _capacity - _availableBytes);
                if (bytesToWrite == 0)
                    return 0;

                int firstChunk = Math.Min(bytesToWrite, _capacity - _writePosition);
                Array.Copy(data, offset, _buffer, _writePosition, firstChunk);

                if (bytesToWrite > firstChunk)
                {
                    int secondChunk = bytesToWrite - firstChunk;
                    Array.Copy(data, offset + firstChunk, _buffer, 0, secondChunk);
                }

                _writePosition = (_writePosition + bytesToWrite) % _capacity;
                _availableBytes += bytesToWrite;

                return bytesToWrite;
            }
        }

        public int Read(byte[] destination, int offset, int count)
        {
            lock (_lockObject)
            {
                int bytesToRead = Math.Min(count, _availableBytes);
                if (bytesToRead == 0)
                    return 0;

                int firstChunk = Math.Min(bytesToRead, _capacity - _readPosition);
                Array.Copy(_buffer, _readPosition, destination, offset, firstChunk);

                if (bytesToRead > firstChunk)
                {
                    int secondChunk = bytesToRead - firstChunk;
                    Array.Copy(_buffer, 0, destination, offset + firstChunk, secondChunk);
                }

                _readPosition = (_readPosition + bytesToRead) % _capacity;
                _availableBytes -= bytesToRead;

                return bytesToRead;
            }
        }

        public void Clear()
        {
            lock (_lockObject)
            {
                _writePosition = 0;
                _readPosition = 0;
                _availableBytes = 0;
            }
        }
    }
}
