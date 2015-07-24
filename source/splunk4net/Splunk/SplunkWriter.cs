using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Splunk.Client;

namespace splunk4net.Splunk
{
    public interface ISplunkWriter
    {
        Task<bool> Log(string jsonData);
    }

    public class SplunkWriter: ISplunkWriter
    {
        public IEnumerable<SplunkWriterConfig> InstanceConfigurations { get { return _instanceConfigurations; }}
        private readonly SplunkWriterConfig[] _instanceConfigurations;

        public SplunkWriter(SplunkWriterConfig[] configurations)
        {
            _instanceConfigurations = configurations;
        }

        public async Task<bool> Log(string jsonData)
        {
            var success = false;
            foreach (var config in _instanceConfigurations)
            {
                try
                {
                    var service = await CreateSplunkServiceFor(config);
                    var transmitter = service.Transmitter;
                    await transmitter.SendAsync(jsonData, config.IndexName);
                    success = true;
                }
                catch (Exception)
                {
                    // one splunk log making it through marks success; allowing fail-over
                }
            }
            return success;
        }

        private async Task<Service> CreateSplunkServiceFor(SplunkWriterConfig config)
        {
            var service = new Service(new Uri(config.RemoteUrl));
            await service.LogOnAsync(config.Login, config.Password);
            // ensure that the index is alive and well
            var index = await service.Indexes.GetOrNullAsync(config.IndexName)
                        ?? await service.Indexes.CreateAsync(config.IndexName);
            await index.EnableAsync();
            return service;
        }
    }
}
