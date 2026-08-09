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
        Storage = 2,
    }

    internal enum EntraTokenAcquisitionMode
    {
        SilentThenInteractive = 0,
        SilentOnly = 1,
    }

    public class AccessTokenContext
    {
        public string Username { get; set; }
        internal string AccountIdentifier { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public AccessTokenScope TokenScope { get; set; }
        internal EntraTokenAcquisitionMode RenewalMode { get; set; }
        public string TenantId { get; set; }
        public string DomainPostfix { get; set; }
        public IEnumerable<string> Scope { get; set; }
    }
}
