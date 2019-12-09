using DaxStudio.Interfaces;
using DaxStudio.UI.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.JsonConverters
{
    class DaxFileConverter : JsonConverter<ObservableCollection<IDaxFile>>
    {
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override ObservableCollection<IDaxFile> ReadJson(JsonReader reader, Type objectType, ObservableCollection<IDaxFile> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JArray ja = JArray.Load(reader);
            //JObject jo = JObject.Load(reader);
            var coll = new ObservableCollection<IDaxFile>();
            foreach (var jo in ja.Children())
            {

                // Read the properties which will be used as constructor parameters
                string path = (string)jo["FullPath"];
                bool pinned = (bool?)jo["Pinned"]??false;
                var file = new DaxFile(path, pinned);
                coll.Add(file);
            }
            return coll;
            //if (!hasExistingValue) return null;
            //return new DaxFile(path, pinned);
        }

        public override void WriteJson(JsonWriter writer, ObservableCollection<IDaxFile> value, JsonSerializer serializer)
        {
            //use the default serialization - it works fine
            serializer.Serialize(writer, value);
        }
    }

    

}
