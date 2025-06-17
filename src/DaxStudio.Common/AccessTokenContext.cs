using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Common
{
    public enum AccessTokenScope
    {
        PowerBI = 0,
        AsAzure = 1,
    }

    public class AccessTokenContext
    {
        public string UserName { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public AccessTokenScope TokenScope { get; set; }
    }
}
