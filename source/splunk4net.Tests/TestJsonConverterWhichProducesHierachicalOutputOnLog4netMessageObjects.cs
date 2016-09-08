using System.Globalization;
using log4net.Util;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using static PeanutButter.RandomGenerators.RandomValueGen;

namespace splunk4net.Tests
{
    public class TestJsonConverterWhichProducesHierachicalOutputOnLog4NetMessageObjects
    {
        private JsonConverterWhichProducesHierachicalOutputOnLog4NetMessageObjects Create()
        {
            return new JsonConverterWhichProducesHierachicalOutputOnLog4NetMessageObjects();
        }

        [Test]
        public void CanConvert_ShouldAlwaysReturnTrue()
        {
            //---------------Set up test pack-------------------
            // Create two new anonymous types which the SUT definitely can't know about
            var obj1 = new { };
            var obj2 = new { };
            var sut = Create();
            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            var result1 = sut.CanConvert(obj1.GetType());
            var result2 = sut.CanConvert(obj2.GetType());

            //---------------Test Result -----------------------
            Assert.IsTrue(result1);
            Assert.IsTrue(result2);
        }

        // These tests may not make a lot of sense. They are basically just enforcing the observed behaviour
        //  (ie, they are characterisation tests)
        [Test]
        public void CHARACTERISATION_WriteJson_WhenValueHasStringMessageObject_ShouldLeaveOriginalMessageAlone_ShouldEncode()
        {
            //---------------Set up test pack-------------------
            var messageData = new {
                Message = GetRandomString(1, 5),
                MessageObject = GetRandomString(6, 10)
            };
            var writer = Substitute.For<JsonWriter>();
            var serializer = Substitute.For<JsonSerializer>();
            var sut = Create();


            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            sut.WriteJson(writer, messageData, serializer);

            //---------------Test Result -----------------------
            Received.InOrder(() => {
                writer.WritePropertyName("Message");
                writer.WriteValue(messageData.Message);
                writer.WritePropertyName("MessageObject");
                writer.WriteValue(messageData.MessageObject);
            });
        }

        [Test]
        public void CHARACTERISATION_WriteJson_WhenValueHasObjectMessageObject_ShouldRewriteMessageToObjectValue()
        {
            //---------------Set up test pack-------------------
            var messageData = new {
                Message = GetRandomString(1, 5),
                MessageObject = new {
                    MooCow = GetRandomString(6, 10)
                }
            };
            var writer = Substitute.For<JsonWriter>();
            var serializer = Substitute.For<JsonSerializer>();
            var sut = Create();

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            sut.WriteJson(writer, messageData, serializer);

            //---------------Test Result -----------------------
            Received.InOrder(() => {
                writer.WritePropertyName("Message");
                writer.WriteValue(messageData.MessageObject.MooCow);
                writer.WriteValue(messageData.MessageObject.MooCow);
            });
        }

        [Test]
        public void CHARACTERISATION_WriteJson_WhenValueHasSystemStringFormatForMessageObject_ShouldRewriteMessageToStringRepresentationOfStringFormatValue()
        {
            //---------------Set up test pack-------------------
            var messageData = new {
                Message = GetRandomString(1, 5),
                MessageObject = new SystemStringFormat(CultureInfo.InvariantCulture, "{0} said the {1}", "moo", "cow")
            };
            var writer = Substitute.For<JsonWriter>();
            var serializer = Substitute.For<JsonSerializer>();
            var sut = Create();

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            sut.WriteJson(writer, messageData, serializer);

            //---------------Test Result -----------------------
            Received.InOrder(() => {
                writer.WritePropertyName("Message");
                writer.WriteValue("moo said the cow");
            });
        }


    }
}
