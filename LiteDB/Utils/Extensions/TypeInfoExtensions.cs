using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections;

namespace LiteDB
{
    internal static class TypeInfoExtensions
    {
        public static bool IsAnonymousType(this Type type)
        {
            bool isAnonymousType =
                type.FullName.Contains("AnonymousType") &&
                type.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Any();

            return isAnonymousType;
        }

        public static bool IsEnumerable(this Type type)
        {
            return
                type != typeof(string) &&
                typeof(IEnumerable).IsAssignableFrom(type);
        }
    }
}
