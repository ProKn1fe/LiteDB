﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using static LiteDB.Constants;

namespace LiteDB.Engine
{
    /// <summary>
    /// Implement custom fast/in memory mapped disk access
    /// [ThreadSafe]
    /// </summary>
    internal class DiskService : IDisposable
    {
        private IStreamFactory _streamFactory;

        private StreamPool _streamPool;

        private long _logStartPosition;
        private long _logEndPosition;

        public DiskService(EngineSettings settings, int[] memorySegmentSizes)
        {
            Cache = new MemoryCache(memorySegmentSizes);

            // get new stream factory based on settings
            _streamFactory = settings.CreateDataFactory();

            // create stream pool
            _streamPool = new StreamPool(_streamFactory, settings.ReadOnly);

            // create async writer queue for log file
            Queue = new DiskWriterQueue(_streamPool.Writer);

            // checks if is a new file
            var isNew = !settings.ReadOnly && _streamPool.Writer.Length == 0;

            // create new database if not exist yet
            if (isNew)
            {
                LOG($"creating new database: '{Path.GetFileName(_streamFactory.Name)}'", "DISK");

                Header = Initialize(_streamPool.Writer, settings.Collation, settings.InitialSize);
            }
            else
            {
                // load header page from position 0 from file
                var stream = _streamPool.Rent();
                var buffer = new PageBuffer(new byte[PAGE_SIZE], 0, 0) { Position = 0 };

                try
                {
                    stream.Position = 0;
                    stream.Read(buffer.Array, 0, PAGE_SIZE);

                    // if first byte are 1 this datafile are encrypted but has do defined password to open
                    if (buffer[0] == 1) throw new LiteException(0, "This data file is encrypted and needs a password to open");

                    Header = new HeaderPage(buffer);

                    _streamPool.Return(stream);
                }
                catch
                {
                    // return to pool before dispose 
                    _streamPool.Return(stream);

                    Dispose();

                    throw;
                }
            }

            // define start/end position for log content
            _logStartPosition = (Header.LastPageID + 1) * PAGE_SIZE;
            _logEndPosition = _logStartPosition; // will be updated by RestoreIndex
        }

        /// <summary>
        /// Get async queue writer
        /// </summary>
        public DiskWriterQueue Queue { get; }

        /// <summary>
        /// Get memory cache instance
        /// </summary>
        public MemoryCache Cache { get; }

        /// <summary>
        /// Get writer Stream single instance
        /// </summary>
        public Stream Writer => _streamPool.Writer;

        /// <summary>
        /// Get stream factory instance;
        /// </summary>
        public IStreamFactory Factory => _streamFactory;

        /// <summary>
        /// Get header page single database instance
        /// </summary>
        public HeaderPage Header { get; }

        /// <summary>
        /// Get log length
        /// </summary>
        public long LogLength => _logEndPosition - _logStartPosition;

        /// <summary>
        /// Get log start position in disk
        /// </summary>
        public long LogStartPosition => _logStartPosition;

        /// <summary>
        /// Get/Set log end position in disk
        /// </summary>
        public long LogEndPosition { get => _logEndPosition; set => _logEndPosition = value; }

        /// <summary>
        /// Create a new empty database (use synced mode)
        /// </summary>
        private HeaderPage Initialize(Stream stream, Collation collation, long initialSize)
        {
            var buffer = new PageBuffer(new byte[PAGE_SIZE], 0, 0) { Position = 0 };
            var header = new HeaderPage(buffer, 0);

            var pages = initialSize == 0 ? 0 : (int)(initialSize / PAGE_SIZE) - 1;

            // update last page ID (when initialSize > 0)
            header.LastPageID = (uint)pages;
            header.FreeEmptyPageList = pages == 0 ? uint.MaxValue : 1u;

            // update collation
            header.Pragmas.Set(Pragmas.COLLATION, (collation ?? Collation.Default).ToString(), false);

            // update buffer
            header.UpdateBuffer();

            stream.Write(buffer.Array, buffer.Offset, PAGE_SIZE);

            // create empty pages if defined initial size
            if (pages > 0)
            {
                for (uint p = 1; p <= pages; p++)
                {
                    var empty = new BasePage(new PageBuffer(new byte[PAGE_SIZE], 0, 0), p, PageType.Empty);

                    empty.NextPageID = p < pages ? p + 1 : uint.MaxValue;

                    empty.UpdateBuffer();

                    stream.Write(empty.Buffer.Array, 0, PAGE_SIZE);
                }
            }

            stream.FlushToDisk();

            return header;
        }

        /// <summary>
        /// Get a new instance for read data/log pages. This instance are not thread-safe - must request 1 per thread (used in Transaction)
        /// </summary>
        public DiskReader GetReader()
        {
            return new DiskReader(Cache, _streamPool);
        }

        /// <summary>
        /// Write pages inside file origin using async queue - returns how many pages are inside "pages"
        /// </summary>
        public int WriteAsync(IEnumerable<PageBuffer> pages)
        {
            var count = 0;

            foreach (var page in pages)
            {
                ENSURE(page.ShareCounter == BUFFER_WRITABLE, "to enqueue page, page must be writable");

                var dataPosition = BasePage.GetPagePosition(page.ReadInt32(BasePage.P_PAGE_ID));

                do
                {
                    // adding this page into file AS new page (at end of file)
                    // must add into cache to be sure that new readers can see this page
                    page.Position = Interlocked.Add(ref _logEndPosition, PAGE_SIZE) - PAGE_SIZE;
                }
                while (dataPosition > page.Position);

                // mark this page as readable and get cached paged to enqueue
                var readable = Cache.MoveToReadable(page);

                Queue.EnqueuePage(readable);

                count++;
            }

            Queue.Run();

            return count;
        }

