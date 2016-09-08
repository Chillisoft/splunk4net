using System;
using log4net.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace splunk4net
{
    public class JsonConverterWhichProducesHierachicalOutputOnLog4NetMessageObjects : JsonConverter
    {
        // log4net is designed more around flat data, but Splunk deals well with heirachies, so 
        //  this class ensures that complex objects which were logged in the calling app are
        //  logged at Splunk with their structure intact and available for query
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var jToken = JToken.FromObject(value);
            try
            {
                // if the message object is complex, ensure that it's written heirachically, or fall back on whatever was there to start with
                var propInfo = value.GetType().GetProperty("MessageObject");
                var propVal = propInfo.GetValue(value);
                var asStringFormat = propVal as SystemStringFormat;
                if (asStringFormat != null)
                {
                    jToken["Message"] = asStringFormat.ToString();
                }
                else
                {
                    var replace = JObject.FromObject(propVal);
                    jToken["Message"] = replace;
                }
            }
            catch
            {
                // this will happen for any simple type (eg string) -- in this case, we don't mind as the default serialization
                //  mechanism provides the expected results
            }
            jToken.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanConvert(Type objectType)
        {
            return true;
        }
    }
}