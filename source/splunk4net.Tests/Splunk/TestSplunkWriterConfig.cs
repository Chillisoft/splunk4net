using NUnit.Framework;
using PeanutButter.RandomGenerators;
using splunk4net.Splunk;

namespace splunk4net.Tests.Splunk
{
    [TestFixture]
    public class TestSplunkWriterConfig
    {
        [Test]
        public void Construct_ShouldCopyParametersToProperties()
        {
            //---------------Set up test pack-------------------
            var index = RandomValueGen.GetRandomString();
            var appender = RandomValueGen.GetRandomString();
            var url = RandomValueGen.GetRandomHttpUrl();
            var login = RandomValueGen.GetRandomString();
            var password = RandomValueGen.GetRandomString();

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            var sut = new SplunkWriterConfig(appender, index, url, login, password);

            //---------------Test Result -----------------------
            Assert.AreEqual(appender, sut.AppenderName);
            Assert.AreEqual(index, sut.IndexName);
            Assert.AreEqual(url, sut.RemoteUrl);
            Assert.AreEqual(login, sut.Login);
            Assert.AreEqual(password, sut.Password);
        }
    }
}