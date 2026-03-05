namespace DaxStudio.UI.Model
{
    /// <summary>
    /// Represents the structural fingerprint of a single xmSQL query.
    /// Used for grouping similar queries that share the same structure but differ in filter values or selected columns.
    /// </summary>
    public class XmSqlQueryFingerprint
    {
        /// <summary>
        /// Hash of the full query structure: SELECT columns + FROM + JOIN + WHERE columns (no values).
        /// Queries with the same FullStructuralHash are identical except for filter values.
        /// </summary>
        public string FullStructuralHash { get; set; }

        /// <summary>
        /// Hash of the table access pattern: FROM + JOIN + WHERE columns only (ignores SELECT).
        /// Queries with the same TableAccessHash access the same data with the same filters,
        /// but may request different columns.
        /// </summary>
        public string TableAccessHash { get; set; }

        /// <summary>
        /// Human-readable summary of the SELECT clause (e.g., "Value(SUM), Channel, PEP/ROM").
        /// </summary>
        public string SelectSignature { get; set; }

        /// <summary>
        /// Human-readable summary of the WHERE filter columns (e.g., "Country(=), Category(=), Channel(NIN)").
        /// </summary>
        public string WhereColumnsSignature { get; set; }

        /// <summary>
        /// Human-readable summary of the FROM + JOIN structure (e.g., "CME_Global_Data LEFT JOIN SpclPeriods").
        /// </summary>
        public string FromJoinSignature { get; set; }
    }
}
