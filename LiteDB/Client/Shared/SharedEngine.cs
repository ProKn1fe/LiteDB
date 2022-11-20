using LiteDB.Engine;

using System;
using System.Collections.Generic;
using System.IO;

namespace LiteDB
{
    public class SharedEngine : ILiteEngine
    {
        private readonly EngineSettings _settings;
        private readonly ReadWriteLockFile _locker;
        private LiteEngine _engine;
        private int _stack = 0;

        public SharedEngine(EngineSettings settings)
        {
            _settings = settings;

            var name = Path.GetFullPath(settings.Filename).ToLower().Sha1();

            var lockfile = FileHelper.GetSufixFile(settings.Filename, "-lock", false);

            _locker = new ReadWriteLockFile(lockfile, TimeSpan.FromSeconds(60));

            // create empty database if not exists
            if (File.Exists(settings.Filename) == false)
            {
                try
                {
                    _locker.AcquireLock(LockMode.Write, () =>
                    {
                        using (var e = new LiteEngine(settings)) { }
                    });
                }
                finally
                {
                    _locker.ReleaseLock();
                }
            }
        }

        /// <summary>
        /// Open database in safe mode
        /// </summary>
        private void OpenDatabase(bool readOnly)
        {
            lock (_locker)
            {
                _stack++;

                if (_stack == 1)
                {
                    open();
                }
                // change from read-only to read-write
                else if (_settings.ReadOnly == true && readOnly == false && _engine != null)
                {
                    _engine.Dispose();
                    open();
                }
            }

            void open()
            {
                try
                {
                    _locker.AcquireLock(readOnly ? LockMode.Read : LockMode.Write, () =>
                    {
                        _settings.ReadOnly = readOnly;

                        _engine = new LiteEngine(_settings);
                    });
                }
                catch
                {
                    if (_locker.IsLocked)
                    {
                        _locker.ReleaseLock();
                    }

                    _stack = 0;
                    throw;
                }
            }
        }

        /// <summary>
        /// Dequeue stack and dispose database on empty stack
        /// </summary>
        private void CloseDatabase()
        {
            lock(_locker)
            {
                _stack--;

                if (_stack == 0)
                {
                    _engine.Dispose();
                    _engine = null;

                    _locker.ReleaseLock();
                }
            }
        }

        #region Transaction Operations

        public bool BeginTrans()
        {
            OpenDatabase(false);

            try
            {
                var result = _engine.BeginTrans();

                if (result == false)
                {
                    _stack--;
                }

                return result;
            }
            catch
            {
                CloseDatabase();
                throw;
            }
        }

        public bool Commit()
        {
            if (_engine == null) return false;

            try
            {
                return _engine.Commit();
            }
            finally
            {
                CloseDatabase();
            }
        }

        public bool Rollback()
        {
            if (_engine == null) return false;

            try
            {
                return _engine.Rollback();
            }
            finally
            {
                CloseDatabase();
            }
        }

        #endregion

        #region Read Operation

        public IBsonDataReader Query(string collection, Query query)
        {
            OpenDatabase(true);

            var reader = _engine.Query(collection, query);

            return new SharedDataReader(reader, () => CloseDatabase());
        }

        public BsonValue Pragma(string name)
        {
            OpenDatabase(true);

            try
            {
                return _engine.Pragma(name);
            }
            finally
            {
                CloseDatabase();
            }
        }

        public bool Pragma(string name, BsonValue value)
        {
            OpenDatabase(false);

            try
            {
                return _engine.Pragma(name, value);
            }
            finally
            {
                CloseDatabase();
            }
        }

        #endregion

        #region Write Operations

        public int Checkpoint()
        {
            OpenDatabase(false);

            try
            {
                return _engine.Checkpoint();
            }
            finally
            {
                CloseDatabase();
            }
        }

        public long Rebuild(RebuildOptions options)
        {
            OpenDatabase(false);

            try
            {
                return _engine.Rebuild(options);
            }
            finally
            {
                CloseDatabase();
            }
        }

        public int Insert(string collection, IEnumerable<BsonDocument> docs, BsonAutoId autoId)
        {
            OpenDatabase(false);

            try
            {
                return _engine.Insert(collection, docs, autoId);
            }
            finally
            {
                CloseDatabase();
            }
        }

        public int Update(string collection, IEnumerable<BsonDocument> docs)
        {
            OpenDatabase(false);

            try
            {
                return _engine.Update(collection, docs);
            }
            finally
            {
                CloseDatabase();
            }
        }

        public int UpdateMany(string collection, BsonExpression extend, BsonExpression predicate)
        {
            OpenDatabase(false);

            try
            {
                return _engine.UpdateMany(collection, extend, predicate);
            }
            finally
            {
                CloseDatabase();
            }
        }

        public int Upsert(string collection, IEnumerable<BsonDocument> docs, BsonAutoId autoId)
        {
            OpenDatabase(false);

            try
            {
                return _engine.Upsert(collection, docs, autoId);
            }
            finally
            {
                CloseDatabase();
            }
        }

        public int Delete(string collection, IEnumerable<BsonValue> ids)
        {
            OpenDatabase(false);

            try
            {
                return _engine.Delete(collection, ids);
            }
            finally
            {
                CloseDatabase();
            }
        }

        public int DeleteMany(string collection, BsonExpression predicate)
        {
            OpenDatabase(false);

            try
            {
                return _engine.DeleteMany(collection, predicate);
            }
            finally
            {
                CloseDatabase();
            }
        }

        public bool DropCollection(string name)
        {
            OpenDatabase(false);

            try
            {
                return _engine.DropCollection(name);
            }
            finally
            {
                CloseDatabase();
            }
        }

        public bool RenameCollection(string name, string newName)
        {
            OpenDatabase(false);

            try
            {
                return _engine.RenameCollection(name, newName);
            }
            finally
            {
                CloseDatabase();
            }
        }

        public bool DropIndex(string collection, string name)
        {
            OpenDatabase(false);

            try
            {
                return _engine.DropIndex(collection, name);
            }
            finally
            {
                CloseDatabase();
            }
        }

        public bool EnsureIndex(string collection, string name, BsonExpression expression, bool unique)
        {
            OpenDatabase(false);

            try
            {
                return _engine.EnsureIndex(collection, name, expression, unique);
            }
            finally
            {
                CloseDatabase();
            }
        }

        #endregion

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~SharedEngine()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _engine?.Dispose();

                _locker.Dispose();
            }
        }
    }
}