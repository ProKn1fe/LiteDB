﻿using System;
using System.Collections;
using System.Linq;

namespace LiteDB
{
    public partial class BsonMapper
    {
        /// <summary>
        /// Serialize a entity class to BsonDocument
        /// </summary>
        public virtual BsonDocument ToDocument(Type type, object entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            // if object is BsonDocument, just return them
            if (entity is BsonDocument) return (BsonDocument)(object)entity;

            return Serialize(type, entity, 0).AsDocument;
        }

        /// <summary>
        /// Serialize a entity class to BsonDocument
        /// </summary>
        public virtual BsonDocument ToDocument<T>(T entity)
        {
            return ToDocument(typeof(T), entity)?.AsDocument;
        }

        /// <summary>
        /// Serialize to BsonValue any .NET object based on T type (using mapping rules)
        /// </summary>
        public BsonValue Serialize<T>(T obj)
        {
            return Serialize(typeof(T), obj, 0);
        }

        /// <summary>
        /// Serialize to BsonValue any .NET object based on type parameter (using mapping rules)
        /// </summary>
        public BsonValue Serialize(Type type, object obj)
        {
            return Serialize(type, obj, 0);
        }

        internal BsonValue Serialize(Type type, object obj, int depth)
        {
            if (++depth > MaxDepth) throw LiteException.DocumentMaxDepth(MaxDepth, type);

            if (obj == null) return BsonValue.Null;

            // if is already a bson value
            if (obj is BsonValue bsonValue) return bsonValue;

            // check if is a custom type
            else if (_customSerializer.TryGetValue(type, out var custom) || _customSerializer.TryGetValue(obj.GetType(), out custom))
            {
                return custom(obj);
            }
            // test string - mapper has some special options
            else if (obj is string)
            {
                var str = TrimWhitespace ? (obj as string).Trim() : (string)obj;

                if (EmptyStringToNull && str.Length == 0)
                {
                    return BsonValue.Null;
                }
                else
                {
                    return new BsonValue(str);
                }
            }
            // basic Bson data types (cast datatype for better performance optimization)
            else if (obj is int) return new BsonValue((int)obj);
            else if (obj is long) return new BsonValue((long)obj);
            else if (obj is double) return new BsonValue((double)obj);
            else if (obj is decimal) return new BsonValue((decimal)obj);
            else if (obj is byte[]) return new BsonValue((byte[])obj);
            else if (obj is ObjectId) return new BsonValue((ObjectId)obj);
            else if (obj is Guid) return new BsonValue((Guid)obj);
            else if (obj is bool) return new BsonValue((bool)obj);
            else if (obj is DateTime) return new BsonValue((DateTime)obj);
            // basic .net type to convert to bson
            else if (obj is short || obj is ushort || obj is byte || obj is sbyte)
            {
                return new BsonValue(Convert.ToInt32(obj));
            }
            else if (obj is uint)
            {
                return new BsonValue(Convert.ToInt64(obj));
            }
            else if (obj is ulong)
            {
                var ulng = ((ulong)obj);
                var lng = unchecked((long)ulng);

                return new BsonValue(lng);
            }
            else if (obj is float)
            {
                return new BsonValue(Convert.ToDouble(obj));
            }
            else if (obj is char)
            {
                return new BsonValue(obj.ToString());
            }
            else if (obj is Enum)
            {
                if (EnumAsInteger)
                {
                    return new BsonValue((int)obj);
                }
                else
                {
                    return new BsonValue(obj.ToString());
                }
            }
            // for dictionary
            else if (obj is IDictionary dict)
            {
                // when you are converting Dictionary<string, object>
                if (type == typeof(object))
                {
                    type = obj.GetType();
                }

                var itemType = type.IsGenericType ? type.GetGenericArguments()[1] : typeof(object);

                return SerializeDictionary(itemType, dict, depth);
            }
            // check if is a list or array
            else if (obj is IEnumerable)
            {
                return SerializeArray(Reflection.GetListItemType(obj.GetType()), obj as IEnumerable, depth);
            }
            // otherwise serialize as a plain object
            else
            {
                return SerializeObject(type, obj, depth);
            }
        }

        private BsonArray SerializeArray(Type type, IEnumerable array, int depth)
        {
            var arr = new BsonArray();

            foreach (var item in array)
            {
                arr.Add(Serialize(type, item, depth));
            }

            return arr;
        }

        private BsonDocument SerializeDictionary(Type type, IDictionary dict, int depth)
        {
            var o = new BsonDocument();

            foreach (var key in dict.Keys)
            {
                var value = dict[key];
                var skey = key.ToString();

                if (key is DateTime dateKey)
                {
                    skey = dateKey.ToString("o");
                }

                o[skey] = Serialize(type, value, depth);
            }

            return o;
        }

        private BsonDocument SerializeObject(Type type, object obj, int depth)
        {
            var t = obj.GetType();
            var doc = new BsonDocument();
            var entity = GetEntityMapper(t);

            // adding _type only where property Type is not same as object instance type
            if (type != t)
            {
                doc["_type"] = new BsonValue(_typeNameBinder.GetName(t));
            }

            foreach (var member in entity.Members.Where(x => x.Getter != null))
            {
                // get member value
                var value = member.Getter(obj);

                if (value == null && SerializeNullValues == false && member.FieldName != "_id") continue;

                // if member has a custom serialization, use it
                if (member.Serialize != null)
                {
                    doc[member.FieldName] = member.Serialize(value, this);
                }
                else
                {
                    doc[member.FieldName] = Serialize(member.DataType, value, depth);
                }
            }

            return doc;
        }
    }
}