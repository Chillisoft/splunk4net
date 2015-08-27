using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using splunk4net.Buffering;

namespace splunk4net.Tests
{
    [TestFixture]
    public class TestLogBufferItemRepositoryFactory
    {
        public class LogBufferItemRepositoryFactory_OVERRIDES_GetBufferDatabasePathForApplication: LogBufferItemRepositoryFactory
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
            var result = new LogBufferItemRepositoryFactory_OVERRIDES_GetBufferDatabasePathForApplication();
            result.OverridePath = overridePath;
            return result;
        }
    }
}
