using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace LiteDB
{
    public partial class BsonMapper
    {
        #region Basic direct .NET convert types

        // direct bson types
        private readonly HashSet<Type> _bsonTypes = new HashSet<Type>
        {
            typeof(string),
            typeof(int),
            typeof(long),
            typeof(bool),
            typeof(Guid),
            typeof(DateTime),
            typeof(byte[]),
            typeof(ObjectId),
            typeof(double),
            typeof(decimal)
        };

        // simple convert types
        private readonly HashSet<Type> _basicTypes = new HashSet<Type>
        {
            typeof(short),
            typeof(ushort),
            typeof(uint),
            typeof(float),
            typeof(char),
            typeof(byte),
            typeof(sbyte)
        };

        #endregion

        /// <summary>
        /// Deserialize a BsonDocument to entity class
        /// </summary>
        public virtual object ToObject(Type type, BsonDocument doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));

            // if T is BsonDocument, just return them
            if (type == typeof(BsonDocument)) return doc;

            return Deserialize(type, doc);
        }

        /// <summary>
        /// Deserialize a BsonDocument to entity class
        /// </summary>
        public virtual T ToObject<T>(BsonDocument doc)
        {
            return (T)ToObject(typeof(T), doc);
        }

        /// <summary>
        /// Deserialize a BsonValue to .NET object typed in T
        /// </summary>
        public T Deserialize<T>(BsonValue value)
        {
            if (value == null) return default;

            var result = Deserialize(typeof(T), value);

            return (T)result;
        }

        /// <summary>
        /// Deserilize a BsonValue to .NET object based on type parameter
        /// </summary>
        public object Deserialize(Type type, BsonValue value)
        {
            // null value - null returns
            if (value.IsNull) return null;

            // if is nullable, get underlying type
            if (Reflection.IsNullable(type))
            {
                type = Reflection.UnderlyingTypeOf(type);
            }

            // test if has a custom type implementation
            if (_customDeserializer.TryGetValue(type, out Func<BsonValue, object> custom))
            {
                return custom(value);
            }

            // check if your type is already a BsonValue/BsonDocument/BsonArray
            if (type == typeof(BsonValue))
            {
                return value;
            }
            else if (type == typeof(BsonDocument))
            {
                return value.AsDocument;
            }
            else if (type == typeof(BsonArray))
            {
                return value.AsArray;
            }

            // raw values to native bson values
            else if (_bsonTypes.Contains(type))
            {
                return value.RawValue;
            }

            // simple ConvertTo to basic .NET types
            else if (_basicTypes.Contains(type))
            {
                return Convert.ChangeType(value.RawValue, type);
            }

            // special cast to UInt64 to Int64
            else if (type == typeof(ulong))
            {
                return unchecked((ulong)value.AsInt64);
            }

            // enum value is an int
            else if (type.IsEnum)
            {
                if (value.IsString) return Enum.Parse(type, value.AsString);

                if (value.IsNumber) return Enum.ToObject(type, value.AsInt32);
            }

            // if value is array, deserialize as array
            else if (value.IsArray)
            {
                // when array are from an object (like in Dictionary<string, object> { ["array"] = new string[] { "a", "b" } 
                if (type == typeof(object))
                {
                    return DeserializeArray(typeof(object), value.AsArray);
                }
                if (type.IsArray)
                {
                    return DeserializeArray(type.GetElementType(), value.AsArray);
                }
                else
                {
                    return DeserializeList(type, value.AsArray);
                }
            }

            // if value is document, deserialize as document
            else if (value.IsDocument)
            {
                // if type is anonymous use special handler
                if (type.IsAnonymousType())
                {
                    return DeserializeAnonymousType(type, value.AsDocument);
                }

                var doc = value.AsDocument;

                // test if value is object and has _type
                if (doc.TryGetValue("_type", out var typeField) && typeField.IsString)
                {
                    type = _typeNameBinder.GetType(typeField.AsString);

                    if (type == null) throw LiteException.InvalidTypedName(typeField.AsString);
                }
                // when complex type has no definition (== typeof(object)) use Dictionary<string, object> to better set values
                else if (type == typeof(object))
                {
                    type = typeof(Dictionary<string, object>);
                }

                var entity = GetEntityMapper(type);

                // initialize CreateInstance
                entity.CreateInstance ??=
                        GetTypeCtor(entity) ??
                        ((BsonDocument v) => Reflection.CreateInstance(entity.ForType));

                var o = _typeInstantiator(type) ?? entity.CreateInstance(doc);

                if (o is IDictionary dict)
                {
                    if (o.GetType().IsGenericType)
                    {
                        var k = type.GetGenericArguments()[0];
                        var t = type.GetGenericArguments()[1];

                        DeserializeDictionary(k, t, dict, value.AsDocument);
                    }
                    else
                    {
                        DeserializeDictionary(typeof(object), typeof(object), dict, value.AsDocument);
                    }
                }
                else
                {
                    DeserializeObject(entity, o, doc);
                }

                return o;
            }

            // in last case, return value as-is - can cause "cast error"
            // it's used for "public object MyInt { get; set; }"
            return value.RawValue;
        }

        private object DeserializeArray(Type type, BsonArray array)
        {
            var arr = Array.CreateInstance(type, array.Count);
            var idx = 0;

            foreach (var item in array)
            {
                arr.SetValue(Deserialize(type, item), idx++);
            }

            return arr;
        }

        private object DeserializeList(Type type, BsonArray value)
        {
            var itemType = Reflection.GetListItemType(type);
            var enumerable = (IEnumerable)Reflection.CreateInstance(type);

            if (enumerable is IList list)
            {
                foreach (BsonValue item in value)
                {
                    list.Add(Deserialize(itemType, item));
                }
            }
            else
            {
                var addMethod = type.GetMethod("Add");

                foreach (BsonValue item in value)
                {
                    addMethod.Invoke(enumerable, new[] { Deserialize(itemType, item) });
                }
            }

            return enumerable;
        }

        private void DeserializeDictionary(Type K, Type T, IDictionary dict, BsonDocument value)
        {
            var isKEnum = K.IsEnum;
            foreach (var el in value.GetElements())
            {
                var k = isKEnum ? Enum.Parse(K, el.Key) : K == typeof(Uri) ? new Uri(el.Key) : Convert.ChangeType(el.Key, K);
                var v = Deserialize(T, el.Value);

                dict.Add(k, v);
            }
        }

        private void DeserializeObject(EntityMapper entity, object obj, BsonDocument value)
        {
            foreach (var member in entity.Members.Where(x => x.Setter != null))
            {
                if (value.TryGetValue(member.FieldName, out var val))
                {
                    // check if has a custom deserialize function
                    if (member.Deserialize != null)
                    {
                        member.Setter(obj, member.Deserialize(val, this));
                    }
                    else
                    {
                        member.Setter(obj, Deserialize(member.DataType, val));
                    }
                }
            }
        }

        private object DeserializeAnonymousType(Type type, BsonDocument value)
        {
            var args = new List<object>();
            var ctor = type.GetConstructors()[0];

            foreach (var par in ctor.GetParameters())
            {
                var arg = Deserialize(par.ParameterType, value[par.Name]);

                args.Add(arg);
            }

            var obj = Activator.CreateInstance(type, args.ToArray());

            return obj;
        }
    }
}