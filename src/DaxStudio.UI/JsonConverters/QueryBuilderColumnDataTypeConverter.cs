using Microsoft.AnalysisServices.Tabular;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.JsonConverters
{
    internal class QueryBuilderColumnDataTypeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return false;
        }
        
        public static Dictionary<Type, DataType> DataTypeLookup = new Dictionary<Type, DataType>() {
            { typeof(string), DataType.String },
            { typeof(int), DataType.Int64 },
            { typeof(long), DataType.Int64 },
            { typeof(Decimal), DataType.Decimal },
            { typeof(double), DataType.Double },
            { typeof(DateTime), DataType.DateTime},
            { typeof(bool), DataType.Boolean }
        };

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;
            else if (reader.TokenType == JsonToken.String)
            {
                var sysType = Type.GetType("System.Type");
                Type obj = (Type)serializer.Deserialize(reader, sysType);
                DataTypeLookup.TryGetValue(obj, out DataType dataType);
                return dataType;
                

                //return serializer.Deserialize(reader, obj);
            } 
            else
            {
                return serializer.Deserialize(reader, objectType);
            }
        }

        public override void WriteJson(JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
