using System;
using System.Collections.Generic;
using System.Linq;

namespace DaxStudio.UI.Model
{
    /// <summary>
    /// Represents the complete analysis of xmSQL queries from Server Timing events.
    /// This is the top-level container for all parsed table, column, and relationship information.
    /// </summary>
    public class XmSqlAnalysis
    {
        /// <summary>
        /// Dictionary of tables found in the xmSQL queries, keyed by table name.
        /// </summary>
        public Dictionary<string, XmSqlTableInfo> Tables { get; } = new Dictionary<string, XmSqlTableInfo>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// List of relationships (joins) found between tables.
        /// </summary>
        public List<XmSqlRelationship> Relationships { get; } = new List<XmSqlRelationship>();

        /// <summary>
        /// Total number of SE (Storage Engine) queries analyzed.
        /// </summary>
        public int TotalSEQueriesAnalyzed { get; set; }

        /// <summary>
        /// Number of SE queries that were successfully parsed.
        /// </summary>
        public int SuccessfullyParsedQueries { get; set; }

        /// <summary>
        /// Number of SE queries that failed to parse.
        /// </summary>
        public int FailedParseQueries { get; set; }

        /// <summary>
        /// Number of SE queries that were cache hits.
        /// These are not counted in StorageEngineQueryCount in Server Timings.
        /// </summary>
        public int CacheHitQueries { get; set; }

        /// <summary>
        /// Number of batch SE events.
        /// These are counted in StorageEngineQueryCount in Server Timings and are also parsed for the diagram.
        /// </summary>
        public int BatchEventCount { get; set; }

        /// <summary>
        /// Total CPU time (ms) across all SE queries.
        /// Used to calculate CPU percentage per table.
        /// </summary>
        public long TotalCpuTimeMs { get; set; }

        /// <summary>
        /// Total CPU time (ms) from VertiPaq Scan events only.
        /// This is the real CPU time - DirectQuery events don't report actual CPU.
        /// </summary>
        public long TotalScanCpuTimeMs { get; set; }

        /// <summary>
        /// Total duration (ms) from DirectQuery SQL events only.
        /// DirectQuery CPU values are unreliable (just copy Duration), so we only track duration.
        /// </summary>
        public long TotalDirectQueryDurationMs { get; set; }

        /// <summary>
        /// Number of DirectQuery SQL events.
        /// </summary>
        public int DirectQueryEventCount { get; set; }

        /// <summary>
        /// Number of VertiPaq Scan events (excludes cache hits).
        /// </summary>
        public int ScanEventCount { get; set; }

        /// <summary>
        /// Gets the count of unique tables found.
        /// </summary>
        public int UniqueTablesCount => Tables.Count;

        /// <summary>
        /// Gets the total count of unique columns across all tables.
        /// </summary>
        public int UniqueColumnsCount => Tables.Values.Sum(t => t.Columns.Count);

        /// <summary>
        /// Gets the count of unique relationships found.
        /// </summary>
        public int UniqueRelationshipsCount => Relationships.Count;

        /// <summary>
        /// Gets the count of DirectQuery tables.
        /// </summary>
        public int DirectQueryTableCount => Tables.Values.Count(t => t.IsDirectQuery);

        /// <summary>
        /// Gets the count of tables with callbacks.
        /// </summary>
        public int CallbackTableCount => Tables.Values.Count(t => t.HasCallbacks);

        /// <summary>
        /// Gets columns across all tables that have callbacks.
        /// </summary>
        public IEnumerable<(string TableName, XmSqlColumnInfo Column)> CallbackColumns =>
            Tables.SelectMany(t => t.Value.Columns.Values
                .Where(c => c.HasCallback)
                .Select(c => (t.Key, c)));

        /// <summary>
        /// Gets or adds a table to the analysis.
        /// </summary>
        public XmSqlTableInfo GetOrAddTable(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName)) return null;

            // Clean the table name (remove quotes if present)
            tableName = tableName.Trim().Trim('\'', '"');

