namespace LiteDB.Engine
{
    /// <summary>
    /// Implement similar as ArrayPool for byte array
    /// </summary>
    internal class BufferPool<T>
    {
#if NETFRAMEWORK || NETSTANDARD2_0

        private static readonly object _lock;
        private static readonly ArrayPool<T> _bytePool;

        static BufferPool()
        {
            _lock = new object();
            _bytePool = new ArrayPool<T>();
        }
        
        public static T[] Rent(int count)
        {
            lock (_lock)
            {
                return _bytePool.Rent(count);
            }
        }

        public static void Return(T[] buffer)
        {
            lock (_lock)
            {
                _bytePool.Return(buffer);
            }
        }
    }

#else
        // Use native arraypool in version where it exists.
        private static System.Buffers.ArrayPool<T> _bytePool => System.Buffers.ArrayPool<T>.Shared;

        public static T[] Rent(int count)
        {
            return _bytePool.Rent(count);
        }

        public static void Return(T[] buffer)
        {
            _bytePool.Return(buffer);
        }
    }

#endif
}