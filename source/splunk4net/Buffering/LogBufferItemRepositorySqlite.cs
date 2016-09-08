using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace splunk4net.Buffering
{
    public interface ILogBufferItemRepository
    {
        long Buffer(string data);
        void Unbuffer(long id);
        IEnumerable<ILogBufferItem> ListBufferedLogItems();
        void Trim(int maxRemaining);
    }

    public class LogBufferItemRepositorySqlite: ILogBufferItemRepository
    {
        public string ConnectionString => $"Data Source={BufferDatabasePath};Version=3";
        public string BufferDatabasePath => _dbPath;
        private readonly string _dbPath;
        private static readonly object _lock = new object();

        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public LogBufferItemRepositorySqlite(): this(GetBufferDatabasePathForApplication())
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
                UnbufferUnlocked(id);
            }
        }

        public IEnumerable<ILogBufferItem> ListBufferedLogItems()
        {
            var result = new List<ILogBufferItem>();
            using (var disposer = new AutoDisposer())
            {
                var conn = disposer.Add(GetOpenConnection());
                var cmd = disposer.Add(conn.CreateCommand());
                cmd.CommandText = $"select * from {DataConstants.TABLE} order by {DataConstants.ID} asc";
                var reader = disposer.Add(cmd.ExecuteReader());
                while (reader.Read())
                    result.Add(new LogBufferItem(reader));
            }
            return result;
        }

        public void Trim(int maxRemaining)
        {
            var currentIds = new List<long>();
            lock(_lock)
            {
                using (var disposer = new AutoDisposer())
                {
                    var conn = disposer.Add(GetOpenConnection());
                    var cmd = disposer.Add(conn.CreateCommand());
                    cmd.CommandText = $"select {DataConstants.ID} from {DataConstants.TABLE} order by {DataConstants.ID} desc;";
                    var reader = disposer.Add(cmd.ExecuteReader());
                    while (reader.Read())
                        currentIds.Add(Convert.ToInt32(reader[DataConstants.ID]));
                }
                currentIds.Skip(maxRemaining).ForEach(UnbufferUnlocked);
            }
        }

        private void UnbufferUnlocked(long id)
        {
            ExecuteNonQueryWith($"delete from {DataConstants.TABLE} where {DataConstants.ID} = {id}");
        }

        internal LogBufferItemRepositorySqlite(string path)
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
            if (dbFolder == null)
                throw new InvalidOperationException("Unable to obtain temporary folder for buffering database");
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
            return
                $@"create table {DataConstants.TABLE}(
                                {DataConstants.ID} integer primary key, 
                                {DataConstants
                    .DATA} text not null, 
                                {DataConstants.RETRIES} int,
                                {DataConstants
                        .CREATED} datetime default current_timestamp);";
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
    }

    public static class EnumerableExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> collection, Action<T> toRun)
        {
            foreach (var item in collection)
                toRun(item);
        }
    }
}