using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using log4net.Core;
using Newtonsoft.Json;
using NUnit.Framework;
using Splunk.Client;

namespace splunk4net.Tests
{
    [TestFixture]
    public class Discovery
    {

        [SetUp]
        public void SetUp()
        {
            DisableSslCertificateExceptions();
        }

        private static void DisableSslCertificateExceptions()
        {
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, errors) => true;
        }

        [Test]
        [Ignore("Discovery: requires splunk server at localhost")]
        public async Task LogToLocalHost()
        {
            //---------------Set up test pack-------------------

            const string indexName = "splunk4net";
            var service = await CreateLoggedOnSplunkServiceForIndex(indexName);
            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            var transmitter = service.Transmitter;
            var someLog = new LoggingEventData()
            {
                Level = Level.Info,
                LoggerName = "SomeClassInSomeApplication",
                Message = "The time now is: " + DateTime.Now.ToString("HH:mm:ss"),
                TimeStamp = DateTime.Now
            };

            var json = JsonConvert.SerializeObject(someLog);

            var result = await transmitter.SendAsync(json, indexName);

            //---------------Test Result -----------------------
            var bytes = int.Parse(result.GetValue("bytes"));
            Assert.AreEqual(bytes, json.Length);
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

        [Test]
        [Ignore("Discovery: requires splunk server at localhost")]
        public async Task TryFindLogsAtLocalHost()
        {
            //---------------Set up test pack-------------------
            const string indexName = "splunk4net";
            var service = await CreateLoggedOnSplunkServiceForIndex(indexName);

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            var job = await service.Jobs.CreateAsync("search index=\"splunk4net\" LoggerName=\"SomeClassInSomeApplication\"");
            using (var stream = await job.GetSearchResultsAsync())
            {
                foreach (var result in stream.SearchResults())
                {
                    Console.WriteLine(result);
                }

            }


            //---------------Test Result -----------------------
            Console.WriteLine("OUTTA HERE");
        }

        [Test]
        [Ignore("Discovery: requires splunk server at localhost")]
        public async Task LogAndTestThatItHappened()
        {
            //---------------Set up test pack-------------------
            var index = "splunk4net";
            var loggingService = await CreateLoggedOnSplunkServiceForIndex(index);
            var queryService = await CreateLoggedOnSplunkServiceForIndex(index);

            var searchFor = Guid.NewGuid().ToString();

            var transmitter = loggingService.Transmitter;
            var someLog = new LoggingEventData()
            {
                Level = Level.Info,
                LoggerName = searchFor,
                Message = "The time now is: " + DateTime.Now.ToString("HH:mm:ss"),
                TimeStamp = DateTime.Now
            };

            var json = JsonConvert.SerializeObject(someLog);
            var result = await transmitter.SendAsync(json, index);
            Assert.IsNotNull(result);

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            var searchString = $"search index=\"{index}\" LoggerName=\"{searchFor}\"";
            var job = await queryService.Jobs.CreateAsync(searchString);
            using (var stream = await job.GetSearchResultsAsync())
            {
                var allResults = stream.SearchResults().ToArray();
                Assert.AreEqual(1, allResults.Length);
                var log = allResults.First();
                Console.WriteLine(log.SegmentedRaw.Value);
            }

            //---------------Test Result -----------------------
        }

    }


}
