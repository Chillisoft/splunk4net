using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace splunk4net
{
    internal class MyResolver : DefaultContractResolver, IContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            return type.GetProperties()
                .Select(pi => new JsonProperty()
                {
                    PropertyName = pi.Name,
                    PropertyType = pi.PropertyType,
                    Readable = true,
                    Writable = true,
                    ValueProvider = base.CreateMemberValueProvider(type.GetMember(pi.Name).First())
                }).ToList();
        }

        protected override JsonISerializableContract CreateISerializableContract(Type objectType)
        {
            var jsonISerializableContract = base.CreateISerializableContract(objectType);
            return jsonISerializableContract;
        }
    }
}