        #region Sync Read/Write operations

        /// <summary>
        /// Read all log from current log position to end of file.
        /// This operation are sync and should not be run with any page on queue
        /// Use fullLogArea to read file to end
        /// </summary>
        public IEnumerable<PageBuffer> ReadLog(bool fullLogArea)
        {
            ENSURE(Queue.Length == 0, "no pages on queue before read sync log");

            // do not use MemoryCache factory - reuse same buffer array (one page per time)
            var buffer = new byte[PAGE_SIZE];
            var stream = _streamPool.Rent();

            try
            {
                // get file length
                var endPosition = fullLogArea ? _streamFactory.GetLength() : _logEndPosition;

                // set to first log page position
                stream.Position = _logStartPosition;

                while (stream.Position < endPosition)
                {
                    var position = stream.Position;

                    stream.Read(buffer, 0, PAGE_SIZE);

                    yield return new PageBuffer(buffer, 0, 0)
                    {
                        Position = position,
                        ShareCounter = 0
                    };
                }
            }
            finally
            {
                _streamPool.Return(stream);
            }
        }

        /// <summary>
        /// Read all pages inside datafile - do not consider in-cache only pages. Returns both Data and Log pages
        /// </summary>
        public IEnumerable<PageBuffer> ReadFull()
        {
            var buffer = new byte[PAGE_SIZE];
            var stream = _streamPool.Rent();

            try
            {
                // get file length
                var length = _streamFactory.GetLength();

                stream.Position = 0;

                while (stream.Position < length)
                {
                    var position = stream.Position;

                    stream.Read(buffer, 0, PAGE_SIZE);

                    yield return new PageBuffer(buffer, 0, 0)
                    {
                        Position = position,
                        ShareCounter = 0
                    };
                }
            }
            finally
            {
                _streamPool.Return(stream);
            }
        }

        /// <summary>
        /// Write pages DIRECT in disk with NO queue. Used in CHECKPOINT only
        /// </summary>
        public void Write(IEnumerable<PageBuffer> pages)
        {
            var stream = _streamPool.Writer;

            foreach (var page in pages)
            {
                ENSURE(page.ShareCounter == 0, "this page can't be shared to use sync operation - do not use cached pages");

                stream.Position = page.Position;

                stream.Write(page.Array, page.Offset, PAGE_SIZE);
            }

            stream.FlushToDisk();
        }

        /// <summary>
        /// Reset log position at end of file (based on header.LastPageID) and crop file if require
        /// </summary>
        public void ResetLogPosition(bool crop)
        {
            _logStartPosition = _logEndPosition = (Header.LastPageID + 1) * PAGE_SIZE;

            if (crop)
            {
                FileHelper.TrySetLength(_streamPool.Writer, _logStartPosition);
            }
        }

        /// <summary>
        /// Change data file password
        /// </summary>
        public void ChangePassword(string password, EngineSettings settings)
        {
            if (settings.Password == password) return;

            // rebuild file
            ChangePasswordRebuild(password);

            // change current settings password
            settings.Password = password;

            // close all streams
            _streamPool.Dispose();

            // new datafile will be created with new password
            _streamFactory = settings.CreateDataFactory();

            // create stream pool
            _streamPool = new StreamPool(_streamFactory, false);

            // log position still at same position
        }

        /// <summary>
        /// Rebuild datafile copy source to destination with 2 different Stream (pointing to same file)
        /// Can add, remove or change a password
        /// </summary>
        private void ChangePasswordRebuild(string password)
        {
            var source = Writer;
            var length = source.Length;

            // if destination stream are encrypted, came from end to begin
            if (password != null)
            {
                // encrypt
                var header = new byte[PAGE_SIZE];

                // read header page
                source.Position = 0;
                source.Read(header, 0, PAGE_SIZE);

                // create aes stream and initialize first page
                var destination = new AesStream(password, Writer is AesStream ? (Writer as AesStream).BaseStream : Writer, true);

                var position = length - PAGE_SIZE;
                var buffer = new byte[PAGE_SIZE];

                while (position > 0)
                {
                    source.Position = position;
                    source.Read(buffer, 0, PAGE_SIZE);

                    destination.Position = position;
                    destination.Write(buffer, 0, PAGE_SIZE);

                    position -= PAGE_SIZE;
                }

                // write header page
                destination.Position = 0;
                destination.Write(header, 0, PAGE_SIZE);
            }
            else
            {
                var destination = (source as AesStream).BaseStream;

                var position = 0;
                var buffer = new byte[PAGE_SIZE];

                while (position < length)
                {
                    source.Position = position;
                    source.Read(buffer, 0, PAGE_SIZE);

                    destination.Position = position;
                    destination.Write(buffer, 0, PAGE_SIZE);

                    position += PAGE_SIZE;
                }

                ENSURE(destination.Length == length + PAGE_SIZE, "current source must have 1 extra page for SALT");

                destination.SetLength(length);
            }
        }

        #endregion

        public void Dispose()
        {
            // dispose queue (wait finish)
            Queue?.Dispose();

            // dispose Stream pools
            _streamPool?.Dispose();

            // other disposes
            Cache?.Dispose();
        }
    }
}
