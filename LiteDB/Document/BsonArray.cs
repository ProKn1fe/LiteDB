using System;
using System.Collections;
using System.Collections.Generic;

namespace LiteDB
{
    public class BsonArray : BsonValue, IList<BsonValue>
    {
        private const int InitialArraySize = 32;

        public BsonArray()
            : base(BsonType.Array, new List<BsonValue>(InitialArraySize))
        {
        }

        private BsonArray(int? initialCapacity = InitialArraySize)
            : base(BsonType.Array, new List<BsonValue>(initialCapacity < InitialArraySize ? InitialArraySize : initialCapacity.Value))
        {
        }

        public BsonArray(List<BsonValue> array)
            : this(array?.Count)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));

            AddRange(array);
        }

        public BsonArray(params BsonValue[] array)
            : this(array?.Length)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));

            AddRange(array);
        }

        public BsonArray(BsonValue value)
            : this()
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            Add(value);
        }

        public BsonArray(IEnumerable<BsonValue> items)
            : this()
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            AddRange(items);
        }

        private IList<BsonValue> _rawArray;
        public IList<BsonValue> RawArray
        {
            get => _rawArray ??= RawValue as IList<BsonValue>;
        }

        public override BsonValue this[int index]
        {
            get
            {
                return RawArray[index];
            }
            set
            {
                RawArray[index] = value ?? Null;
            }
        }

        public int Count => RawArray.Count;

        public bool IsReadOnly => false;

        public void Add(BsonValue item) => RawArray.Add(item ?? Null);

        public void AddRange<TCollection>(TCollection collection)
            where TCollection : ICollection<BsonValue>
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            var list = (List<BsonValue>)base.RawValue;

            var listEmptySpace = list.Capacity - list.Count;
            if (listEmptySpace < collection.Count)
            {
                list.Capacity += collection.Count;
            }

            foreach (var bsonValue in collection)
            {
                list.Add(bsonValue ?? Null);
            }
        }

        public void AddRange(IEnumerable<BsonValue> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            foreach (var item in items)
            {
                Add(item ?? Null);
            }
        }

        public void Clear() => RawArray.Clear();

        public bool Contains(BsonValue item) => RawArray.Contains(item ?? Null);

        public void CopyTo(BsonValue[] array, int arrayIndex) => RawArray.CopyTo(array, arrayIndex);

        public IEnumerator<BsonValue> GetEnumerator() => RawArray.GetEnumerator();

        public int IndexOf(BsonValue item) => RawArray.IndexOf(item ?? Null);

        public void Insert(int index, BsonValue item) => RawArray.Insert(index, item ?? Null);

        public bool Remove(BsonValue item) => RawArray.Remove(item);

        public void RemoveAt(int index) => RawArray.RemoveAt(index);

        IEnumerator IEnumerable.GetEnumerator()
        {
            foreach (var value in RawArray)
            {
                yield return value;
            }
        }

        public override int CompareTo(BsonValue other)
        {
            // if types are different, returns sort type order
            if (other.Type != BsonType.Array) return Type.CompareTo(other.Type);

            var otherArray = other.AsArray;

            var result = 0;
            var i = 0;
            var stop = Math.Min(Count, otherArray.Count);

            // compare each element
            for (; result == 0 && i < stop; i++)
                result = this[i].CompareTo(otherArray[i]);

            if (result != 0) return result;
            if (i == Count) return i == otherArray.Count ? 0 : -1;
            return 1;
        }

        private int _length;

        internal override int GetBytesCount(bool recalc)
        {
            if (!recalc && _length > 0) return _length;

            var length = 5;
            var array = RawArray;

            for (var i = 0; i < array.Count; i++)
            {
                length += GetBytesCountElement(i.ToString(), array[i]);
            }

            return _length = length;
        }
    }
}