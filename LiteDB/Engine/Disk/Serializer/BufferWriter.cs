using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using static LiteDB.Constants;

namespace LiteDB.Engine
{
    /// <summary>
    /// Write data types/BSON data into byte[]. It's forward only and support multi buffer slice as source
    /// </summary>
    internal class BufferWriter : IDisposable
    {
        private readonly IEnumerator<BufferSlice> _source;

        private BufferSlice _current;
        private int _currentPosition = 0; // position in _current
        private int _position = 0; // global position

        private bool _isEOF = false;

        /// <summary>
        /// Current global cursor position
        /// </summary>
        public int Position => _position;

        /// <summary>
        /// Indicate position are at end of last source array segment
        /// </summary>
        public bool IsEOF => _isEOF;

        public BufferWriter(byte[] buffer)
            : this(new BufferSlice(buffer, 0, buffer.Length))
        {
        }

        public BufferWriter(BufferSlice buffer)
        {
            _source = null;

            _current = buffer;
        }

        public BufferWriter(IEnumerable<BufferSlice> source)
        {
            _source = source.GetEnumerator();

            _source.MoveNext();
            _current = _source.Current;
        }

        #region Basic Write

        /// <summary>
        /// Move forward in current segment. If array segment finish, open next segment
        /// Returns true if move to another segment - returns false if continue in same segment
        /// </summary>
        private bool MoveForward(int count)
        {
            // do not move forward if source finish
            if (_isEOF) return false;

            ENSURE(_currentPosition + count <= _current.Count, "forward is only for current segment");

            _currentPosition += count;
            _position += count;

            // request new source array if _current all consumed
            if (_currentPosition == _current.Count)
            {
                if (_source == null || _source.MoveNext() == false)
                {
                    _isEOF = true;
                }
                else
                {
                    _current = _source.Current;
                    _currentPosition = 0;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Write bytes from buffer into segmentsr. Return how many bytes was write
        /// </summary>
        public int Write(byte[] buffer, int offset, int count)
        {
            var bufferPosition = 0;

            while (bufferPosition < count)
            {
                var bytesLeft = _current.Count - _currentPosition;
                var bytesToCopy = Math.Min(count - bufferPosition, bytesLeft);

                // fill buffer
                if (buffer != null)
                {
                    Buffer.BlockCopy(buffer,
                        offset + bufferPosition,
                        _current.Array,
                        _current.Offset + _currentPosition,
                        bytesToCopy);
                }

                bufferPosition += bytesToCopy;

                // move position in current segment (and go to next segment if finish)
                MoveForward(bytesToCopy);

                if (_isEOF) break;
            }

            ENSURE(count == bufferPosition, "current value must fit inside defined buffer");

            return bufferPosition;
        }

#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        /// <summary>
        /// Write bytes from buffer into segmentsr. Return how many bytes was write
        /// </summary>
        public int Write(Span<byte> buffer)
        {
            var bufferPosition = 0;
            var count = buffer.Length;

            while (bufferPosition < count)
            {
                var bytesLeft = _current.Count - _currentPosition;
                var bytesToCopy = Math.Min(count - bufferPosition, bytesLeft);

                // fill buffer
                var sliceCount = Math.Min(bytesLeft, count - bufferPosition);
                buffer[bufferPosition..].CopyTo(new Span<byte>(_current.Array, _current.Offset + _currentPosition, sliceCount));

                bufferPosition += bytesToCopy;

                // move position in current segment (and go to next segment if finish)
                MoveForward(bytesToCopy);

                if (_isEOF) break;
            }

            ENSURE(count == bufferPosition, "current value must fit inside defined buffer");

            return bufferPosition;
        }
#endif

        /// <summary>
        /// Write bytes from buffer into segmentsr. Return how many bytes was write
        /// </summary>
        public int Write(byte[] buffer) => Write(buffer, 0, buffer.Length);

        /// <summary>
        /// Skip bytes (same as Write but with no array copy)
        /// </summary>
        public int Skip(int count) => Write(null, 0, count);

        /// <summary>
        /// Consume all data source until finish
        /// </summary>
        public void Consume()
        {
            if (_source != null)
            {
                while (_source.MoveNext())
                {
                }
            }
        }

        #endregion

        #region String

        /// <summary>
        /// Write String with \0 at end
        /// </summary>
        public void WriteCString(string value)
        {
            if (value.IndexOf('\0') > -1) throw LiteException.InvalidNullCharInString();

            var bytesCount = Encoding.UTF8.GetByteCount(value);
            var available = _current.Count - _currentPosition; // avaiable in current segment

            // can write direct in current segment (use < because need +1 \0)
            if (bytesCount < available)
            {
                Encoding.UTF8.GetBytes(value, 0, value.Length, _current.Array, _current.Offset + _currentPosition);

                _current[_currentPosition + bytesCount] = 0x00;

                MoveForward(bytesCount + 1); // +1 to '\0'
            }
            else
            {
                var buffer = BufferPool<byte>.Rent(bytesCount);

                Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, 0);

                Write(buffer, 0, bytesCount);

                _current[_currentPosition] = 0x00;

                MoveForward(1);

                BufferPool<byte>.Return(buffer);
            }
        }

        /// <summary>
        /// Write string into output buffer. 
        /// Support direct string (with no length information) or BSON specs: with (legnth + 1) [4 bytes] before and '\0' at end = 5 extra bytes
        /// </summary>
        public void WriteString(string value, bool specs)
        {
            var count = Encoding.UTF8.GetByteCount(value);

            if (specs)
            {
                Write(count + 1); // write Length + 1 (for \0)
            }

            if (count <= _current.Count - _currentPosition)
            {
                Encoding.UTF8.GetBytes(value, 0, value.Length, _current.Array, _current.Offset + _currentPosition);

                MoveForward(count);
            }
            else
            {
                // rent a buffer to be re-usable
                var buffer = BufferPool<byte>.Rent(count);

                Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, 0);

                Write(buffer, 0, count);

                BufferPool<byte>.Return(buffer);
            }

            if (specs)
            {
                Write((byte)0x00);
            }
        }

        #endregion

        #region Numbers

        public void Write(int value)
        {
#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            Span<byte> buffer = stackalloc byte[4];
            BitConverter.TryWriteBytes(buffer, value);
            Write(buffer);
#else
            var buffer = BitConverter.GetBytes(value);
            Write(buffer, 0, 4);
#endif
        }

        public void Write(long value)
        {
#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            Span<byte> buffer = stackalloc byte[8];
            BitConverter.TryWriteBytes(buffer, value);
            Write(buffer);
#else
            var buffer = BitConverter.GetBytes(value);
            Write(buffer, 0, 8);
#endif
        }

        public void Write(uint value)
        {
#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            Span<byte> buffer = stackalloc byte[4];
            BitConverter.TryWriteBytes(buffer, value);
            Write(buffer);
#else
            var buffer = BitConverter.GetBytes(value);
            Write(buffer, 0, 4);
#endif
        }

        public void Write(double value)
        {
#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            Span<byte> buffer = stackalloc byte[8];
            BitConverter.TryWriteBytes(buffer, value);
            Write(buffer);
#else
            var buffer = BitConverter.GetBytes(value);
            Write(buffer, 0, 8);
#endif
        }

        public void Write(decimal value)
        {
            var bits = decimal.GetBits(value);
            Write(bits[0]);
            Write(bits[1]);
            Write(bits[2]);
            Write(bits[3]);
        }

        #endregion

        #region Complex Types

        /// <summary>
        /// Write DateTime as UTC ticks (not BSON format)
        /// </summary>
        public void Write(DateTime value)
        {
            var utc = (value == DateTime.MinValue || value == DateTime.MaxValue) ? value : value.ToUniversalTime();

            Write(utc.Ticks);
        }

        /// <summary>
        /// Write Guid as 16 bytes array
        /// </summary>
        public void Write(Guid value)
        {
            // there is no avaiable value.TryWriteBytes (TODO: implement conditional compile)?
            var bytes = value.ToByteArray();

            Write(bytes, 0, 16);
        }

        /// <summary>
        /// Write ObjectId as 12 bytes array
        /// </summary>
        public void Write(ObjectId value)
        {
            if (_currentPosition + 12 <= _current.Count)
            {
                value.ToByteArray(_current.Array, _current.Offset + _currentPosition);

                MoveForward(12);
            }
            else
            {
                var buffer = BufferPool<byte>.Rent(12);

                value.ToByteArray(buffer, 0);

                Write(buffer, 0, 12);

                BufferPool<byte>.Return(buffer);
            }
        }

        /// <summary>
        /// Write a boolean as 1 byte (0 or 1)
        /// </summary>
        public void Write(bool value)
        {
            _current[_currentPosition] = value ? (byte)0x01 : (byte)0x00;
            MoveForward(1);
        }

        /// <summary>
        /// Write single byte
        /// </summary>
        public void Write(byte value)
        {
            _current[_currentPosition] = value;
            MoveForward(1);
        }

        /// <summary>
        /// Write PageAddress as PageID, Index
        /// </summary>
        internal void Write(PageAddress address)
        {
            Write(address.PageID);
            Write(address.Index);
        }

        #endregion

        #region BsonDocument as SPECS

        /// <summary>
        /// Write BsonArray as BSON specs. Returns array bytes count
        /// </summary>
        public int WriteArray(BsonArray value, bool recalc)
        {
            var bytesCount = value.GetBytesCount(recalc);

            Write(bytesCount);

            for (var i = 0; i < value.Count; i++)
            {
                WriteElement(i.ToString(), value[i]);
            }

            Write((byte)0x00);

            return bytesCount;
        }

        /// <summary>
        /// Write BsonDocument as BSON specs. Returns document bytes count
        /// </summary>
        public int WriteDocument(BsonDocument value, bool recalc)
        {
            var bytesCount = value.GetBytesCount(recalc);

            Write(bytesCount);

            foreach (var el in value.GetElements())
            {
                WriteElement(el.Key, el.Value);
            }

            Write((byte)0x00);

            return bytesCount;
        }

        private void WriteElement(string key, BsonValue value)
        {
            // cast RawValue to avoid one if on As<Type>
            switch (value.Type)
            {
                case BsonType.Double:
                    Write((byte)0x01);
                    WriteCString(key);
                    Write(value.AsDouble);
                    break;

                case BsonType.String:
                    Write((byte)0x02);
                    WriteCString(key);
                    WriteString(value.AsString, true); // true = BSON Specs (add LENGTH at begin + \0 at end)
                    break;

                case BsonType.Document:
                    Write((byte)0x03);
                    WriteCString(key);
                    WriteDocument(value.AsDocument, false);
                    break;

                case BsonType.Array:
                    Write((byte)0x04);
                    WriteCString(key);
                    WriteArray(value.AsArray, false);
                    break;

                case BsonType.Binary:
                    Write((byte)0x05);
                    WriteCString(key);
                    var bytes = value.AsBinary;
                    Write(bytes.Length);
                    Write((byte)0x00); // subtype 00 - Generic binary subtype
                    Write(bytes, 0, bytes.Length);
                    break;

                case BsonType.Guid:
                    Write((byte)0x05);
                    WriteCString(key);
                    var guid = value.AsGuid;
                    Write(16);
                    Write((byte)0x04); // UUID
                    Write(guid);
                    break;

                case BsonType.ObjectId:
                    Write((byte)0x07);
                    WriteCString(key);
                    Write(value.AsObjectId);
                    break;

                case BsonType.Boolean:
                    Write((byte)0x08);
                    WriteCString(key);
                    Write((byte)(value.AsBoolean ? 0x01 : 0x00));
                    break;

                case BsonType.DateTime:
                    Write((byte)0x09);
                    WriteCString(key);
                    var date = value.AsDateTime;
                    // do not convert to UTC min/max date values - #19
                    var utc = (date == DateTime.MinValue || date == DateTime.MaxValue) ? date : date.ToUniversalTime();
                    var ts = utc - BsonValue.UnixEpoch;
                    Write(Convert.ToInt64(ts.TotalMilliseconds));
                    break;

                case BsonType.Null:
                    Write((byte)0x0A);
                    WriteCString(key);
                    break;

                case BsonType.Int32:
                    Write((byte)0x10);
                    WriteCString(key);
                    Write(value.AsInt32);
                    break;

                case BsonType.Int64:
                    Write((byte)0x12);
                    WriteCString(key);
                    Write(value.AsInt64);
                    break;

                case BsonType.Decimal:
                    Write((byte)0x13);
                    WriteCString(key);
                    Write(value.AsDecimal);
                    break;

                case BsonType.MinValue:
                    Write((byte)0xFF);
                    WriteCString(key);
                    break;

                case BsonType.MaxValue:
                    Write((byte)0x7F);
                    WriteCString(key);
                    break;
            }
        }

        #endregion

        public void Dispose()
        {
            _source?.Dispose();
        }
    }
}