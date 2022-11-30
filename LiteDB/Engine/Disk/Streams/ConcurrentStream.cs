using System;
using System.IO;

namespace LiteDB.Engine
{
    /// <summary>
    /// Implement internal thread-safe Stream using lock control - A single instance of ConcurrentStream are not multi thread,
    /// but multiples ConcurrentStream instances using same stream base will support concurrency
    /// </summary>
    internal class ConcurrentStream : Stream
    {
        private readonly Stream _stream;

        public ConcurrentStream(Stream stream)
        {
            _stream = stream;
        }

        public override bool CanRead => _stream.CanRead;

        public override bool CanSeek => _stream.CanSeek;

        public override bool CanWrite => _stream.CanWrite;

        public override long Length => _stream.Length;

        public override long Position { get; set; }

        public override void Flush() => _stream.Flush();

        public override void SetLength(long value) => _stream.SetLength(value);

        protected override void Dispose(bool disposing) => _stream.Dispose();

        public override long Seek(long offset, SeekOrigin origin)
        {
            lock (_stream)
            {
                var position =
                    origin == SeekOrigin.Begin ? offset :
                    origin == SeekOrigin.Current ? Position + offset :
                    Position - offset;

                Position = position;

                return Position;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // lock internal stream and set position before read
            lock (_stream)
            {
                _stream.Position = Position;
                var read = _stream.Read(buffer, offset, count);
                Position = _stream.Position;
                return read;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!CanWrite) throw new NotSupportedException("Current stream are readonly");

            // lock internal stream and set position before write
            lock (_stream)
            {
                _stream.Position = Position;
                _stream.Write(buffer, offset, count);
                Position = _stream.Position;
            }
        }
    }
}