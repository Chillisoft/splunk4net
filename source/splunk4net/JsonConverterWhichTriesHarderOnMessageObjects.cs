using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace splunk4net
{
    public class JsonConverterWhichTriesHarderOnMessageObjects : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var jToken = JToken.FromObject(value);
            try
            {
                // if the message object is complex, ensure that it's written heirachically, or fall back on whatever was there to start with
                var propInfo = value.GetType().GetProperty("MessageObject");
                var propVal = propInfo.GetValue(value);
                var replace = JObject.FromObject(propVal);
                jToken["Message"] = replace;
            }
            catch { }
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