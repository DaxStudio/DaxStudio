using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DaxStudio.UI.JsonConverters
{

    //public class InterfaceContractResolver<TInterface> : DefaultContractResolver where TInterface : class
    public class InterfaceContractResolver : DefaultContractResolver
    {
        //protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        //{
        //    IList<JsonProperty> properties = base.CreateProperties(typeof(TInterface), memberSerialization);
        //    return properties;
        //}

        private readonly Type[] _interfaceTypes;

        private readonly ConcurrentDictionary<Type, Type> _typeToSerializeMap;

        public InterfaceContractResolver(params Type[] interfaceTypes)
        {
            _interfaceTypes = interfaceTypes;

            _typeToSerializeMap = new ConcurrentDictionary<Type, Type>();
        }

        protected override IList<JsonProperty> CreateProperties(
            Type type,
            MemberSerialization memberSerialization)
        {
            var typeToSerialize = _typeToSerializeMap.GetOrAdd(
                type,
                t => _interfaceTypes.FirstOrDefault(
                    it => it.IsAssignableFrom(t)) ?? t);

            return base.CreateProperties(typeToSerialize, memberSerialization);
        }
    }
}
