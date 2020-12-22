using System;

namespace DaxStudio.UI.Extensions
{
    public static class ServerVersionExtensions
    {
        const string AggTableSupportMinVerStr = "15.0.1.681";
        // versions of the tabular engine >= 14 support filtering direct query end events by session id
        public static bool SupportsDirectQueryFilters(this string versionString)
        {
            Version version;
            Version.TryParse(versionString, out version);
            return (version != null) && (version.Major >= 14);
        }

        public static bool SupportsAggregateTables(this string versionString)
        {
            Version AggSupportMinVersion = new Version(AggTableSupportMinVerStr);
            Version version;
            Version.TryParse(versionString, out version);
            return (version != null) && (version >= AggSupportMinVersion);
        }
    }
}
