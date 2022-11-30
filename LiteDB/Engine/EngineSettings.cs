using System;
using System.IO;

namespace LiteDB.Engine
{
    /// <summary>
    /// All engine settings used to starts new engine
    /// </summary>
    public class EngineSettings
    {
        /// <summary>
        /// Get/Set custom stream to be used as datafile (can be MemoryStrem or TempStream). Do not use FileStream - to use physical file, use "filename" attribute (and keep DataStrem null)
        /// </summary>
        public Stream DataStream { get; set; }

        /// <summary>
        /// Full path or relative path from DLL directory. Can use ':temp:' for temp database or ':memory:' for in-memory database. (default: null)
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        /// Get database password to decrypt pages
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// If database is new, initialize with allocated space (in bytes) (default: 0)
        /// </summary>
        public long InitialSize { get; set; } = 0;

        /// <summary>
        /// Create database with custom string collection (used only to create database) (default: Collation.Default)
        /// </summary>
        public Collation Collation { get; set; }

        /// <summary>
        /// Indicate that engine will open files in readonly mode (and will not support any database change)
        /// </summary>
        public bool ReadOnly { get; set; }

        /// <summary>
        /// Create new IStreamFactory for datafile
        /// </summary>
        internal IStreamFactory CreateDataFactory()
        {
            if (DataStream != null)
            {
                return new StreamFactory(DataStream, Password);
            }
            else if (Filename == ":memory:")
            {
                return new StreamFactory(new MemoryStream(), Password);
            }
            else if (Filename == ":temp:")
            {
                return new StreamFactory(new TempStream(), Password);
            }
            else if (!string.IsNullOrEmpty(Filename))
            {
                return new FileStreamFactory(Filename, Password, false);
            }

            throw new ArgumentException("EngineSettings must have Filename or DataStream as data source");
        }

        /// <summary>
        /// Create new IStreamFactory for temporary file (sort)
        /// </summary>
        internal IStreamFactory CreateTempFactory()
        {
            if (DataStream is MemoryStream || Filename == ":memory:" || ReadOnly)
            {
                return new StreamFactory(new MemoryStream(), null);
            }
            else if (Filename == ":temp:")
            {
                return new StreamFactory(new TempStream(), null);
            }
            else if (!string.IsNullOrEmpty(Filename))
            {
                var tempName = FileHelper.GetSufixFile(Filename, "-tmp", true);

                return new FileStreamFactory(tempName, null, true);
            }

            return new StreamFactory(new TempStream(), null);
        }
    }
}