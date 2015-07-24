using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace splunk4net.Buffering
{
    public interface ILogBufferDatabase
    {
        long Buffer(string data);
        void Unbuffer(long id);
        List<LogBufferItem> ListBufferedLogItems();
    }

    public class LogBufferDatabase: ILogBufferDatabase
    {
        public string ConnectionString { get { return string.Format("Data Source={0};Version=3", BufferDatabasePath); } }
        public string BufferDatabasePath { get { return _dbPath; }}
        private readonly string _dbPath;
        private static readonly object _lock = new object();

        public LogBufferDatabase(): this(GetBufferDatabasePathForApplication())
        {
        }

        public long Buffer(string data)
        {
            var item = new LogBufferItem(data);
            lock(_lock)
            {
                return ExecuteInsertWith(item.AsInsertSql());
            }
        }

        public void Unbuffer(long id)
        {
            lock(_lock)
            {
                ExecuteNonQueryWith(string.Format("delete from {0} where {1} = {2}",
                    DataConstants.TABLE, DataConstants.ID, id));
            }
        }

        internal LogBufferDatabase(string path)
        {
            _dbPath = path;
            CreateBufferDatabaseIfRequired();
        }

        private long ExecuteInsertWith(string asInsertSql)
        {
            asInsertSql += "; select last_insert_rowid();";
            return ExecuteScalarQuery<long>(asInsertSql);
        }

        private T ExecuteScalarQuery<T>(string sql)
        {
            using (var disposer = new AutoDisposer())
            {
                var connection = disposer.Add(GetOpenConnection());
                var cmd = disposer.Add(connection.CreateCommand());
                cmd.CommandText = sql;
                return (T)cmd.ExecuteScalar();
            }
        }

        private void ExecuteNonQueryWith(string sql)
        {
            using (var disposer = new AutoDisposer())
            {
                var connection = disposer.Add(GetOpenConnection());
                var cmd = disposer.Add(connection.CreateCommand());
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        private void CreateBufferDatabaseIfRequired()
        {
            lock(_lock)
            {
                if (!File.Exists(_dbPath))
                    CreateDatabaseAt(_dbPath);
            }
        }

        private void CreateDatabaseAt(string dbPath)
        {
            var dbFolder = Path.GetDirectoryName(dbPath);
            if (!Directory.Exists(dbFolder))
                Directory.CreateDirectory(dbFolder);
            SQLiteConnection.CreateFile(dbPath);
            CreateLogBufferTable();
        }

        private void CreateLogBufferTable()
        {
            using (var disposer = new AutoDisposer())
            {
                var connection = disposer.Add(GetOpenConnection());
                var cmd = disposer.Add(connection.CreateCommand());
                cmd.CommandText = GenerateTableCreateScript();
                cmd.ExecuteNonQuery();
            }
        }

        private static string GenerateTableCreateScript()
        {
            return string.Format(@"create table {0}(
                                {1} integer primary key, 
                                {2} text not null, 
                                {3} int,
                                {4} datetime default current_timestamp);",
                DataConstants.TABLE,
                DataConstants.ID,
                DataConstants.DATA,
                DataConstants.RETRIES,
                DataConstants.CREATED);
        }

        private SQLiteConnection GetOpenConnection()
        {
            var sqLiteConnection = new SQLiteConnection(ConnectionString);
            return sqLiteConnection.OpenAndReturn();
        }

        private static string GetBufferDatabasePathForApplication()
        {
            // this only works for actual apps: NUnit running through R# will b0rk
            //  as there is no EntryAssembly, apparently
            var entryPath = Assembly.GetEntryAssembly().CodeBase;
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var bufferBase = Path.Combine(appDataPath, "LogBuffer");
            var md5 = MD5.Create();
            var dbName = string.Join("", md5.ComputeHash(Encoding.UTF8.GetBytes(entryPath))
                                .Select(b => b.ToString("X2"))) + ".db";
            return Path.Combine(bufferBase, dbName);
        }

        public List<LogBufferItem> ListBufferedLogItems()
        {
            var result = new List<LogBufferItem>();
            using (var disposer = new AutoDisposer())
            {
                var conn = disposer.Add(GetOpenConnection());
                var cmd = disposer.Add(conn.CreateCommand());
                cmd.CommandText = string.Format("select * from {0} order by {1} asc", DataConstants.TABLE, DataConstants.ID);
                var reader = disposer.Add(cmd.ExecuteReader());
                while (reader.Read())
                    result.Add(new LogBufferItem(reader));
            }
            return result;
        }
    }
}