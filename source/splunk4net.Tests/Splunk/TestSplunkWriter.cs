using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using PeanutButter.RandomGenerators;
using splunk4net.Splunk;
using Splunk.Client;

namespace splunk4net.Tests
{
    [TestFixture]
    public class TestSplunkWriter
    {
        [SetUp]
        public void SetUp()
        {
            DisableSSLCertificateExceptions();
        }

        private static void DisableSSLCertificateExceptions()
        {
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, errors) => true;
        }


        [Test]
        public void RegisterConfigFor_GivenOneSpecificConfigurationWithNo_ShouldRegisterThatDataForWriterCreation()
        {
            //---------------Set up test pack-------------------
            var appender = RandomValueGen.GetRandomString(1, 10);
            var url = RandomValueGen.GetRandomHttpUrl();
            var indexName = RandomValueGen.GetRandomString(1, 10);
            var login = RandomValueGen.GetRandomString(1, 10);
            var password = RandomValueGen.GetRandomString(1, 10);
            var factory = new SplunkWriterFactory();

            factory.RegisterConfigFor(appender, indexName, url, login, password);

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            var sut = factory.CreateFor(appender) as SplunkWriter;

            //---------------Test Result -----------------------
            var configuredWith = sut.InstanceConfigurations.FirstOrDefault();
            Assert.IsNotNull(configuredWith);
            Assert.AreEqual(appender, configuredWith.AppenderName);
            Assert.AreEqual(url, configuredWith.RemoteUrl);
            Assert.AreEqual(indexName, configuredWith.IndexName);
            Assert.AreEqual(login, configuredWith.Login);
            Assert.AreEqual(password, configuredWith.Password);
        }

        public class ObjectWrapper
        {
            public ObjectWrapper(object toWrap)
            {
                Wrapped = toWrap;
            }

            public object Wrapped { get; private set; }
        }

        [Test]
        public void RegisterConfigFor_GivenCatchAllGlob_ShouldIncludeThatConfig()
        {
            //---------------Set up test pack-------------------
            var appender = RandomValueGen.GetRandomString(1, 10);
            var url = RandomValueGen.GetRandomHttpUrl();
            var indexName = RandomValueGen.GetRandomString(1, 10);
            var login = RandomValueGen.GetRandomString(1, 10);
            var password = RandomValueGen.GetRandomString(1, 10);
            var factory = new SplunkWriterFactory();

            //---------------Assert Precondition----------------
            factory.RegisterConfigFor("*", indexName, url, login, password);

            //---------------Execute Test ----------------------
            var sut = factory.CreateFor(appender) as SplunkWriter;

            //---------------Test Result -----------------------
            var configuredWith = sut.InstanceConfigurations.FirstOrDefault();
            Assert.IsNotNull(configuredWith);
            Assert.AreEqual("*", configuredWith.AppenderName);
            Assert.AreEqual(url, configuredWith.RemoteUrl);
            Assert.AreEqual(indexName, configuredWith.IndexName);
            Assert.AreEqual(login, configuredWith.Login);
            Assert.AreEqual(password, configuredWith.Password);
        }


        public class Simple
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public Guid Source { get; set; }
        }

        [Test]
        [Ignore("Integration: requires local instance of Splunk to work")]
        public async void Log_WhenCanConnectToSplunk_ShouldLogAccordingToProvidedConfiguration()
        {
            //---------------Set up test pack-------------------
            var indexName = "splunk4net";
            var factory = new SplunkWriterFactory();
            factory.RegisterConfigFor("*", indexName, "https://localhost:8089", "admin", "P4$$w0rd");
            var appender = RandomValueGen.GetRandomString(1, 10);
            var sut = factory.CreateFor(appender);
            var someData = new Simple()
            {
                Id = RandomValueGen.GetRandomInt(1, 100),
                Name = RandomValueGen.GetRandomString(1, 10),
                Source = Guid.NewGuid()
            };
            var serialized = JsonConvert.SerializeObject(someData);
            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            await sut.Log(serialized);

            //---------------Test Result -----------------------
            var service = await CreateLoggedOnSplunkServiceForIndex(indexName);
            var results = await GetResultsFor(service, "search index=" + indexName + " Source=" + someData.Source, 1);
            Assert.AreEqual(1, results.Length);
            var log = results.First();
            var actual = JsonConvert.DeserializeObject<Simple>(log.SegmentedRaw.Value);
            Assert.IsNotNull(actual);
            Assert.AreEqual(someData.Id, actual.Id);
            Assert.AreEqual(someData.Name, actual.Name);
        }

        private async Task<SearchResult[]> GetResultsFor(Service service, string search, int expected)
        {
            // Splunk appears to buffer for a while before results can be queried
            for (var i = 0; i < 10; i++)
            {
                var job = await service.Jobs.CreateAsync(search);
                using (var stream = await job.GetSearchResultsAsync())
                {
                    var potentialResults = stream.SearchResults().ToArray();
                    if (potentialResults.Length == expected)
                        return potentialResults;
                }
                Thread.Sleep(1000);
            }
            throw new Exception(string.Format("timed out looking for {0} results for search '{1}'", expected, search));
        }

        private static async Task<Service> CreateLoggedOnSplunkServiceForIndex(string indexName)
        {
            var service = new Service(new Uri("https://localhost:8089"));
            await service.LogOnAsync("admin", "P4$$w0rd");
            // ensure that the index is alive and well
            var index = await service.Indexes.GetOrNullAsync(indexName)
                        ?? await service.Indexes.CreateAsync(indexName);
            await index.EnableAsync();
            return service;
        }

    }
}
