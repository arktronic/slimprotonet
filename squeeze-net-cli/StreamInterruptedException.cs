using System;

namespace SqueezeNetCli
{
    /// <summary>
    /// Exception thrown when an audio stream is interrupted before sufficient data 
    /// can be buffered (typically during rapid seeking).
    /// </summary>
    public class StreamInterruptedException : Exception
    {
        public int BytesBuffered { get; }

        public StreamInterruptedException(int bytesBuffered)
            : base($"Stream interrupted - only {bytesBuffered} bytes buffered before connection closed")
        {
            BytesBuffered = bytesBuffered;
        }

        public StreamInterruptedException(int bytesBuffered, Exception innerException)
            : base($"Stream interrupted - only {bytesBuffered} bytes buffered before connection closed", innerException)
        {
            BytesBuffered = bytesBuffered;
        }
    }
}
