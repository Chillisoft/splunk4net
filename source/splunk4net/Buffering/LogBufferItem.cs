using System;
using System.Data.SQLite;

namespace splunk4net.Buffering
{
    public class LogBufferItem
    {
        public int Id { get; private set;  }
        public string Data { get; private set; }
        public int? Retries { get; set; }
        public DateTime Created { get; private set; }
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
            return string.Format("insert into {0} ({1}) values ('{2}');",
                DataConstants.TABLE, DataConstants.DATA, quoted);
        }

    }
}