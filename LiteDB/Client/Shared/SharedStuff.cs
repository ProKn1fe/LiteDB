using System;
using System.Security.Cryptography;

namespace LiteDB.Client.Shared
{
    internal class SharedStuff
    {
#if NET5_0_OR_GREATER
        public static Random Random => Random.Shared;
#else
        public static readonly Random Random = new Random();
#endif

        public static readonly RandomNumberGenerator RandomNumberGenerator = RandomNumberGenerator.Create();
    }
}
