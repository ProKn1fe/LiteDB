﻿using System.IO;

using static LiteDB.Constants;

namespace LiteDB.Engine
{
    /// <summary>
    /// Simple Stream disk implementation of disk factory - used for Memory/Temp database
    /// [ThreadSafe]
    /// </summary>
    internal class StreamFactory : IStreamFactory
    {
        private readonly Stream _stream;
        private readonly string _password;

        public StreamFactory(Stream stream, string password)
        {
            _stream = stream;
            _password = password;
        }

        /// <summary>
        /// Stream has no name (use stream type)
        /// </summary>
        public string Name => _stream is MemoryStream ? ":memory:" : _stream is TempStream ? ":temp:" : ":stream:";

        /// <summary>
        /// Use Synchronized wrapper to support multi thread in same Stream (using lock control)
        /// </summary>
        public Stream GetStream(bool readOnly)
        {
            if (_password == null)
            {
                return new ConcurrentStream(_stream);
            }
            else
            {
                return new AesStream(_password, new ConcurrentStream(_stream));
            }
        }

        /// <summary>
        /// Get file length using _stream.Length
        /// </summary>
        public long GetLength()
        {
            // if using AesStream reduce PAGE_SIZE (first SALT page)
            return _stream.Length - (_password == null ? 0 : PAGE_SIZE);
        }

        /// <summary>
        /// There is no delete method in Stream factory
        /// </summary>
        public void Delete()
        {
        }

        /// <summary>
        /// Do no dispose on finish
        /// </summary>
        public bool CloseOnDispose => false;
    }
}