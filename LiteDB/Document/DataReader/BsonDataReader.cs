using System;
using System.Collections.Generic;

namespace LiteDB
{
    /// <summary>
    /// Class to read void, one or a collection of BsonValues. Used in SQL execution commands and query returns. Use local data source (IEnumerable[BsonDocument])
    /// </summary>
    public class BsonDataReader : IBsonDataReader
    {
        private readonly IEnumerator<BsonValue> _source;

        private BsonValue _current;
        private bool _isFirst;
        private bool _disposed;

        /// <summary>
        /// Initialize with no value
        /// </summary>
        internal BsonDataReader()
        {
            HasValues = false;
        }

        /// <summary>
        /// Initialize with a single value
        /// </summary>
        internal BsonDataReader(BsonValue value, string collection = null)
        {
            _current = value;
            _isFirst = HasValues = true;
            Collection = collection;
        }

        /// <summary>
        /// Initialize with an IEnumerable data source
        /// </summary>
        internal BsonDataReader(IEnumerable<BsonValue> values, string collection)
        {
            _source = values.GetEnumerator();
            Collection = collection;

            if (_source.MoveNext())
            {
                HasValues = _isFirst = true;
                _current = _source.Current;
            }
        }

        /// <summary>
        /// Return if has any value in result
        /// </summary>
        public bool HasValues { get; }

        /// <summary>
        /// Return current value
        /// </summary>
        public BsonValue Current => _current;

        /// <summary>
        /// Return collection name
        /// </summary>
        public string Collection { get; }

        /// <summary>
        /// Move cursor to next result. Returns true if read was possible
        /// </summary>
        public bool Read()
        {
            if (!HasValues) return false;

            if (_isFirst)
            {
                _isFirst = false;
                return true;
            }
            else if (_source != null)
            {
                var read = _source.MoveNext();
                _current = _source.Current;
                return read;
            }
            else
            {
                return false;
            }
        }

        public BsonValue this[string field]
        {
            get
            {
                return _current.AsDocument[field] ?? BsonValue.Null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~BsonDataReader()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            _disposed = true;

            if (disposing)
            {
                _source?.Dispose();
            }
        }
    }
}