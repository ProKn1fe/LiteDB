namespace LiteDB.Engine
{
    /// <summary>
    /// Implement similar as ArrayPool for byte array
    /// </summary>
    internal class BufferPool
    {
#if NETFRAMEWORK || NETSTANDARD2_0

        private static readonly object _lock;
        private static readonly ArrayPool<byte> _bytePool;

        static BufferPool()
        {
            _lock = new object();
            _bytePool = new ArrayPool<byte>();
        }
        
        public static byte[] Rent(int count)
        {
            lock (_lock)
            {
                return _bytePool.Rent(count);
            }
        }

        public static void Return(byte[] buffer)
        {
            lock (_lock)
            {
                _bytePool.Return(buffer);
            }
        }
    }

#else
        // Use native arraypool in version where it exists.
        private static System.Buffers.ArrayPool<byte> _bytePool => System.Buffers.ArrayPool<byte>.Shared;

        public static byte[] Rent(int count)
        {
            return _bytePool.Rent(count);
        }

        public static void Return(byte[] buffer)
        {
            _bytePool.Return(buffer);
        }
    }

#endif
}