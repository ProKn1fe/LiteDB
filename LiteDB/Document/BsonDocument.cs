using LiteDB.Engine;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace LiteDB
{
    public class BsonDocument : BsonValue, IDictionary<string, BsonValue>
    {
        private const int InitialDictionarySize = 32;

        public BsonDocument()
            : base(BsonType.Document, new Dictionary<string, BsonValue>(InitialDictionarySize, StringComparer.OrdinalIgnoreCase))
        {
        }

        internal BsonDocument(int? initialCapacity = InitialDictionarySize)
            : base(BsonType.Document, new Dictionary<string, BsonValue>(initialCapacity < InitialDictionarySize ? InitialDictionarySize : initialCapacity.Value, StringComparer.OrdinalIgnoreCase))
        {
        }

        public BsonDocument(ConcurrentDictionary<string, BsonValue> dict)
            : this(dict?.Count)
        {
            if (dict == null) throw new ArgumentNullException(nameof(dict));

            foreach (var element in dict)
            {
                Add(element);
            }
        }

        public BsonDocument(IDictionary<string, BsonValue> dict)
            : this(dict?.Count)
        {
            if (dict == null) throw new ArgumentNullException(nameof(dict));

            foreach (var element in dict)
            {
                Add(element);
            }
        }

        private IDictionary<string, BsonValue> _rawDictionary;
        public IDictionary<string, BsonValue> RawDictionary
        {
            get => _rawDictionary ??= RawValue as IDictionary<string, BsonValue>;
        }

        /// <summary>
        /// Get/Set position of this document inside database. It's filled when used in Find operation.
        /// </summary>
        internal PageAddress RawId { get; set; } = PageAddress.Empty;

        /// <summary>
        /// Get/Set a field for document. Fields are case sensitive
        /// </summary>
        public override BsonValue this[string key]
        {
            get
            {
                return RawDictionary.GetOrDefault(key, Null);
            }
            set
            {
                RawDictionary[key] = value ?? Null;
            }
        }

        #region CompareTo

        public override int CompareTo(BsonValue other)
        {
            // if types are different, returns sort type order
            if (other.Type != BsonType.Document) return Type.CompareTo(other.Type);

            var thisKeys = Keys.ToArray();
            var thisLength = thisKeys.Length;

            var otherDoc = other.AsDocument;
            var otherKeys = otherDoc.Keys.ToArray();
            var otherLength = otherKeys.Length;

            var result = 0;
            var i = 0;
            var stop = Math.Min(thisLength, otherLength);

            for (; result == 0 && i < stop; i++)
                result = this[thisKeys[i]].CompareTo(otherDoc[thisKeys[i]]);

            // are different
            if (result != 0) return result;

            // test keys length to check which is bigger
            if (i == thisLength) return i == otherLength ? 0 : -1;

            return 1;
        }

        #endregion

        #region IDictionary

        public ICollection<string> Keys => RawDictionary.Keys;

        public ICollection<BsonValue> Values => RawDictionary.Values;

        public int Count => RawDictionary.Count;

        public bool IsReadOnly => false;

        public bool ContainsKey(string key) => RawDictionary.ContainsKey(key);

        /// <summary>
        /// Get all document elements - Return "_id" as first of all (if exists)
        /// </summary>
        public IEnumerable<KeyValuePair<string, BsonValue>> GetElements()
        {
            if (RawDictionary.TryGetValue("_id", out var id))
                yield return new KeyValuePair<string, BsonValue>("_id", id);

            foreach (var key in RawDictionary.Keys)
            {
                if (key == "_id") continue;
                yield return new KeyValuePair<string, BsonValue>(key, RawDictionary[key]);
            }
        }

        public void Add(string key, BsonValue value) => RawDictionary.Add(key, value ?? Null);

        public bool Remove(string key) => RawDictionary.Remove(key);

        public void Clear() => RawDictionary.Clear();

        public bool TryGetValue(string key, out BsonValue value) => RawDictionary.TryGetValue(key, out value);

        public void Add(KeyValuePair<string, BsonValue> item) => Add(item.Key, item.Value);

        public bool Contains(KeyValuePair<string, BsonValue> item) => RawDictionary.Contains(item);

        public bool Remove(KeyValuePair<string, BsonValue> item) => Remove(item.Key);

        public IEnumerator<KeyValuePair<string, BsonValue>> GetEnumerator() => RawDictionary.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => RawDictionary.GetEnumerator();

        public void CopyTo(KeyValuePair<string, BsonValue>[] array, int arrayIndex)
        {
            RawDictionary.CopyTo(array, arrayIndex);
        }

        public void CopyTo(BsonDocument other)
        {
            foreach (var element in this)
            {
                other[element.Key] = element.Value;
            }
        }

        #endregion

        private int _length;

        internal override int GetBytesCount(bool recalc)
        {
            if (!recalc && _length > 0) return _length;

            var length = 5;

            foreach (var key in RawDictionary.Keys)
            {
                length += GetBytesCountElement(key, RawDictionary[key]);
            }

            return _length = length;
        }
    }
}
