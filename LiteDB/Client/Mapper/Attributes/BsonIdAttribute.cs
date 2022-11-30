using System;

namespace LiteDB
{
    /// <summary>
    /// Indicate that property will be used as BsonDocument Id
    /// </summary>
    public class BsonIdAttribute : Attribute
    {
        public bool AutoId { get; }

        public BsonIdAttribute()
        {
            AutoId = true;
        }

        public BsonIdAttribute(bool autoId)
        {
            AutoId = autoId;
        }
    }
}