using DaxStudio.UI.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.JsonConverters
{
    public class SecureStringConverter : JsonConverter<SecureString>
    {
        public override SecureString ReadJson(JsonReader reader, Type objectType, SecureString existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string str = (string)reader.Value;
            SecureString secureString = new SecureString();
            if (str != null)
            {
                var pwd = str.Decrypt();
                foreach (char c in pwd)
                {
                    secureString.AppendChar(c);
                }
            }
            return secureString;
        }

        public override void WriteJson(JsonWriter writer, SecureString value, JsonSerializer serializer)
        {
            writer.WriteValue(value.GetInsecureString().Encrypt());
        }
    }
}
