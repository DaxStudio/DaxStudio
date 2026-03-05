using System.Collections.Generic;

namespace DaxStudio.UI.Model
{
    /// <summary>
    /// Represents a group of structurally similar xmSQL queries.
    /// Members of a group share the same fingerprint hash.
    /// </summary>
    public class XmSqlQueryGroup
    {
        /// <summary>
        /// Unique identifier for this group (1-based).
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Human-readable label describing the group pattern.
        /// e.g., "SELECT [Value](SUM) FROM CME_Global_Data WHERE [Country],[Category],..."
        /// </summary>
        public string GroupLabel { get; set; }

        /// <summary>
        /// The full structural hash shared by all members.
        /// </summary>
        public string FullStructuralHash { get; set; }

        /// <summary>
        /// The table access hash shared by all members.
        /// </summary>
        public string TableAccessHash { get; set; }

        /// <summary>
        /// List of query IDs (RowNumbers) that belong to this group.
        /// </summary>
        public List<int> MemberQueryIds { get; set; } = new List<int>();

        /// <summary>
        /// Number of queries in this group.
        /// </summary>
        public int Count => MemberQueryIds.Count;

        /// <summary>
        /// The fingerprint details for this group (from the first member).
        /// </summary>
        public XmSqlQueryFingerprint Fingerprint { get; set; }
    }
}
