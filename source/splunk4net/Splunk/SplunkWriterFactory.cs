using System.Collections.Generic;
using System.Linq;
// ReSharper disable MemberCanBePrivate.Global

namespace splunk4net.Splunk
{
    public class SplunkWriterFactory: ISplunkWriterFactory
    {
        private readonly object _lock = new object();
        private readonly List<SplunkWriterConfig> _configurations = new List<SplunkWriterConfig>();
        public void RegisterConfigFor(string appenderName, 
            string indexName, 
            string remoteUrl, 
            string login, 
            string password)
        {
            lock(_lock)
            {
                _configurations.Add(new SplunkWriterConfig(appenderName, indexName, remoteUrl, login, password));
            }
        }

        // ReSharper disable once UnusedMember.Global
        public void ForgetConfigurations()
        {
            lock(_lock)
            {
                _configurations.Clear();
            }
        }

        public SplunkWriterConfig[] GetConfigurationsFor(string appenderName)
        {
            lock(_lock)
            {
                return _configurations.Where(p => appenderName.Like(p.AppenderName)).ToArray();
            }
        }

        public ISplunkWriter CreateFor(string appenderName)
        {
            return new SplunkWriter(GetConfigurationsFor(appenderName));
        }

    }
}