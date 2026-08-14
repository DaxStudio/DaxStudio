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

    public class AccessTokenContext
    {
        public string Username { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public AccessTokenScope TokenScope { get; set; }
        public string TenantId { get; set; }
        public string DomainPostfix { get; set; }
        public IEnumerable<string> Scope { get; set; }

        /// <summary>
        /// The MSAL <c>HomeAccountId.Identifier</c> of the account this token was issued to. Renewal
        /// is bound to this rather than to the username, so a long-running job cannot drift onto a
        /// different identity. Public (not internal) so Newtonsoft serializes it - if this were lost
        /// in a round-trip the renewal would silently fall back to interactive.
        /// </summary>
        public string AccountIdentifier { get; set; }

        /// <summary>
        /// Whether a token renewal is permitted to prompt. Carried on the context because renewal
        /// happens inside the ADOMD/TOM client callback, long after the original command settings
        /// have gone out of scope.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public TokenRenewalMode RenewalMode { get; set; } = TokenRenewalMode.AllowInteractive;
    }

    /// <summary>
    /// Controls whether an expiring token may be renewed interactively.
    /// </summary>
    public enum TokenRenewalMode
    {
        AllowInteractive = 0,
        SilentOnly = 1,
    }

}
