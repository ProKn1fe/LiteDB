using System;
using System.Collections.Generic;
using System.Globalization;

namespace LiteDB
{
    /// <summary>
    /// Implement how database will compare to order by/find strings according defined culture/compare options
    /// If not set, default is CurrentCulture with IgnoreCase
    /// </summary>
    public class Collation : IComparer<BsonValue>, IComparer<string>, IEqualityComparer<BsonValue>
    {
        private CompareInfo _compareInfo => Culture.CompareInfo;

        public Collation(string collation)
        {
            var parts = collation.Split('/');
            var culture = parts[0];
            SortOptions = parts.Length > 1 ?
                (CompareOptions)Enum.Parse(typeof(CompareOptions), parts[1]) :
                CompareOptions.None;
            Culture = new CultureInfo(culture);
        }

        public Collation(int lcid, CompareOptions sortOptions)
        {
            SortOptions = sortOptions;
            Culture = CultureInfo.GetCultureInfo(lcid);
        }

        /// <summary>
        /// Default collation: CurrentCulture / IgnoreCase
        /// </summary>
        public static Collation Default = new Collation(CultureInfo.CurrentCulture.LCID, CompareOptions.IgnoreCase);

        /// <summary>
        /// Binary collection: InvariantCulture / Ordinal
        /// </summary>
        public static Collation Binary = new Collation(CultureInfo.InvariantCulture.LCID, CompareOptions.Ordinal);

        /// <summary>
        /// Get database language culture
        /// </summary>
        public CultureInfo Culture { get; }

        /// <summary>
        /// Get options to how string should be compared in sort
        /// </summary>
        public CompareOptions SortOptions { get; }

        /// <summary>
        /// Compare 2 string values using current culture/compare options
        /// </summary>
        public int Compare(string left, string right)
        {
            var result = _compareInfo.Compare(left, right, SortOptions);

            return result < 0 ? -1 : result > 0 ? +1 : 0;
        }

        /// <summary>
        /// Compare 2 chars values using current culture/compare options
        /// </summary>
        public int Compare(char left, char right)
        {
            //TODO implementar o compare corretamente
            return char.ToUpper(left, Culture) == char.ToUpper(right, Culture) ? 0 : 1;
        }

        public int Compare(BsonValue left, BsonValue rigth)
        {
            return left.CompareTo(rigth, this);
        }

        public bool Equals(BsonValue x, BsonValue y)
        {
            return Compare(x, y) == 0;
        }

        public int GetHashCode(BsonValue obj)
        {
            return obj.GetHashCode();
        }

        public override string ToString()
        {
            return Culture.Name + "/" + SortOptions.ToString();
        }
    }
}