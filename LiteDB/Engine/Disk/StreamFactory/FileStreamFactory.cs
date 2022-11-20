﻿using System.IO;

using static LiteDB.Constants;

namespace LiteDB.Engine
{
    /// <summary>
    /// FileStream disk implementation of disk factory
    /// [ThreadSafe]
    /// </summary>
    internal class FileStreamFactory : IStreamFactory
    {
        private readonly string _filename;
        private readonly string _password;
        private readonly bool _hidden;

        public FileStreamFactory(string filename, string password, bool hidden)
        {
            _filename = filename;
            _password = password;
            _hidden = hidden;
        }

        /// <summary>
        /// Get data filename
        /// </summary>
        public string Name => Path.GetFileName(_filename);

        /// <summary>
        /// Create new data file FileStream instance based on filename
        /// </summary>
        public Stream GetStream(bool readOnly)
        {
            var stream = new FileStream(_filename,
                readOnly ? System.IO.FileMode.Open : System.IO.FileMode.OpenOrCreate,
                readOnly ? FileAccess.Read : FileAccess.ReadWrite,
                readOnly ? FileShare.ReadWrite : FileShare.Read,
                PAGE_SIZE,
                readOnly ? FileOptions.RandomAccess : FileOptions.SequentialScan);

            // new file
            if (readOnly == false && stream.Length == 0 && _hidden)
            {
                // hidden sort file
                File.SetAttributes(_filename, FileAttributes.Hidden);
            }

            return _password == null ? stream : new AesStream(_password, stream);
        }

        /// <summary>
        /// Get file length using FileInfo
        /// </summary>
        public long GetLength()
        {
            // getting size from OS - if encrypted must remove salt first page
            return new FileInfo(_filename).Length - (_password == null ? 0 : PAGE_SIZE);
        }

        /// <summary>
        /// Delete file (must all stream be closed)
        /// </summary>
        public void Delete()
        {
            File.Delete(_filename);
        }

        /// <summary>
        /// Close all stream on end
        /// </summary>
        public bool CloseOnDispose => true;
    }
}