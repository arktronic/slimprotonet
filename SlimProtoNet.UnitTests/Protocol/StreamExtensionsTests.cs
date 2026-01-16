using SlimProtoNet.Protocol;

namespace SlimProtoNet.UnitTests.Protocol
{
    [TestClass]
    public class StreamExtensionsTests
    {
        [TestMethod]
        public async Task ReadFrameAsyncShouldReturnPayloadWhenSingleCompleteFrame()
        {
            // [length=5][H][E][L][L][O]
            byte[] data = new byte[] { 0x00, 0x05, 0x48, 0x45, 0x4C, 0x4C, 0x4F };
            using var stream = new MemoryStream(data);

            byte[] result = await stream.ReadFrameAsync();

            Assert.HasCount(5, result);
            CollectionAssert.AreEqual(new byte[] { 0x48, 0x45, 0x4C, 0x4C, 0x4F }, result);
        }

        [TestMethod]
        public async Task ReadFrameAsyncShouldReturnEmptyArrayWhenFrameIsEmpty()
        {
            // [length=0]
            byte[] data = new byte[] { 0x00, 0x00 };
            using var stream = new MemoryStream(data);

            byte[] result = await stream.ReadFrameAsync();

            Assert.IsEmpty(result);
        }

        [TestMethod]
        public async Task ReadFrameAsyncShouldReadSequentiallyWhenMultipleFrames()
        {
            // [len=3][ABC][len=2][XY]
            byte[] data = new byte[] { 0x00, 0x03, 0x41, 0x42, 0x43, 0x00, 0x02, 0x58, 0x59 };
            using var stream = new MemoryStream(data);

            byte[] frame1 = await stream.ReadFrameAsync();
            byte[] frame2 = await stream.ReadFrameAsync();

            CollectionAssert.AreEqual(new byte[] { 0x41, 0x42, 0x43 }, frame1);
            CollectionAssert.AreEqual(new byte[] { 0x58, 0x59 }, frame2);
        }

        [TestMethod]
        public async Task ReadFrameAsyncShouldHandleCorrectlyWhenFrameIsLarge()
        {
            // Create a large frame (1000 bytes)
            byte[] payload = new byte[1000];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(i % 256);
            }

            byte[] data = new byte[1002];
            data[0] = 0x03; // Length high byte (1000 = 0x03E8)
            data[1] = 0xE8; // Length low byte
            Array.Copy(payload, 0, data, 2, 1000);

            using var stream = new MemoryStream(data);

            byte[] result = await stream.ReadFrameAsync();

            Assert.HasCount(1000, result);
            CollectionAssert.AreEqual(payload, result);
        }

        [TestMethod]
        public async Task ReadFrameAsyncShouldReassembleCorrectlyWhenPartialReads()
        {
            // Use a stream that reads one byte at a time
            byte[] data = new byte[] { 0x00, 0x04, 0x54, 0x45, 0x53, 0x54 };
            using var stream = new SlowReadStream(data, bytesPerRead: 1);

            byte[] result = await stream.ReadFrameAsync();

            Assert.HasCount(4, result);
            CollectionAssert.AreEqual(new byte[] { 0x54, 0x45, 0x53, 0x54 }, result);
        }

        [TestMethod]
        public async Task ReadFrameAsyncShouldSucceedWhenFrameIsMaxSize()
        {
            // Maximum frame size (65535 bytes)
            byte[] payload = new byte[65535];
            byte[] data = new byte[65537];
            data[0] = 0xFF;
            data[1] = 0xFF;
            Array.Copy(payload, 0, data, 2, 65535);

            using var stream = new MemoryStream(data);

            byte[] result = await stream.ReadFrameAsync();

            Assert.HasCount(65535, result);
        }

        [TestMethod]
        public async Task ReadFrameAsyncShouldThrowEndOfStreamExceptionWhenConnectionClosedDuringLength()
        {
            // Only one byte available (incomplete length)
            byte[] data = new byte[] { 0x00 };
            using var stream = new MemoryStream(data);

            await Assert.ThrowsAsync<EndOfStreamException>(async () => await stream.ReadFrameAsync());
        }

        [TestMethod]
        public async Task ReadFrameAsyncShouldThrowEndOfStreamExceptionWhenConnectionClosedDuringPayload()
        {
            // Length says 5 bytes but only 3 available
            byte[] data = new byte[] { 0x00, 0x05, 0x41, 0x42, 0x43 };
            using var stream = new MemoryStream(data);

            await Assert.ThrowsAsync<EndOfStreamException>(async () => await stream.ReadFrameAsync());
        }

        /// <summary>
        /// Helper stream that simulates slow network reads
        /// </summary>
        private class SlowReadStream : Stream
        {
            private readonly byte[] _data;
            private readonly int _bytesPerRead;
            private int _position;

            public SlowReadStream(byte[] data, int bytesPerRead)
            {
                _data = data;
                _bytesPerRead = bytesPerRead;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _data.Length;
            public override long Position { get => _position; set => throw new NotSupportedException(); }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_position >= _data.Length)
                    return 0;

                int toRead = Math.Min(Math.Min(count, _bytesPerRead), _data.Length - _position);
                Array.Copy(_data, _position, buffer, offset, toRead);
                _position += toRead;
                return toRead;
            }

            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
