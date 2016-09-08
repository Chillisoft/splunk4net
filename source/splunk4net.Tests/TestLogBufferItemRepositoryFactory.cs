using System.IO;
using NUnit.Framework;
using splunk4net.Buffering;
// ReSharper disable MemberCanBePrivate.Global

namespace splunk4net.Tests
{
    [TestFixture]
    public class TestLogBufferItemRepositoryFactory
    {
#pragma warning disable S101 // Types should be named in camel case
        // ReSharper disable once InconsistentNaming
        public class LogBufferItemRepositoryFactory_OVERRIDES_GetBufferDatabasePathForApplication: LogBufferItemRepositoryFactory
#pragma warning restore S101 // Types should be named in camel case
        {
            public string OverridePath { get; set; }

            protected override string GetBufferDatabasePathForApplication()
            {
                return OverridePath;
            }
        }

        [Test]
        public void CreateRepository_WhenCanResolvePathToDatabase_ShouldReturn_InstanceOf_LogBufferItemRepositorySqlite()
        {
            //---------------Set up test pack-------------------
            var expected = Path.GetTempFileName();
            var sut = CreateWithOverridePath(expected);

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            var result = sut.CreateRepository();

            //---------------Test Result -----------------------
            Assert.IsNotNull(result);
            var concrete = result as LogBufferItemRepositorySqlite;
            Assert.IsNotNull(concrete);
            Assert.AreEqual(expected, concrete.BufferDatabasePath);
        }

        [Test]
        public void CreateRepository_WhenCannotResolvePathToDatabase_ShouldReturn_InstanceOf_LogBufferItemRepositoryInMemorye()
        {
            //---------------Set up test pack-------------------
            var sut = CreateWithOverridePath(null);

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            var result = sut.CreateRepository();

            //---------------Test Result -----------------------
            Assert.IsNotNull(result);
            Assert.IsNotNull(result as LogBufferItemRepositoryInMemory);
        }


        private LogBufferItemRepositoryFactory CreateWithOverridePath(string overridePath)
        {
            var result = new LogBufferItemRepositoryFactory_OVERRIDES_GetBufferDatabasePathForApplication {
                OverridePath = overridePath
            };
            return result;
        }
    }
}
