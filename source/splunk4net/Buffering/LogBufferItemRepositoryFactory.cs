using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace splunk4net.Buffering
{
    public interface ILogBufferItemRepositoryFactory
    {
        ILogBufferItemRepository CreateRepository();
    }

    public class LogBufferItemRepositoryFactory : ILogBufferItemRepositoryFactory
    {
        protected virtual string GetBufferDatabasePathForApplication()
        {
            try
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
            catch (Exception ex)
            {
                Trace.WriteLine("Unable to get a path for buffering log items; buffering will occur in memory and will not survive a process restart");
                Trace.WriteLine("Actual error was: " + ex.Message);
                return null;
            }
        }

        public ILogBufferItemRepository CreateRepository()
        {
            var bufferPath = GetBufferDatabasePathForApplication();
            return bufferPath == null
                        ? (ILogBufferItemRepository) new LogBufferItemRepositoryInMemory()
                        : new LogBufferItemRepositorySqlite(bufferPath);
        }
    }
}
