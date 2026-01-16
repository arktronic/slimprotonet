using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SlimProtoNet.Protocol
{
    /// <summary>
    /// Extension methods for Stream to handle SlimProto framing.
    /// Server messages are length-prefixed with a 2-byte big-endian length.
    /// </summary>
    public static class StreamExtensions
    {
        private const int MaxFrameSize = 1024 * 1024; // 1 MB

        /// <summary>
        /// Reads one complete frame from the stream.
        /// Handles partial reads by waiting for more data.
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The frame payload bytes (without length prefix)</returns>
        public static async Task<byte[]> ReadFrameAsync(this Stream stream, CancellationToken cancellationToken = default)
        {
            var lengthBuffer = new byte[2];

            // Read 2-byte big-endian length prefix
            int totalRead = 0;
            while (totalRead < 2)
            {
                int bytesRead = await stream.ReadAsync(lengthBuffer, totalRead, 2 - totalRead, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException("Connection closed while reading frame length");
                }
                totalRead += bytesRead;
            }

            // Convert big-endian length to integer
            int frameLength = (lengthBuffer[0] << 8) | lengthBuffer[1];

            if (frameLength < 0 || frameLength > MaxFrameSize)
            {
                throw new InvalidDataException($"Invalid frame length: {frameLength}");
            }

            // Read payload
            var payload = new byte[frameLength];
            totalRead = 0;
            while (totalRead < frameLength)
            {
                int bytesRead = await stream.ReadAsync(payload, totalRead, frameLength - totalRead, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException("Connection closed while reading frame payload");
                }
                totalRead += bytesRead;
            }

            return payload;
        }
    }
}
