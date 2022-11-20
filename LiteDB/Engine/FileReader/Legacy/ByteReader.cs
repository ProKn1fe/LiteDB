using System;
using System.Text;

namespace LiteDB
{
    internal class ByteReader
    {
        private readonly byte[] _buffer;
        private readonly int _length;
        private int _pos;

        public int Position { get { return _pos; } set { _pos = value; } }

        public ByteReader(byte[] buffer)
        {
            _buffer = buffer;
            _length = buffer.Length;
            _pos = 0;
        }

        public void Skip(int length)
        {
            _pos += length;
        }

        #region Native data types

        public byte ReadByte()
        {
            var value = _buffer[_pos];

            _pos++;

            return value;
        }

        public bool ReadBoolean()
        {
            var value = _buffer[_pos];

            _pos++;

            return value != 0;
        }

        public ushort ReadUInt16()
        {
            _pos += 2;
            return BitConverter.ToUInt16(_buffer, _pos - 2);
        }

        public uint ReadUInt32()
        {
            _pos += 4;
            return BitConverter.ToUInt32(_buffer, _pos - 4);
        }

        public ulong ReadUInt64()
        {
            _pos += 8;
            return BitConverter.ToUInt64(_buffer, _pos - 8);
        }

        public short ReadInt16()
        {
            _pos += 2;
            return BitConverter.ToInt16(_buffer, _pos - 2);
        }

        public int ReadInt32()
        {
            _pos += 4;
            return BitConverter.ToInt32(_buffer, _pos - 4);
        }

        public long ReadInt64()
        {
            _pos += 8;
            return BitConverter.ToInt64(_buffer, _pos - 8);
        }

        public float ReadSingle()
        {
            _pos += 4;
            return BitConverter.ToSingle(_buffer, _pos - 4);
        }

        public double ReadDouble()
        {
            _pos += 8;
            return BitConverter.ToDouble(_buffer, _pos - 8);
        }

        public decimal ReadDecimal()
        {
            _pos += 16;
            var a = BitConverter.ToInt32(_buffer, _pos - 16);
            var b = BitConverter.ToInt32(_buffer, _pos - 12);
            var c = BitConverter.ToInt32(_buffer, _pos - 8);
            var d = BitConverter.ToInt32(_buffer, _pos - 4);
            return new decimal(new int[] {  a, b, c, d });
        }

        public byte[] ReadBytes(int count)
        {
            var buffer = new byte[count];

            System.Buffer.BlockCopy(_buffer, _pos, buffer, 0, count);

            _pos += count;

            return buffer;
        }

        #endregion

        #region Extended types

        public string ReadString()
        {
            var length = ReadInt32();
            var str = Encoding.UTF8.GetString(_buffer, _pos, length);
            _pos += length;

            return str;
        }

        public string ReadString(int length)
        {
            var str = Encoding.UTF8.GetString(_buffer, _pos, length);
            _pos += length;

            return str;
        }

        /// <summary>
        /// Read BSON string add \0x00 at and of string and add this char in length before
        /// </summary>
        public string ReadBsonString()
        {
            var length = ReadInt32();
            var str = Encoding.UTF8.GetString(_buffer, _pos, length - 1);
            _pos += length;

            return str;
        }

        public string ReadCString()
        {
            var pos = _pos;
            var length = 0;

            while (true)
            {
                if (_buffer[pos] == 0x00)
                {
                    var str = Encoding.UTF8.GetString(_buffer, _pos, length);
                    _pos += length + 1; // read last 0x00
                    return str;
                }
                else if (pos > _length)
                {
                    return "_";
                }

                pos++;
                length++;
            }
        }

        public DateTime ReadDateTime()
        {
            // fix #921 converting index key into LocalTime
            // this is not best solution because uctDate must be a global parameter
            // this will be review in v5
            var date = new DateTime(ReadInt64(), DateTimeKind.Utc);

            return date.ToLocalTime();
        }

        public Guid ReadGuid()
        {
            return new Guid(ReadBytes(16));
        }

        public ObjectId ReadObjectId()
        {
            return new ObjectId(ReadBytes(12));
        }

        // Legacy PageAddress structure: [uint, ushort]
        // public PageAddress ReadPageAddress()
        // {
        //     return new PageAddress(this.ReadUInt32(), this.ReadUInt16());
        // }

        public BsonValue ReadBsonValue(ushort length)
        {
            var type = (BsonType)ReadByte();

            switch (type)
            {
                case BsonType.Null: return BsonValue.Null;

                case BsonType.Int32: return ReadInt32();
                case BsonType.Int64: return ReadInt64();
                case BsonType.Double: return ReadDouble();
                case BsonType.Decimal: return ReadDecimal();

                case BsonType.String: return ReadString(length);

                case BsonType.Document: return new BsonReader(false).ReadDocument(this);
                case BsonType.Array: return new BsonReader(false).ReadArray(this);

                case BsonType.Binary: return ReadBytes(length);
                case BsonType.ObjectId: return ReadObjectId();
                case BsonType.Guid: return ReadGuid();

                case BsonType.Boolean: return ReadBoolean();
                case BsonType.DateTime: return ReadDateTime();

                case BsonType.MinValue: return BsonValue.MinValue;
                case BsonType.MaxValue: return BsonValue.MaxValue;
            }

            throw new NotImplementedException();
        }

        #endregion
    }
}