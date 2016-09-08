using System;
using System.Data.SQLite;
using System.Diagnostics.CodeAnalysis;

namespace splunk4net.Buffering
{
    [SuppressMessage("ReSharper", "UnusedMemberInSuper.Global")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public interface ILogBufferItem
    {
        int Id { get; }
        string Data { get; }
        int? Retries { get; set; }
        DateTime Created { get; }
    }

    public class LogBufferItem : ILogBufferItem
    {
        public int Id { get; }
        public string Data { get; }
        public int? Retries { get; set; }
        public DateTime Created { get; }
        public LogBufferItem(int id, string data, DateTime created)
        {
            Id = id;
            Data = data;
            Created = created;
        }

        public LogBufferItem(string data)
        {
            Data = data;
            Created = DateTime.UtcNow;
        }

        public LogBufferItem(SQLiteDataReader reader): 
            this(Convert.ToInt32(reader["id"].ToString()), 
                 reader["data"].ToString(), 
                 GetDateFrom(reader["created"]))
        {
        }

        private static DateTime GetDateFrom(object value)
        {
            DateTime result;
            DateTime.TryParse((value ?? new object()).ToString(), out result);
            return result;
        }

        public string AsInsertSql()
        {
            var quoted = (Data ?? "").Replace("'", "''");
            return $"insert into {DataConstants.TABLE} ({DataConstants.DATA}) values ('{quoted}');";
        }

    }
}