            if (!Tables.TryGetValue(tableName, out var table))
            {
                table = new XmSqlTableInfo(tableName);
                Tables[tableName] = table;
            }
            return table;
        }

        /// <summary>
        /// Adds or updates a relationship between tables.
        /// Also checks for the reverse direction to avoid duplicate lines.
        /// </summary>
        public void AddRelationship(string fromTable, string fromColumn, string toTable, string toColumn, XmSqlJoinType joinType)
        {
            // Clean table/column names to match GetOrAddTable behavior
            fromTable = fromTable?.Trim().Trim('\'', '"');
            fromColumn = fromColumn?.Trim().Trim('[', ']');
            toTable = toTable?.Trim().Trim('\'', '"');
            toColumn = toColumn?.Trim().Trim('[', ']');

            // Check if this relationship already exists (in either direction)
            var existing = Relationships.FirstOrDefault(r =>
                (r.FromTable.Equals(fromTable, StringComparison.OrdinalIgnoreCase) &&
                 r.FromColumn.Equals(fromColumn, StringComparison.OrdinalIgnoreCase) &&
                 r.ToTable.Equals(toTable, StringComparison.OrdinalIgnoreCase) &&
                 r.ToColumn.Equals(toColumn, StringComparison.OrdinalIgnoreCase)) ||
                // Also check reverse direction to avoid duplicate lines
                (r.FromTable.Equals(toTable, StringComparison.OrdinalIgnoreCase) &&
                 r.FromColumn.Equals(toColumn, StringComparison.OrdinalIgnoreCase) &&
                 r.ToTable.Equals(fromTable, StringComparison.OrdinalIgnoreCase) &&
                 r.ToColumn.Equals(fromColumn, StringComparison.OrdinalIgnoreCase)));

            if (existing != null)
            {
                existing.HitCount++;
            }
            else
            {
                Relationships.Add(new XmSqlRelationship
                {
                    FromTable = fromTable,
                    FromColumn = fromColumn,
                    ToTable = toTable,
                    ToColumn = toColumn,
                    JoinType = joinType,
                    HitCount = 1
                });
            }
        }

        /// <summary>
        /// Mapping from query ID (RowNumber) to structural similarity group ID.
        /// Populated by XmSqlQueryGrouper after parsing.
        /// </summary>
        public Dictionary<int, int> QueryToStructuralGroup { get; set; } = new Dictionary<int, int>();

        /// <summary>
        /// Mapping from query ID (RowNumber) to table access similarity group ID.
        /// Populated by XmSqlQueryGrouper after parsing.
        /// </summary>
        public Dictionary<int, int> QueryToTableAccessGroup { get; set; } = new Dictionary<int, int>();

        /// <summary>
        /// Clears all analysis data.
        /// </summary>
        public void Clear()
        {
            Tables.Clear();
            Relationships.Clear();
            TotalSEQueriesAnalyzed = 0;
            SuccessfullyParsedQueries = 0;
            FailedParseQueries = 0;
            CacheHitQueries = 0;
            BatchEventCount = 0;
            QueryToStructuralGroup.Clear();
            QueryToTableAccessGroup.Clear();
        }
    }

    /// <summary>
    /// Represents information about a table found in xmSQL queries.
    /// </summary>
    public class XmSqlTableInfo
    {
        public XmSqlTableInfo(string tableName)
        {
            TableName = tableName;
        }

        /// <summary>
        /// The name of the table.
        /// </summary>
        public string TableName { get; }

        /// <summary>
        /// Dictionary of columns found in this table, keyed by column name.
        /// </summary>
        public Dictionary<string, XmSqlColumnInfo> Columns { get; } = new Dictionary<string, XmSqlColumnInfo>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Number of SE queries that referenced this table.
        /// </summary>
        public int HitCount { get; set; }

        /// <summary>
        /// Total estimated rows scanned across all SE queries referencing this table.
        /// </summary>
        public long TotalEstimatedRows { get; set; }

        /// <summary>
        /// Maximum estimated rows from a single SE query for this table.
        /// Useful for identifying expensive individual scans.
        /// </summary>
        public long MaxEstimatedRows { get; set; }

        /// <summary>
        /// Total duration (ms) of all SE queries referencing this table.
        /// </summary>
        public long TotalDurationMs { get; set; }

        /// <summary>
        /// Maximum duration (ms) of a single SE query for this table.
        /// </summary>
        public long MaxDurationMs { get; set; }

        /// <summary>
        /// Number of SE queries that hit the cache for this table (VertiPaqSEQueryCacheMatch).
        /// </summary>
        public int CacheHits { get; set; }

        /// <summary>
        /// Number of SE queries that missed the cache (actual scans) for this table.
        /// </summary>
        public int CacheMisses { get; set; }

        /// <summary>
        /// Number of distinct SE queries referencing this table.
        /// </summary>
        public int QueryCount { get; set; }

        /// <summary>
        /// Total CPU time (ms) across all SE queries for this table.
        /// </summary>
        public long TotalCpuTimeMs { get; set; }

        /// <summary>
        /// Total CPU time (ms) from VertiPaq Scan events only for this table.
        /// DirectQuery events don't report actual CPU, so we track them separately.
        /// </summary>
        public long TotalScanCpuTimeMs { get; set; }

        /// <summary>
        /// Total duration (ms) from DirectQuery SQL events only for this table.
        /// DirectQuery CPU values just copy Duration, so we only track duration.
        /// </summary>
        public long TotalDirectQueryDurationMs { get; set; }

        /// <summary>
        /// Total parallel duration (ms) across all SE queries for this table.
        /// NetParallelDuration shows the "saved" time from parallelism.
        /// </summary>
        public long TotalParallelDurationMs { get; set; }

        /// <summary>
        /// Maximum CPU factor seen for this table (CpuTime/Duration ratio).
        /// Higher values indicate more parallel execution.
        /// </summary>
        public double MaxCpuFactor { get; set; }

        /// <summary>
        /// Number of SE queries that showed parallel execution (CpuFactor > 1).
        /// </summary>
        public int ParallelQueryCount { get; set; }

        /// <summary>
        /// Whether this table appeared in a FROM clause (base table).
        /// </summary>
        public bool IsFromTable { get; set; }

        /// <summary>
        /// Whether this table appeared in a JOIN clause.
        /// </summary>
        public bool IsJoinedTable { get; set; }

        /// <summary>
        /// Whether this table was accessed via DirectQuery (vs. VertiPaq).
        /// </summary>
        public bool IsDirectQuery { get; set; }

        /// <summary>
        /// The source system for DirectQuery tables.
        /// </summary>
        public string DirectQuerySource { get; set; }

        /// <summary>
        /// Whether this table has columns with DAX callbacks.
        /// </summary>
        public bool HasCallbacks => Columns.Values.Any(c => c.HasCallback);

        /// <summary>
        /// SE Event Query IDs that reference this table.
        /// </summary>
        public HashSet<int> QueryIds { get; } = new HashSet<int>();

        /// <summary>
        /// Gets or adds a column to this table.
        /// </summary>
        public XmSqlColumnInfo GetOrAddColumn(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName)) return null;

            // Clean the column name (remove brackets if present)
            columnName = columnName.Trim().Trim('[', ']');

            if (!Columns.TryGetValue(columnName, out var column))
            {
                column = new XmSqlColumnInfo(columnName);
                Columns[columnName] = column;
            }
            return column;
        }

        /// <summary>
        /// Gets columns that are used in joins (relationship keys).
        /// </summary>
        public IEnumerable<XmSqlColumnInfo> JoinColumns => Columns.Values.Where(c => c.UsageTypes.HasFlag(XmSqlColumnUsage.Join));

        /// <summary>
        /// Gets columns that are filtered.
        /// </summary>
        public IEnumerable<XmSqlColumnInfo> FilteredColumns => Columns.Values.Where(c => c.UsageTypes.HasFlag(XmSqlColumnUsage.Filter));

        /// <summary>
        /// Gets columns that are selected/output.
        /// </summary>
        public IEnumerable<XmSqlColumnInfo> SelectedColumns => Columns.Values.Where(c => c.UsageTypes.HasFlag(XmSqlColumnUsage.Select));
    }

    /// <summary>
    /// Represents information about a column found in xmSQL queries.
    /// </summary>
    public class XmSqlColumnInfo
    {
        public XmSqlColumnInfo(string columnName)
        {
            ColumnName = columnName;
        }

        /// <summary>
        /// The name of the column.
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        /// How this column is used in queries (flags can be combined).
        /// </summary>
        public XmSqlColumnUsage UsageTypes { get; set; }

        /// <summary>
        /// Number of times this column was referenced.
        /// </summary>
        public int HitCount { get; set; }

        /// <summary>
        /// Aggregation functions applied to this column (SUM, COUNT, etc.)
        /// </summary>
        public HashSet<string> AggregationTypes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Filter values applied to this column (extracted from WHERE clauses).
        /// </summary>
        public List<string> FilterValues { get; } = new List<string>();

        /// <summary>
        /// Filter operators used with this column (=, IN, >, <, etc.)
        /// </summary>
        public HashSet<string> FilterOperators { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private int _totalFilterValueCount = 0;
        /// <summary>
        /// The total count of filter values (may be higher than FilterValues.Count if truncated).
        /// </summary>
        public int TotalFilterValueCount { 
            get { return _totalFilterValueCount > 0 ? _totalFilterValueCount : FilterValues.Count; }
            private set { _totalFilterValueCount = value; } 
        }

        /// <summary>
        /// Whether this column has a DAX callback (CallbackDataID or EncodeCallback).
        /// </summary>
        public bool HasCallback { get; set; }

        /// <summary>
        /// The type of callback on this column (if any).
        /// </summary>
        public string CallbackType { get; set; }

        /// <summary>
        /// Adds a usage type to this column.
        /// </summary>
        public void AddUsage(XmSqlColumnUsage usage)
        {
            UsageTypes |= usage;
            HitCount++;
        }

        /// <summary>
        /// Adds a filter value to this column.
        /// </summary>
        public void AddFilterValue(string value, string op = "=")
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                // Limit total filter values to avoid memory issues with large datasets
                if (FilterValues.Count < 50 && !FilterValues.Contains(value))
                {
                    FilterValues.Add(value);
                }
                if (!string.IsNullOrWhiteSpace(op))
                {
                    FilterOperators.Add(op.Trim());
                }
            }
        }

        /// <summary>
        /// Sets the total filter value count (used when the actual count exceeds the stored limit).
        /// </summary>
        public void SetTotalFilterValueCount(int count)
        {
            TotalFilterValueCount = count;
        }

        /// <summary>
        /// Adds an aggregation function to this column.
        /// </summary>
        public void AddAggregation(string aggregationType)
        {
            if (!string.IsNullOrWhiteSpace(aggregationType))
            {
                AggregationTypes.Add(aggregationType.Trim().ToUpperInvariant());
                UsageTypes |= XmSqlColumnUsage.Aggregate;
            }
        }
    }

    /// <summary>
    /// Represents a relationship (join) between two tables.
    /// </summary>
    public class XmSqlRelationship
    {
        /// <summary>
        /// The source table in the relationship.
        /// </summary>
        public string FromTable { get; set; }

        /// <summary>
        /// The column in the source table used for the join.
        /// </summary>
        public string FromColumn { get; set; }

        /// <summary>
        /// The target table in the relationship.
        /// </summary>
        public string ToTable { get; set; }

        /// <summary>
        /// The column in the target table used for the join.
        /// </summary>
        public string ToColumn { get; set; }

        /// <summary>
        /// The type of join (LEFT OUTER, INNER, etc.)
        /// </summary>
        public XmSqlJoinType JoinType { get; set; }

        /// <summary>
        /// Number of times this relationship was used.
        /// </summary>
        public int HitCount { get; set; }

        /// <summary>
        /// The cardinality of the relationship (OneToMany, ManyToMany, etc.)
        /// </summary>
        public XmSqlCardinality Cardinality { get; set; }

        /// <summary>
        /// The cross-filter direction (Single, Both).
        /// </summary>
        public XmSqlCrossFilterDirection CrossFilterDirection { get; set; }

        /// <summary>
        /// Whether this is an active relationship.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Returns a unique key for this relationship.
        /// </summary>
        public string Key => $"{FromTable}.{FromColumn}->{ToTable}.{ToColumn}";
    }

    /// <summary>
    /// Flags representing how a column is used in xmSQL queries.
    /// </summary>
    [Flags]
    public enum XmSqlColumnUsage
    {
        None = 0,
        Select = 1,      // Column appears in SELECT clause
        Filter = 2,      // Column appears in WHERE clause
        Join = 4,        // Column is used in a JOIN/ON clause
        GroupBy = 8,     // Column is used for grouping
        Aggregate = 16,  // Column has an aggregation function applied
        OrderBy = 32,    // Column is used in ORDER BY
        Expression = 64  // Column is used in a WITH $Expr expression (calculated measure)
    }

    /// <summary>
    /// Types of joins found in xmSQL.
    /// </summary>
    public enum XmSqlJoinType
    {
        Unknown,
        LeftOuterJoin,
        InnerJoin,
        RightOuterJoin,
        FullOuterJoin
    }

    /// <summary>
    /// Relationship cardinality types.
    /// </summary>
    public enum XmSqlCardinality
    {
        Unknown,
        OneToOne,
        OneToMany,
        ManyToOne,
        ManyToMany
    }

    /// <summary>
    /// Cross-filter direction for relationships.
    /// </summary>
    public enum XmSqlCrossFilterDirection
    {
        Unknown,
        Single,       // One-way filtering (standard)
        Both          // Bi-directional filtering
    }
}
