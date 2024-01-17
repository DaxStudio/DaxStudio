using DaxStudio.UI.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace DaxStudio.UI.JsonConverters
{
    public class ServerTimingConverter:JsonConverter
    {
        
        public override bool CanConvert(Type objectType)
        {
            return typeof(TraceStorageEngineEvent).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // load the json string
            var jsonObject = JObject.Load(reader);
            //jsonObject["QueryRichText"] = jsonObject["Query"];
            // instantiate the appropriate object based on the json string
            var target = Create(objectType, jsonObject);

            // populate the properties of the object
            serializer.Populate(jsonObject.CreateReader(), target);
            target.QueryRichText = target.Query;
            // return the object
            return target;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// The class that will create Animals when proper json objects are passed in
        /// </summary>
        /// <param name="objectType"></param>
        /// <param name="jsonObject"></param>
        /// <returns></returns>
        protected TraceStorageEngineEvent Create(Type objectType, JObject jsonObject)
        {
            // examine the $type value
            string eventClass =  jsonObject["Class"].ToString();

            // based on the $type, instantiate and return a new object
            switch (eventClass)
            {
                case "AggregateTableRewriteQuery":
                case "131":
                    return new RewriteTraceEngineEvent();
                
                default:
                    return new TraceStorageEngineEvent();
            }
        }
    }
}

