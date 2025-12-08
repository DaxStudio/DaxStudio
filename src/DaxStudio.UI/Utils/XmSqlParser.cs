using DaxStudio.UI.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DaxStudio.UI.Utils
{
    /// <summary>
    /// Parses xmSQL queries from Server Timing events to extract table, column, and relationship information.
    /// Includes lineage tracking to resolve temporary/intermediate tables back to physical tables.
    /// </summary>
    public class XmSqlParser
    {
        // Regex patterns for parsing xmSQL

        // Matches table and column references like 'TableName'[ColumnName] or 'Table Name'[Column Name]
        private static readonly Regex TableColumnPattern = new Regex(
            @"'(?<table>[^']+)'\s*\[(?<column>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches standalone table references like 'TableName' (without column)
        private static readonly Regex StandaloneTablePattern = new Regex(
            @"(?<!\[)'(?<table>[^']+)'(?!\s*\[)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches FROM clause - captures the table name after FROM
        private static readonly Regex FromClausePattern = new Regex(
            @"\bFROM\s+'(?<table>[^']+)'",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches LEFT OUTER JOIN clause
        private static readonly Regex LeftOuterJoinPattern = new Regex(
            @"\bLEFT\s+OUTER\s+JOIN\s+'(?<table>[^']+)'",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches INNER JOIN clause
        private static readonly Regex InnerJoinPattern = new Regex(
            @"\bINNER\s+JOIN\s+'(?<table>[^']+)'",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches ON clause for join conditions: ON 'Table1'[Col1]='Table2'[Col2]
        // Also handles ON 'Table1'[Col1] = 'Table2'[Col2] (with spaces)
        private static readonly Regex OnClausePattern = new Regex(
            @"\bON\s+'(?<fromTable>[^']+)'\s*\[(?<fromColumn>[^\]]+)\]\s*=\s*'(?<toTable>[^']+)'\s*\[(?<toColumn>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches WHERE clause start
        private static readonly Regex WhereClausePattern = new Regex(
            @"\bWHERE\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches SELECT clause - everything between SELECT and FROM
        private static readonly Regex SelectClausePattern = new Regex(
            @"\bSELECT\b(?<columns>.*?)\bFROM\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Matches aggregation functions: SUM(...), COUNT(...), DCOUNT(...), MIN(...), MAX(...)
        private static readonly Regex AggregationPattern = new Regex(
            @"\b(?<agg>SUM|COUNT|DCOUNT|MIN|MAX|AVG|SUMSQR)\s*\(\s*(?:'(?<table>[^']+)'\s*\[(?<column>[^\]]+)\]|\s*\(\s*\))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches COUNT() without column (count all rows)
        private static readonly Regex CountAllPattern = new Regex(
            @"\bCOUNT\s*\(\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches VAND / VOR for additional filter conditions
        private static readonly Regex VandVorPattern = new Regex(
            @"\b(?:VAND|VOR)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // ==================== FILTER VALUE EXTRACTION PATTERNS ====================

        // Matches equality comparisons: 'Table'[Column] = value or 'Table'[Column] = 'string value'
        // Handles: 'table'[col] = 'value', 'table'[col] = 123, 'table'[col] = '2025/09'
        private static readonly Regex FilterEqualityPattern = new Regex(
            @"'(?<table>[^']+)'\s*\[(?<column>[^\]]+)\]\s*(?<op>=|<>|!=|>=|<=|>|<)\s*(?<value>'[^']*'|""[^""]*""|-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches IN clause with spaces: 'Table'[Column] IN ( 'val1', 'val2', ... )
        // Handles spacing variations like IN ( val ) or IN (val)
        private static readonly Regex FilterInPattern = new Regex(
            @"'(?<table>[^']+)'\s*\[(?<column>[^\]]+)\]\s+IN\s*\(\s*(?<values>[^)]+)\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches BETWEEN clause: 'Table'[Column] BETWEEN value1 AND value2
        private static readonly Regex FilterBetweenPattern = new Regex(
            @"'(?<table>[^']+)'\s*\[(?<column>[^\]]+)\]\s+BETWEEN\s+(?<val1>'[^']*'|""[^""]*""|-?\d+(?:\.\d+)?)\s+AND\s+(?<val2>'[^']*'|""[^""]*""|-?\d+(?:\.\d+)?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches COALESCE comparisons: PFCASTCOALESCE ( 'Table'[Column] AS TYPE ) > COALESCE ( value )
        // or COALESCE ( 'Table'[Column] ) > value
        private static readonly Regex FilterCoalescePattern = new Regex(
            @"(?:PFCASTCOALESCE|COALESCE)\s*\(\s*'(?<table>[^']+)'\s*\[(?<column>[^\]]+)\](?:\s+AS\s+\w+)?\s*\)\s*(?<op>=|<>|!=|>=|<=|>|<)\s*(?:COALESCE\s*\(\s*)?(?<value>'[^']*'|""[^""]*""|-?\d+(?:\.\d+)?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // ==================== CALLBACK DETECTION PATTERNS ====================

        // Matches CallbackDataID - indicates DAX callback for the column
        // Examples: 'Table'[Column] CALLBACKDATAID, CALLBACKDATAID('Table'[Column])
        private static readonly Regex CallbackDataIdPattern = new Regex(
            @"CALLBACKDATAID\s*\(?(?:\s*'(?<table>[^']+)'\s*\[(?<column>[^\]]+)\])?\)?|'(?<table2>[^']+)'\s*\[(?<column2>[^\]]+)\]\s+CALLBACKDATAID",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches ENCODECALLBACK - another callback pattern
        private static readonly Regex EncodeCallbackPattern = new Regex(
            @"ENCODECALLBACK\s*\(\s*'(?<table>[^']+)'\s*\[(?<column>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches CALLBACK in general context
        private static readonly Regex GenericCallbackPattern = new Regex(
            @"CALLBACK.*?'(?<table>[^']+)'\s*\[(?<column>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // ==================== CARDINALITY/RELATIONSHIP PATTERNS ====================

        // Matches MANYTOMANY in CREATE SHALLOW RELATION
        private static readonly Regex ManyToManyPattern = new Regex(
            @"CREATE\s+SHALLOW\s+RELATION.*?MANYTOMANY",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Enhanced CREATE SHALLOW RELATION to capture cardinality hints
        private static readonly Regex CreateShallowRelationExtendedPattern = new Regex(
            @"CREATE\s+SHALLOW\s+RELATION\s+'(?<relationName>[^']+)'.*?(?<manyToMany>MANYTOMANY)?.*?FROM\s+'(?<fromTable>[^']+)'\s*\[(?<fromColumn>[^\]]+)\].*?TO\s+'(?<toTable>[^']+)'\s*\[(?<toColumn>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Matches BOTH direction indicators
        private static readonly Regex BothDirectionPattern = new Regex(
            @"\bBOTH\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // ==================== LINEAGE TRACKING PATTERNS ====================

        // Matches DEFINE TABLE '$TTableX' := ... blocks
        // Captures the temp table name and the definition body
        private static readonly Regex DefineTablePattern = new Regex(
            @"DEFINE\s+TABLE\s+'(?<tempTable>\$T[^']+)'\s*:=\s*(?<definition>.*?)(?=DEFINE\s+TABLE|CREATE\s+SHALLOW|REDUCED\s+BY|$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Matches REDUCED BY clause (contains nested temp table definitions)
        private static readonly Regex ReducedByPattern = new Regex(
            @"REDUCED\s+BY\s*'(?<tempTable>\$T[^']+)'\s*:=\s*(?<definition>.*?)(?=;|$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Matches CREATE SHALLOW RELATION definitions
        private static readonly Regex CreateShallowRelationPattern = new Regex(
            @"CREATE\s+SHALLOW\s+RELATION\s+'(?<relationName>[^']+)'.*?FROM\s+'(?<fromTable>[^']+)'\s*\[(?<fromColumn>[^\]]+)\].*?TO\s+'(?<toTable>[^']+)'\s*\[(?<toColumn>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Matches ININDEX operator: 'Table'[Column] ININDEX '$TTableX'[$SemijoinProjection]
        private static readonly Regex InIndexPattern = new Regex(
            @"'(?<physTable>[^']+)'\s*\[(?<physColumn>[^\]]+)\]\s+ININDEX\s+'(?<tempTable>\$T[^']+)'\s*\[(?<tempColumn>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches REVERSE BITMAP JOIN: REVERSE BITMAP JOIN 'Table' ON 'TempTable'[Col]='Table'[Col]
        private static readonly Regex ReverseBitmapJoinPattern = new Regex(
            @"REVERSE\s+BITMAP\s+JOIN\s+'(?<table>[^']+)'\s+ON\s+'(?<leftTable>[^']+)'\s*\[(?<leftCol>[^\]]+)\]\s*=\s*'(?<rightTable>[^']+)'\s*\[(?<rightCol>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches column references with $ naming convention: 'SourceTable$ColumnName' -> SourceTable, ColumnName
        private static readonly Regex TempColumnNamePattern = new Regex(
            @"^(?<sourceTable>[^\$]+)\$(?<sourceColumn>.+)$",
            RegexOptions.Compiled);

        // ==================== LINEAGE DATA STRUCTURES ====================

        /// <summary>
        /// Tracks lineage of temporary tables back to their source physical tables.
        /// Key: temp table name (e.g., "$TTable1")
        /// Value: TempTableLineage containing source tables and column mappings
        /// </summary>
        private Dictionary<string, TempTableLineage> _tempTableLineage;

        /// <summary>
        /// Represents lineage information for a temporary table.
        /// </summary>
        private class TempTableLineage
        {
            public string TempTableName { get; set; }
            public HashSet<string> SourcePhysicalTables { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> SourceTempTables { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, ColumnLineage> ColumnMappings { get; } = new Dictionary<string, ColumnLineage>(StringComparer.OrdinalIgnoreCase);
            
            /// <summary>
            /// Filter values defined in the WHERE clause of this temp table's DEFINE statement.
            /// Key: physical column in format "Table.Column", Value: list of filter values
            /// </summary>
            public Dictionary<string, List<string>> FilterValues { get; } = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            
            /// <summary>
            /// Filter operators used for each column.
            /// </summary>
            public Dictionary<string, string> FilterOperators { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Represents lineage information for a column in a temp table.
        /// </summary>
        private class ColumnLineage
        {
            public string TempColumnName { get; set; }
            public string SourceTable { get; set; }
            public string SourceColumn { get; set; }
        }

        // Stores the estimated rows for the current query being parsed
        private long? _currentQueryEstimatedRows;
        
        // Stores the duration for the current query being parsed
        private long? _currentQueryDurationMs;

        // Stores whether current query was a cache hit
        private bool _currentQueryIsCacheHit;

        // Stores parallelism metrics for current query
        private long? _currentQueryCpuTimeMs;
        private double? _currentQueryCpuFactor;
        private long? _currentQueryNetParallelDurationMs;

        // Stores the current query ID (row number from Server Timings)
        private int _currentQueryId;

        /// <summary>
        /// Extended SE event metrics for parsing.
        /// </summary>
        public class SeEventMetrics
        {
            public long? EstimatedRows { get; set; }
            public long? DurationMs { get; set; }
            public bool IsCacheHit { get; set; }
            public long? CpuTimeMs { get; set; }
            public double? CpuFactor { get; set; }
            public long? NetParallelDurationMs { get; set; }
            public int QueryId { get; set; }
        }

        /// <summary>
        /// Parses a single xmSQL query and adds the results to the analysis.
        /// </summary>
        /// <param name="xmSql">The xmSQL query text to parse.</param>
        /// <param name="analysis">The analysis object to add results to.</param>
        /// <param name="estimatedRows">Optional estimated rows returned by this SE query.</param>
        /// <param name="durationMs">Optional duration in milliseconds of this SE query.</param>
        /// <returns>True if parsing was successful, false otherwise.</returns>
        public bool ParseQuery(string xmSql, XmSqlAnalysis analysis, long? estimatedRows = null, long? durationMs = null)
        {
            return ParseQueryWithMetrics(xmSql, analysis, new SeEventMetrics
            {
                EstimatedRows = estimatedRows,
                DurationMs = durationMs,
                IsCacheHit = false,
                CpuTimeMs = null,
                CpuFactor = null,
                NetParallelDurationMs = null
            });
        }

        /// <summary>
        /// Parses a single xmSQL query with full SE event metrics.
        /// </summary>
        /// <param name="xmSql">The xmSQL query text to parse.</param>
        /// <param name="analysis">The analysis object to add results to.</param>
        /// <param name="metrics">SE event metrics including cache hit, parallelism data.</param>
        /// <returns>True if parsing was successful, false otherwise.</returns>
        public bool ParseQueryWithMetrics(string xmSql, XmSqlAnalysis analysis, SeEventMetrics metrics)
        {
            if (string.IsNullOrWhiteSpace(xmSql))
                return false;

            try
            {
                analysis.TotalSEQueriesAnalyzed++;
                _currentQueryEstimatedRows = metrics?.EstimatedRows;
                _currentQueryDurationMs = metrics?.DurationMs;
                _currentQueryIsCacheHit = metrics?.IsCacheHit ?? false;
                _currentQueryCpuTimeMs = metrics?.CpuTimeMs;
                _currentQueryCpuFactor = metrics?.CpuFactor;
                _currentQueryNetParallelDurationMs = metrics?.NetParallelDurationMs;
                _currentQueryId = metrics?.QueryId ?? 0;

                // Initialize lineage tracking for this query
                _tempTableLineage = new Dictionary<string, TempTableLineage>(StringComparer.OrdinalIgnoreCase);

                // First pass: Build temp table lineage map
                BuildTempTableLineage(xmSql);

                // Parse FROM clause (base table)
                ParseFromClause(xmSql, analysis);

                // Parse SELECT clause (selected columns)
                ParseSelectClause(xmSql, analysis);

                // Parse JOIN clauses and relationships
                ParseJoinClauses(xmSql, analysis);

                // Parse ON clauses for relationship details (with lineage resolution)
                ParseOnClauses(xmSql, analysis);

                // Parse WHERE clause (filtered columns)
                ParseWhereClause(xmSql, analysis);

                // Parse ININDEX operations (filter relationships via temp tables)
                ParseInIndexOperations(xmSql, analysis);

                // Parse CREATE SHALLOW RELATION definitions
                ParseShallowRelations(xmSql, analysis);

                // Parse aggregations
                ParseAggregations(xmSql, analysis);

                // Parse callback columns (DAX callbacks)
                ParseCallbacks(xmSql, analysis);

                analysis.SuccessfullyParsedQueries++;
                
                // Track cache hits separately (they don't count toward SE query count in Server Timings)
                if (_currentQueryIsCacheHit)
                {
                    analysis.CacheHitQueries++;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to parse xmSQL query: {Query}", xmSql.Substring(0, Math.Min(100, xmSql.Length)));
                analysis.FailedParseQueries++;
                return false;
            }
        }

        /// <summary>
        /// Parses the FROM clause to identify the base table.
        /// Skips temp tables - only tracks physical tables.
        /// </summary>
        private void ParseFromClause(string xmSql, XmSqlAnalysis analysis)
        {
            var matches = FromClausePattern.Matches(xmSql);
            foreach (Match match in matches)
            {
                var tableName = match.Groups["table"].Value;
                
                // Skip temp tables - we only want physical tables in our analysis
                if (IsTempTable(tableName))
                    continue;
                    
                var table = analysis.GetOrAddTable(tableName);
                if (table != null)
                {
                    table.IsFromTable = true;
                    table.HitCount++;
                    
                    // Track query count (distinct queries hitting this table)
                    table.QueryCount++;
                    
                    // Track cache hits/misses
                    if (_currentQueryIsCacheHit)
                    {
                        table.CacheHits++;
                    }
                    else
                    {
                        table.CacheMisses++;
                    }
                    
                    // Track estimated rows for this table
                    if (_currentQueryEstimatedRows.HasValue && _currentQueryEstimatedRows.Value > 0)
                    {
                        table.TotalEstimatedRows += _currentQueryEstimatedRows.Value;
                        if (_currentQueryEstimatedRows.Value > table.MaxEstimatedRows)
                        {
                            table.MaxEstimatedRows = _currentQueryEstimatedRows.Value;
                        }
                    }
                    
                    // Track duration for this table
                    if (_currentQueryDurationMs.HasValue && _currentQueryDurationMs.Value > 0)
                    {
                        table.TotalDurationMs += _currentQueryDurationMs.Value;
                        if (_currentQueryDurationMs.Value > table.MaxDurationMs)
                        {
                            table.MaxDurationMs = _currentQueryDurationMs.Value;
                        }
                    }
                    
                    // Track parallelism metrics
                    if (_currentQueryCpuTimeMs.HasValue && _currentQueryCpuTimeMs.Value > 0)
                    {
                        table.TotalCpuTimeMs += _currentQueryCpuTimeMs.Value;
                    }
                    
                    if (_currentQueryNetParallelDurationMs.HasValue && _currentQueryNetParallelDurationMs.Value > 0)
                    {
                        table.TotalParallelDurationMs += _currentQueryNetParallelDurationMs.Value;
                    }
                    
                    if (_currentQueryCpuFactor.HasValue && _currentQueryCpuFactor.Value > 1.0)
                    {
                        // This query ran in parallel (CpuFactor > 1)
                        table.ParallelQueryCount++;
                        if (_currentQueryCpuFactor.Value > table.MaxCpuFactor)
                        {
                            table.MaxCpuFactor = _currentQueryCpuFactor.Value;
                        }
                    }
                    
                    // Track which query IDs accessed this table (for Query Plan Integration)
                    if (_currentQueryId > 0)
                    {
                        table.QueryIds.Add(_currentQueryId);
                    }
                }
            }
        }

        /// <summary>
        /// Parses the SELECT clause to identify selected columns.
        /// Resolves temp table column references to physical tables.
        /// </summary>
        private void ParseSelectClause(string xmSql, XmSqlAnalysis analysis)
        {
            var selectMatch = SelectClausePattern.Match(xmSql);
            if (selectMatch.Success)
            {
                var selectClause = selectMatch.Groups["columns"].Value;
                
                // Find all table[column] references in the SELECT clause
                var columnMatches = TableColumnPattern.Matches(selectClause);
                foreach (Match match in columnMatches)
                {
                    var tableName = match.Groups["table"].Value;
                    var columnName = match.Groups["column"].Value;

                    // Resolve temp table references to physical tables
                    var resolved = ResolveToPhysicalColumn(tableName, columnName);
                    if (resolved.HasValue && !IsTempTable(resolved.Value.Table))
                    {
                        tableName = resolved.Value.Table;
                        columnName = resolved.Value.Column;
                    }
                    else if (IsTempTable(tableName))
                    {
                        // Couldn't resolve and it's a temp table - skip
                        continue;
                    }

                    var table = analysis.GetOrAddTable(tableName);
                    if (table != null)
                    {
                        var column = table.GetOrAddColumn(columnName);
                        column?.AddUsage(XmSqlColumnUsage.Select);
                    }
                }
            }
        }

        /// <summary>
        /// Parses JOIN clauses to identify joined tables.
        /// Skips temp tables - only tracks physical tables.
        /// </summary>
        private void ParseJoinClauses(string xmSql, XmSqlAnalysis analysis)
        {
            // Parse LEFT OUTER JOINs
            var leftJoinMatches = LeftOuterJoinPattern.Matches(xmSql);
            foreach (Match match in leftJoinMatches)
            {
                var tableName = match.Groups["table"].Value;
                
                // Skip temp tables
                if (IsTempTable(tableName))
                    continue;
                    
                var table = analysis.GetOrAddTable(tableName);
                if (table != null)
                {
                    table.IsJoinedTable = true;
                    table.HitCount++;
                }
            }

            // Parse INNER JOINs
            var innerJoinMatches = InnerJoinPattern.Matches(xmSql);
            foreach (Match match in innerJoinMatches)
            {
                var tableName = match.Groups["table"].Value;
                
                // Skip temp tables
                if (IsTempTable(tableName))
                    continue;
                    
                var table = analysis.GetOrAddTable(tableName);
                if (table != null)
                {
                    table.IsJoinedTable = true;
                    table.HitCount++;
                }
            }
        }

        /// <summary>
        /// Parses ON clauses to extract relationship details.
        /// Uses lineage resolution to convert temp table references to physical tables.
        /// </summary>
        private void ParseOnClauses(string xmSql, XmSqlAnalysis analysis)
        {
            // Determine the join type by looking at what precedes each ON clause
            var onMatches = OnClausePattern.Matches(xmSql);
            Log.Debug("ParseOnClauses: Found {Count} ON clause matches", onMatches.Count);
            
            foreach (Match match in onMatches)
            {
                var fromTable = match.Groups["fromTable"].Value;
                var fromColumn = match.Groups["fromColumn"].Value;
                var toTable = match.Groups["toTable"].Value;
                var toColumn = match.Groups["toColumn"].Value;

                Log.Debug("ParseOnClauses: {FromTable}[{FromCol}] = {ToTable}[{ToCol}]",
                    fromTable, fromColumn, toTable, toColumn);

                // Determine join type by looking at text before the ON
                var textBeforeOn = xmSql.Substring(0, match.Index);
                var joinType = DetermineJoinType(textBeforeOn);

                // Resolve temp table references to physical tables using lineage
                var resolvedFrom = ResolveToPhysicalColumn(fromTable, fromColumn);
                var resolvedTo = ResolveToPhysicalColumn(toTable, toColumn);

                // Use resolved physical tables if available, otherwise use original
                var physFromTable = resolvedFrom?.Table ?? fromTable;
                var physFromColumn = resolvedFrom?.Column ?? fromColumn;
                var physToTable = resolvedTo?.Table ?? toTable;
                var physToColumn = resolvedTo?.Column ?? toColumn;

                // Skip if either side is still a temp table (couldn't resolve)
                if (IsTempTable(physFromTable) || IsTempTable(physToTable))
                {
                    Log.Debug("  Skipping temp table relationship: {From}[{FromCol}] -> {To}[{ToCol}]",
                        physFromTable, physFromColumn, physToTable, physToColumn);
                    continue;
                }

                // Log resolution if it happened
                if (!fromTable.Equals(physFromTable, StringComparison.OrdinalIgnoreCase) ||
                    !toTable.Equals(physToTable, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Debug("  Resolved to physical: {From}[{FromCol}] -> {To}[{ToCol}]",
                        physFromTable, physFromColumn, physToTable, physToColumn);
                }

                // Add relationship between physical tables
                analysis.AddRelationship(physFromTable, physFromColumn, physToTable, physToColumn, joinType);

                // Mark columns as used in joins
                var fromTableInfo = analysis.GetOrAddTable(physFromTable);
                var fromColumnInfo = fromTableInfo?.GetOrAddColumn(physFromColumn);
                fromColumnInfo?.AddUsage(XmSqlColumnUsage.Join);
                Log.Debug("  Marked {Table}[{Col}] as Join", physFromTable, physFromColumn);

                var toTableInfo = analysis.GetOrAddTable(physToTable);
                var toColumnInfo = toTableInfo?.GetOrAddColumn(physToColumn);
                toColumnInfo?.AddUsage(XmSqlColumnUsage.Join);
                Log.Debug("  Marked {Table}[{Col}] as Join", physToTable, physToColumn);
            }
        }

        /// <summary>
        /// Determines the join type by examining text before the ON clause.
        /// </summary>
        private XmSqlJoinType DetermineJoinType(string textBeforeOn)
        {
            // Find the last JOIN keyword before ON
            var lastLeftOuterJoin = textBeforeOn.LastIndexOf("LEFT OUTER JOIN", StringComparison.OrdinalIgnoreCase);
            var lastInnerJoin = textBeforeOn.LastIndexOf("INNER JOIN", StringComparison.OrdinalIgnoreCase);

            if (lastLeftOuterJoin > lastInnerJoin)
                return XmSqlJoinType.LeftOuterJoin;
            if (lastInnerJoin > lastLeftOuterJoin)
                return XmSqlJoinType.InnerJoin;

            return XmSqlJoinType.Unknown;
        }

        /// <summary>
        /// Parses the WHERE clause to identify filtered columns and extract filter values.
        /// Resolves temp table column references to physical tables.
        /// </summary>
        private void ParseWhereClause(string xmSql, XmSqlAnalysis analysis)
        {
            var whereMatch = WhereClausePattern.Match(xmSql);
            if (whereMatch.Success)
            {
                // Get everything after WHERE
                var whereClause = xmSql.Substring(whereMatch.Index);

                // Extract filter values from equality comparisons
                ExtractFilterEqualityValues(whereClause, analysis);

                // Extract filter values from IN clauses
                ExtractFilterInValues(whereClause, analysis);

                // Extract filter values from BETWEEN clauses
                ExtractFilterBetweenValues(whereClause, analysis);

                // Extract filter values from COALESCE comparisons
                ExtractFilterCoalesceValues(whereClause, analysis);

                // Find all table[column] references in the WHERE clause (for columns without explicit values)
                var columnMatches = TableColumnPattern.Matches(whereClause);
                foreach (Match match in columnMatches)
                {
                    var tableName = match.Groups["table"].Value;
                    var columnName = match.Groups["column"].Value;

                    // Resolve temp table references to physical tables
                    var resolved = ResolveToPhysicalColumn(tableName, columnName);
                    if (resolved.HasValue && !IsTempTable(resolved.Value.Table))
                    {
                        tableName = resolved.Value.Table;
                        columnName = resolved.Value.Column;
                    }
                    else if (IsTempTable(tableName))
                    {
                        // Couldn't resolve and it's a temp table - skip
                        continue;
                    }

                    var table = analysis.GetOrAddTable(tableName);
                    if (table != null)
                    {
                        var column = table.GetOrAddColumn(columnName);
                        column?.AddUsage(XmSqlColumnUsage.Filter);
                    }
                }
            }
        }

        /// <summary>
        /// Extracts filter values from equality comparisons like 'Table'[Column] = value.
        /// </summary>
        private void ExtractFilterEqualityValues(string whereClause, XmSqlAnalysis analysis)
        {
            var matches = FilterEqualityPattern.Matches(whereClause);
            foreach (Match match in matches)
            {
                var tableName = match.Groups["table"].Value;
                var columnName = match.Groups["column"].Value;
                var op = match.Groups["op"].Value;
                var value = match.Groups["value"].Value.Trim('\'', '"');

                // Resolve temp table references
                var resolved = ResolveToPhysicalColumn(tableName, columnName);
                if (resolved.HasValue && !IsTempTable(resolved.Value.Table))
                {
                    tableName = resolved.Value.Table;
                    columnName = resolved.Value.Column;
                }
                else if (IsTempTable(tableName))
                {
                    continue;
                }

                var table = analysis.GetOrAddTable(tableName);
                if (table != null)
                {
                    var column = table.GetOrAddColumn(columnName);
                    if (column != null)
                    {
                        column.AddUsage(XmSqlColumnUsage.Filter);
                        column.AddFilterValue(value, op);
                    }
                }
            }
        }

        /// <summary>
        /// Extracts filter values from IN clauses like 'Table'[Column] IN (val1, val2).
        /// </summary>
        private void ExtractFilterInValues(string whereClause, XmSqlAnalysis analysis)
        {
            var matches = FilterInPattern.Matches(whereClause);
            foreach (Match match in matches)
            {
                var tableName = match.Groups["table"].Value;
                var columnName = match.Groups["column"].Value;
                var valuesText = match.Groups["values"].Value;

                // Resolve temp table references
                var resolved = ResolveToPhysicalColumn(tableName, columnName);
                if (resolved.HasValue && !IsTempTable(resolved.Value.Table))
                {
                    tableName = resolved.Value.Table;
                    columnName = resolved.Value.Column;
                }
                else if (IsTempTable(tableName))
                {
                    continue;
                }

                var table = analysis.GetOrAddTable(tableName);
                if (table != null)
                {
                    var column = table.GetOrAddColumn(columnName);
                    if (column != null)
                    {
                        column.AddUsage(XmSqlColumnUsage.Filter);
                        
                        // Parse individual values from the IN list
                        var values = ParseInValues(valuesText);
                        foreach (var value in values)
                        {
                            column.AddFilterValue(value, "IN");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Parses values from an IN clause value list.
        /// </summary>
        private List<string> ParseInValues(string valuesText)
        {
            var result = new List<string>();
            var parts = valuesText.Split(',');
            foreach (var part in parts)
            {
                var trimmed = part.Trim().Trim('\'', '"');
                if (!string.IsNullOrWhiteSpace(trimmed) && result.Count < 20)
                {
                    result.Add(trimmed);
                }
            }
            return result;
        }

        /// <summary>
        /// Extracts filter values from BETWEEN clauses.
        /// </summary>
        private void ExtractFilterBetweenValues(string whereClause, XmSqlAnalysis analysis)
        {
            var matches = FilterBetweenPattern.Matches(whereClause);
            foreach (Match match in matches)
            {
                var tableName = match.Groups["table"].Value;
                var columnName = match.Groups["column"].Value;
                var val1 = match.Groups["val1"].Value.Trim('\'', '"');
                var val2 = match.Groups["val2"].Value.Trim('\'', '"');

                // Resolve temp table references
                var resolved = ResolveToPhysicalColumn(tableName, columnName);
                if (resolved.HasValue && !IsTempTable(resolved.Value.Table))
                {
                    tableName = resolved.Value.Table;
                    columnName = resolved.Value.Column;
                }
                else if (IsTempTable(tableName))
                {
                    continue;
                }

                var table = analysis.GetOrAddTable(tableName);
                if (table != null)
                {
                    var column = table.GetOrAddColumn(columnName);
                    if (column != null)
                    {
                        column.AddUsage(XmSqlColumnUsage.Filter);
                        column.AddFilterValue($"{val1} to {val2}", "BETWEEN");
                    }
                }
            }
        }

        /// <summary>
        /// Extracts filter values from COALESCE/PFCASTCOALESCE comparisons.
        /// Handles: PFCASTCOALESCE ( 'Table'[Column] AS INT ) > COALESCE ( value )
        /// </summary>
        private void ExtractFilterCoalesceValues(string whereClause, XmSqlAnalysis analysis)
        {
            var matches = FilterCoalescePattern.Matches(whereClause);
            foreach (Match match in matches)
            {
                var tableName = match.Groups["table"].Value;
                var columnName = match.Groups["column"].Value;
                var op = match.Groups["op"].Value;
                var value = match.Groups["value"].Value.Trim('\'', '"', ')', ' ');

                // Resolve temp table references
                var resolved = ResolveToPhysicalColumn(tableName, columnName);
                if (resolved.HasValue && !IsTempTable(resolved.Value.Table))
                {
                    tableName = resolved.Value.Table;
                    columnName = resolved.Value.Column;
                }
                else if (IsTempTable(tableName))
                {
                    continue;
                }

                var table = analysis.GetOrAddTable(tableName);
                if (table != null)
                {
                    var column = table.GetOrAddColumn(columnName);
                    if (column != null)
                    {
                        column.AddUsage(XmSqlColumnUsage.Filter);
                        column.AddFilterValue(value, op);
                    }
                }
            }
        }

        /// <summary>
        /// Parses aggregation functions to identify aggregated columns.
        /// </summary>
        private void ParseAggregations(string xmSql, XmSqlAnalysis analysis)
        {
            var aggMatches = AggregationPattern.Matches(xmSql);
            foreach (Match match in aggMatches)
            {
                var aggFunction = match.Groups["agg"].Value;
                var tableName = match.Groups["table"].Value;
                var columnName = match.Groups["column"].Value;

                if (!string.IsNullOrEmpty(tableName) && !string.IsNullOrEmpty(columnName))
                {
                    // Resolve temp table references to physical tables
                    var resolved = ResolveToPhysicalColumn(tableName, columnName);
                    if (resolved.HasValue && !IsTempTable(resolved.Value.Table))
                    {
                        tableName = resolved.Value.Table;
                        columnName = resolved.Value.Column;
                    }
                    else if (IsTempTable(tableName))
                    {
                        // Couldn't resolve and it's a temp table - skip
                        continue;
                    }

                    var table = analysis.GetOrAddTable(tableName);
                    if (table != null)
                    {
                        var column = table.GetOrAddColumn(columnName);
                        column?.AddAggregation(aggFunction);
                    }
                }
            }
        }

        /// <summary>
        /// Parses callback columns - these indicate DAX callbacks that can't be pushed down to the storage engine.
        /// </summary>
        private void ParseCallbacks(string xmSql, XmSqlAnalysis analysis)
        {
            // Parse CALLBACKDATAID patterns
            var callbackMatches = CallbackDataIdPattern.Matches(xmSql);
            foreach (Match match in callbackMatches)
            {
                var tableName = match.Groups["table"].Success ? match.Groups["table"].Value : match.Groups["table2"].Value;
                var columnName = match.Groups["column"].Success ? match.Groups["column"].Value : match.Groups["column2"].Value;

                if (!string.IsNullOrEmpty(tableName) && !string.IsNullOrEmpty(columnName))
                {
                    MarkColumnAsCallback(tableName, columnName, "CallbackDataID", analysis);
                }
            }

            // Parse ENCODECALLBACK patterns
            var encodeMatches = EncodeCallbackPattern.Matches(xmSql);
            foreach (Match match in encodeMatches)
            {
                var tableName = match.Groups["table"].Value;
                var columnName = match.Groups["column"].Value;

                if (!string.IsNullOrEmpty(tableName) && !string.IsNullOrEmpty(columnName))
                {
                    MarkColumnAsCallback(tableName, columnName, "EncodeCallback", analysis);
                }
            }

            // Parse generic CALLBACK patterns
            var genericMatches = GenericCallbackPattern.Matches(xmSql);
            foreach (Match match in genericMatches)
            {
                var tableName = match.Groups["table"].Value;
                var columnName = match.Groups["column"].Value;

                if (!string.IsNullOrEmpty(tableName) && !string.IsNullOrEmpty(columnName))
                {
                    MarkColumnAsCallback(tableName, columnName, "Callback", analysis);
                }
            }
        }

        /// <summary>
        /// Marks a column as having a callback.
        /// </summary>
        private void MarkColumnAsCallback(string tableName, string columnName, string callbackType, XmSqlAnalysis analysis)
        {
            // Resolve temp table references to physical tables
            var resolved = ResolveToPhysicalColumn(tableName, columnName);
            if (resolved.HasValue && !IsTempTable(resolved.Value.Table))
            {
                tableName = resolved.Value.Table;
                columnName = resolved.Value.Column;
            }
            else if (IsTempTable(tableName))
            {
                // Couldn't resolve and it's a temp table - skip
                return;
            }

            var table = analysis.GetOrAddTable(tableName);
            if (table != null)
            {
                var column = table.GetOrAddColumn(columnName);
                if (column != null)
                {
                    column.HasCallback = true;
                    if (string.IsNullOrEmpty(column.CallbackType))
                    {
                        column.CallbackType = callbackType;
                    }
                }
            }
        }

        /// <summary>
        /// Parses multiple xmSQL queries and aggregates the results.
        /// </summary>
        /// <param name="queries">Collection of xmSQL query strings.</param>
        /// <returns>An XmSqlAnalysis containing the aggregated results.</returns>
        public XmSqlAnalysis ParseQueries(IEnumerable<string> queries)
        {
            var analysis = new XmSqlAnalysis();

            foreach (var query in queries)
            {
                ParseQuery(query, analysis);
            }

            return analysis;
        }

        /// <summary>
        /// Extracts just the table name from a 'TableName'[ColumnName] reference.
        /// </summary>
        public static string ExtractTableName(string reference)
        {
            var match = TableColumnPattern.Match(reference);
            return match.Success ? match.Groups["table"].Value : null;
        }

        /// <summary>
        /// Extracts just the column name from a 'TableName'[ColumnName] reference.
        /// </summary>
        public static string ExtractColumnName(string reference)
        {
            var match = TableColumnPattern.Match(reference);
            return match.Success ? match.Groups["column"].Value : null;
        }

        /// <summary>
        /// Checks if the given xmSQL is a scan event (contains SELECT/FROM structure).
        /// </summary>
        public static bool IsScanQuery(string xmSql)
        {
            if (string.IsNullOrWhiteSpace(xmSql))
                return false;

            return xmSql.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   xmSql.IndexOf("FROM", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ==================== LINEAGE TRACKING METHODS ====================

        /// <summary>
        /// Builds the temp table lineage map by parsing DEFINE TABLE statements.
        /// This creates a mapping from temp tables to their source physical tables.
        /// </summary>
        private void BuildTempTableLineage(string xmSql)
        {
            // Parse DEFINE TABLE statements
            var defineMatches = DefineTablePattern.Matches(xmSql);
            foreach (Match match in defineMatches)
            {
                var tempTableName = match.Groups["tempTable"].Value;
                var definition = match.Groups["definition"].Value;
                
                ParseTempTableDefinition(tempTableName, definition);
            }

            // Parse REDUCED BY statements (nested temp table definitions)
            var reducedByMatches = ReducedByPattern.Matches(xmSql);
            foreach (Match match in reducedByMatches)
            {
                var tempTableName = match.Groups["tempTable"].Value;
                var definition = match.Groups["definition"].Value;
                
                ParseTempTableDefinition(tempTableName, definition);
            }

            // Resolve transitive lineage (temp tables derived from other temp tables)
            ResolveTransitiveLineage();

            Log.Debug("Built lineage for {Count} temp tables", _tempTableLineage.Count);
        }

        /// <summary>
        /// Parses a single temp table definition to extract source tables and column mappings.
        /// </summary>
        private void ParseTempTableDefinition(string tempTableName, string definition)
        {
            if (_tempTableLineage.ContainsKey(tempTableName))
                return;

            var lineage = new TempTableLineage { TempTableName = tempTableName };

            // Find FROM clause in definition
            var fromMatches = FromClausePattern.Matches(definition);
            foreach (Match fromMatch in fromMatches)
            {
                var sourceTable = fromMatch.Groups["table"].Value;
                if (IsTempTable(sourceTable))
                {
                    lineage.SourceTempTables.Add(sourceTable);
                }
                else
                {
                    lineage.SourcePhysicalTables.Add(sourceTable);
                }
            }

            // Find JOIN tables in definition
            var leftJoinMatches = LeftOuterJoinPattern.Matches(definition);
            foreach (Match joinMatch in leftJoinMatches)
            {
                var joinTable = joinMatch.Groups["table"].Value;
                if (IsTempTable(joinTable))
                {
                    lineage.SourceTempTables.Add(joinTable);
                }
                else
                {
                    lineage.SourcePhysicalTables.Add(joinTable);
                }
            }

            var innerJoinMatches = InnerJoinPattern.Matches(definition);
            foreach (Match joinMatch in innerJoinMatches)
            {
                var joinTable = joinMatch.Groups["table"].Value;
                if (IsTempTable(joinTable))
                {
                    lineage.SourceTempTables.Add(joinTable);
                }
                else
                {
                    lineage.SourcePhysicalTables.Add(joinTable);
                }
            }

            // Find REVERSE BITMAP JOIN tables
            var reverseBitmapMatches = ReverseBitmapJoinPattern.Matches(definition);
            foreach (Match rbMatch in reverseBitmapMatches)
            {
                var table = rbMatch.Groups["table"].Value;
                if (IsTempTable(table))
                {
                    lineage.SourceTempTables.Add(table);
                }
                else
                {
                    lineage.SourcePhysicalTables.Add(table);
                }
            }

            // Parse column mappings from SELECT clause
            // Look for columns with $ naming: 'Calendar$Date' means Calendar.Date
            var columnMatches = TableColumnPattern.Matches(definition);
            foreach (Match colMatch in columnMatches)
            {
                var table = colMatch.Groups["table"].Value;
                var column = colMatch.Groups["column"].Value;

                // Check if column name has the $ pattern (e.g., "Calendar$Date")
                var tempColMatch = TempColumnNamePattern.Match(column);
                if (tempColMatch.Success)
                {
                    var sourceTable = tempColMatch.Groups["sourceTable"].Value;
                    var sourceColumn = tempColMatch.Groups["sourceColumn"].Value;
                    
                    lineage.ColumnMappings[column] = new ColumnLineage
                    {
                        TempColumnName = column,
                        SourceTable = sourceTable,
                        SourceColumn = sourceColumn
                    };

                    // Also track this as a source physical table
                    if (!IsTempTable(sourceTable))
                    {
                        lineage.SourcePhysicalTables.Add(sourceTable);
                    }
                }
                else if (!IsTempTable(table))
                {
                    // Direct physical table reference
                    lineage.SourcePhysicalTables.Add(table);
                    
                    // Map the column
                    lineage.ColumnMappings[column] = new ColumnLineage
                    {
                        TempColumnName = column,
                        SourceTable = table,
                        SourceColumn = column
                    };
                }
            }

            // Extract filter values from WHERE clause in the definition
            ExtractTempTableFilterValues(definition, lineage);

            _tempTableLineage[tempTableName] = lineage;
            
            Log.Debug("Parsed lineage for {TempTable}: Physical=[{Physical}], Temp=[{Temp}], Columns={ColCount}, Filters={FilterCount}",
                tempTableName,
                string.Join(",", lineage.SourcePhysicalTables),
                string.Join(",", lineage.SourceTempTables),
                lineage.ColumnMappings.Count,
                lineage.FilterValues.Count);
        }

        /// <summary>
        /// Extracts filter values from the WHERE clause of a temp table definition.
        /// These values can later be propagated to ININDEX-filtered columns.
        /// </summary>
        private void ExtractTempTableFilterValues(string definition, TempTableLineage lineage)
        {
            // Look for WHERE clause in the definition
            var whereMatch = WhereClausePattern.Match(definition);
            if (!whereMatch.Success) return;

            var whereClause = definition.Substring(whereMatch.Index);
            // Limit to just the WHERE portion (stop at REDUCED BY, DEFINE, etc.)
            var endMarkers = new[] { "REDUCED BY", "DEFINE TABLE", "CREATE SHALLOW" };
            foreach (var marker in endMarkers)
            {
                var idx = whereClause.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx > 0) whereClause = whereClause.Substring(0, idx);
            }

            // Extract equality filters: 'table'[column] = 'value'
            var eqMatches = FilterEqualityPattern.Matches(whereClause);
            foreach (Match match in eqMatches)
            {
                var tableName = match.Groups["table"].Value;
                var columnName = match.Groups["column"].Value;
                var op = match.Groups["op"].Value;
                var value = match.Groups["value"].Value.Trim('\'', '"');

                // Skip temp table references
                if (IsTempTable(tableName)) continue;

                var key = $"{tableName}.{columnName}";
                if (!lineage.FilterValues.ContainsKey(key))
                {
                    lineage.FilterValues[key] = new List<string>();
                }
                if (!lineage.FilterValues[key].Contains(value) && lineage.FilterValues[key].Count < 20)
                {
                    lineage.FilterValues[key].Add(value);
                }
                lineage.FilterOperators[key] = op;
            }

            // Extract IN clause filters: 'table'[column] IN ( 'val1', 'val2', ... )
            var inMatches = FilterInPattern.Matches(whereClause);
            foreach (Match match in inMatches)
            {
                var tableName = match.Groups["table"].Value;
                var columnName = match.Groups["column"].Value;
                var valuesText = match.Groups["values"].Value;

                // Skip temp table references
                if (IsTempTable(tableName)) continue;

                var key = $"{tableName}.{columnName}";
                if (!lineage.FilterValues.ContainsKey(key))
                {
                    lineage.FilterValues[key] = new List<string>();
                }
                lineage.FilterOperators[key] = "IN";

                // Parse individual values
                var values = ParseInValues(valuesText);
                foreach (var value in values)
                {
                    if (!lineage.FilterValues[key].Contains(value) && lineage.FilterValues[key].Count < 20)
                    {
                        lineage.FilterValues[key].Add(value);
                    }
                }
            }
        }

        /// <summary>
        /// Resolves transitive lineage - when temp tables are derived from other temp tables,
        /// we need to trace back to the original physical tables.
        /// </summary>
        private void ResolveTransitiveLineage()
        {
            // Keep iterating until no more changes (handles multi-level nesting)
            bool changed;
            int maxIterations = 10; // Prevent infinite loops
            int iteration = 0;

            do
            {
                changed = false;
                iteration++;

                foreach (var lineage in _tempTableLineage.Values)
                {
                    // For each source temp table, add its physical sources to our physical sources
                    foreach (var sourceTempTable in lineage.SourceTempTables.ToList())
                    {
                        if (_tempTableLineage.TryGetValue(sourceTempTable, out var sourceLineage))
                        {
                            foreach (var physTable in sourceLineage.SourcePhysicalTables)
                            {
                                if (lineage.SourcePhysicalTables.Add(physTable))
                                {
                                    changed = true;
                                }
                            }
                        }
                    }
                }
            } while (changed && iteration < maxIterations);
        }

        /// <summary>
        /// Checks if a table name represents a temporary table.
        /// </summary>
        private static bool IsTempTable(string tableName)
        {
            return tableName != null && tableName.StartsWith("$T", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves a table name to its physical source(s).
        /// If it's a temp table, returns the underlying physical tables.
        /// If it's already a physical table, returns it as-is.
        /// </summary>
        private IEnumerable<string> ResolveToPhysicalTables(string tableName)
        {
            if (!IsTempTable(tableName))
            {
                return new[] { tableName };
            }

            if (_tempTableLineage != null && _tempTableLineage.TryGetValue(tableName, out var lineage))
            {
                if (lineage.SourcePhysicalTables.Count > 0)
                {
                    return lineage.SourcePhysicalTables;
                }
            }

            // Couldn't resolve - return empty
            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// Resolves a column reference (table + column) to its physical source.
        /// Handles the $ naming convention (e.g., Calendar$Date -> Calendar.Date).
        /// </summary>
        private (string Table, string Column)? ResolveToPhysicalColumn(string tableName, string columnName)
        {
            // First, check if the column name has the $ pattern
            var tempColMatch = TempColumnNamePattern.Match(columnName);
            if (tempColMatch.Success)
            {
                var sourceTable = tempColMatch.Groups["sourceTable"].Value;
                var sourceColumn = tempColMatch.Groups["sourceColumn"].Value;
                return (sourceTable, sourceColumn);
            }

            // If the table is a temp table, try to resolve through lineage
            if (IsTempTable(tableName) && _tempTableLineage != null)
            {
                if (_tempTableLineage.TryGetValue(tableName, out var lineage))
                {
                    if (lineage.ColumnMappings.TryGetValue(columnName, out var colLineage))
                    {
                        return (colLineage.SourceTable, colLineage.SourceColumn);
                    }
                    
                    // If we have exactly one source physical table, use that
                    if (lineage.SourcePhysicalTables.Count == 1)
                    {
                        return (lineage.SourcePhysicalTables.First(), columnName);
                    }
                }
            }

            // Not a temp table or couldn't resolve
            if (!IsTempTable(tableName))
            {
                return (tableName, columnName);
            }

            return null;
        }

        /// <summary>
        /// Parses ININDEX operations which represent filter relationships through temp tables.
        /// Example: 'Sales'[DateKey] ININDEX '$TTable4'[$SemijoinProjection]
        /// This indicates Sales.DateKey is filtered by the temp table, which we can trace back.
        /// </summary>
        private void ParseInIndexOperations(string xmSql, XmSqlAnalysis analysis)
        {
            var inIndexMatches = InIndexPattern.Matches(xmSql);
            foreach (Match match in inIndexMatches)
            {
                var physTable = match.Groups["physTable"].Value;
                var physColumn = match.Groups["physColumn"].Value;
                var tempTable = match.Groups["tempTable"].Value;

                Log.Debug("Found ININDEX: {Table}[{Col}] ININDEX {TempTable}", physTable, physColumn, tempTable);

                // Mark the physical column as used in a filter
                if (!IsTempTable(physTable))
                {
                    var table = analysis.GetOrAddTable(physTable);
                    var column = table?.GetOrAddColumn(physColumn);
                    if (column != null)
                    {
                        column.AddUsage(XmSqlColumnUsage.Filter);
                        
                        // Try to propagate filter values from the source temp table chain
                        PropagateInIndexFilterValues(tempTable, physTable, physColumn, column);
                    }
                }

                // Resolve the temp table to find what physical tables it relates to
                // This helps us understand the relationship chain
                var physicalSources = ResolveToPhysicalTables(tempTable);
                foreach (var sourceTable in physicalSources)
                {
                    if (!sourceTable.Equals(physTable, StringComparison.OrdinalIgnoreCase))
                    {
                        // There's an implicit relationship between physTable and sourceTable
                        // through this ININDEX operation
                        Log.Debug("  Resolved ININDEX lineage: {Source} -> {Target} via {Temp}",
                            sourceTable, physTable, tempTable);

                        // We could add an implicit relationship here if needed
                        // For now, just ensure both tables are tracked
                        analysis.GetOrAddTable(sourceTable);
                    }
                }
            }
        }

        /// <summary>
        /// Propagates filter values from temp table chains to the ININDEX-filtered column.
        /// Traces back through $TTable references to find the original filter values.
        /// </summary>
        private void PropagateInIndexFilterValues(string tempTable, string targetTable, string targetColumn, XmSqlColumnInfo column)
        {
            // Track visited tables to avoid infinite loops
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tablesToCheck = new Queue<string>();
            tablesToCheck.Enqueue(tempTable);

            while (tablesToCheck.Count > 0)
            {
                var currentTemp = tablesToCheck.Dequeue();
                if (visited.Contains(currentTemp)) continue;
                visited.Add(currentTemp);

                if (!_tempTableLineage.TryGetValue(currentTemp, out var lineage))
                    continue;

                // Check if this temp table has filter values we can propagate
                foreach (var kvp in lineage.FilterValues)
                {
                    // The key is "Table.Column" - we need to check if this relates to our target
                    var parts = kvp.Key.Split('.');
                    if (parts.Length != 2) continue;

                    var filterTable = parts[0];
                    var filterColumn = parts[1];

                    // Check for direct match or related columns
                    // The filter might be on the same table/column, or on a related dimension table
                    // For now, propagate if it's from a dimension table that feeds into this ININDEX
                    if (lineage.SourcePhysicalTables.Contains(filterTable))
                    {
                        foreach (var value in kvp.Value)
                        {
                            column.AddFilterValue(value, lineage.FilterOperators.TryGetValue(kvp.Key, out var op) ? op : "ININDEX");
                        }
                        
                        Log.Debug("  Propagated filter values from {Table}[{Col}] to {Target}[{TargetCol}]: {Values}",
                            filterTable, filterColumn, targetTable, targetColumn, 
                            string.Join(", ", kvp.Value.Take(5)));
                    }
                }

                // Add source temp tables to check (trace back further)
                foreach (var sourceTempTable in lineage.SourceTempTables)
                {
                    if (!visited.Contains(sourceTempTable))
                    {
                        tablesToCheck.Enqueue(sourceTempTable);
                    }
                }
            }
        }

        /// <summary>
        /// Parses CREATE SHALLOW RELATION definitions which explicitly define relationships.
        /// Also extracts cardinality (OneToMany, ManyToMany) and cross-filter direction.
        /// </summary>
        private void ParseShallowRelations(string xmSql, XmSqlAnalysis analysis)
        {
            var matches = CreateShallowRelationExtendedPattern.Matches(xmSql);
            foreach (Match match in matches)
            {
                var relationName = match.Groups["relationName"].Value;
                var fromTable = match.Groups["fromTable"].Value;
                var fromColumn = match.Groups["fromColumn"].Value;
                var toTable = match.Groups["toTable"].Value;
                var toColumn = match.Groups["toColumn"].Value;
                var isManyToMany = match.Groups["manyToMany"].Success;

                // Get the full matched text to check for BOTH direction
                var fullMatch = match.Value;
                var isBothDirection = BothDirectionPattern.IsMatch(fullMatch);

                Log.Debug("Found SHALLOW RELATION: {From}[{FromCol}] -> {To}[{ToCol}] ManyToMany={M2M} BothDir={Both}",
                    fromTable, fromColumn, toTable, toColumn, isManyToMany, isBothDirection);

                // Resolve any temp table references to physical tables
                var resolvedFrom = ResolveToPhysicalColumn(fromTable, fromColumn);
                var resolvedTo = ResolveToPhysicalColumn(toTable, toColumn);

                if (resolvedFrom.HasValue && resolvedTo.HasValue)
                {
                    var (physFromTable, physFromCol) = resolvedFrom.Value;
                    var (physToTable, physToCol) = resolvedTo.Value;

                    // Add the relationship between physical tables
                    if (!IsTempTable(physFromTable) && !IsTempTable(physToTable))
                    {
                        // Check if relationship already exists (in either direction)
                        var existing = analysis.Relationships.FirstOrDefault(r =>
                            (r.FromTable.Equals(physFromTable, StringComparison.OrdinalIgnoreCase) &&
                             r.FromColumn.Equals(physFromCol, StringComparison.OrdinalIgnoreCase) &&
                             r.ToTable.Equals(physToTable, StringComparison.OrdinalIgnoreCase) &&
                             r.ToColumn.Equals(physToCol, StringComparison.OrdinalIgnoreCase)) ||
                            // Also check reverse direction
                            (r.FromTable.Equals(physToTable, StringComparison.OrdinalIgnoreCase) &&
                             r.FromColumn.Equals(physToCol, StringComparison.OrdinalIgnoreCase) &&
                             r.ToTable.Equals(physFromTable, StringComparison.OrdinalIgnoreCase) &&
                             r.ToColumn.Equals(physFromCol, StringComparison.OrdinalIgnoreCase)));

                        if (existing != null)
                        {
                            // Update existing relationship with cardinality info
                            existing.HitCount++;
                            if (isManyToMany)
                            {
                                existing.Cardinality = XmSqlCardinality.ManyToMany;
                            }
                            else if (existing.Cardinality == XmSqlCardinality.Unknown)
                            {
                                // Default assumption: OneToMany (common in star schema)
                                existing.Cardinality = XmSqlCardinality.OneToMany;
                            }
                            if (isBothDirection)
                            {
                                existing.CrossFilterDirection = XmSqlCrossFilterDirection.Both;
                            }
                            else if (existing.CrossFilterDirection == XmSqlCrossFilterDirection.Unknown)
                            {
                                existing.CrossFilterDirection = XmSqlCrossFilterDirection.Single;
                            }
                        }
                        else
                        {
                            // Add new relationship
                            var relationship = new XmSqlRelationship
                            {
                                FromTable = physFromTable,
                                FromColumn = physFromCol,
                                ToTable = physToTable,
                                ToColumn = physToCol,
                                JoinType = XmSqlJoinType.Unknown,
                                HitCount = 1,
                                Cardinality = isManyToMany ? XmSqlCardinality.ManyToMany : XmSqlCardinality.OneToMany,
                                CrossFilterDirection = isBothDirection ? XmSqlCrossFilterDirection.Both : XmSqlCrossFilterDirection.Single
                            };
                            analysis.Relationships.Add(relationship);
                        }

                        // Mark columns as join keys
                        var fromTableInfo = analysis.GetOrAddTable(physFromTable);
                        fromTableInfo?.GetOrAddColumn(physFromCol)?.AddUsage(XmSqlColumnUsage.Join);

                        var toTableInfo = analysis.GetOrAddTable(physToTable);
                        toTableInfo?.GetOrAddColumn(physToCol)?.AddUsage(XmSqlColumnUsage.Join);

                        Log.Debug("  Added physical relationship: {From}[{FromCol}] -> {To}[{ToCol}]",
                            physFromTable, physFromCol, physToTable, physToCol);
                    }
                }
            }

            // Fallback: Also parse with original pattern if extended didn't match
            var fallbackMatches = CreateShallowRelationPattern.Matches(xmSql);
            foreach (Match match in fallbackMatches)
            {
                var fromTable = match.Groups["fromTable"].Value;
                var fromColumn = match.Groups["fromColumn"].Value;
                var toTable = match.Groups["toTable"].Value;
                var toColumn = match.Groups["toColumn"].Value;

                // Resolve any temp table references to physical tables
                var resolvedFrom = ResolveToPhysicalColumn(fromTable, fromColumn);
                var resolvedTo = ResolveToPhysicalColumn(toTable, toColumn);

                if (resolvedFrom.HasValue && resolvedTo.HasValue)
                {
                    var (physFromTable, physFromCol) = resolvedFrom.Value;
                    var (physToTable, physToCol) = resolvedTo.Value;

                    // Add the relationship if it doesn't exist yet (check both directions)
                    if (!IsTempTable(physFromTable) && !IsTempTable(physToTable))
                    {
                        var exists = analysis.Relationships.Any(r =>
                            (r.FromTable.Equals(physFromTable, StringComparison.OrdinalIgnoreCase) &&
                             r.FromColumn.Equals(physFromCol, StringComparison.OrdinalIgnoreCase) &&
                             r.ToTable.Equals(physToTable, StringComparison.OrdinalIgnoreCase) &&
                             r.ToColumn.Equals(physToCol, StringComparison.OrdinalIgnoreCase)) ||
                            // Also check reverse direction
                            (r.FromTable.Equals(physToTable, StringComparison.OrdinalIgnoreCase) &&
                             r.FromColumn.Equals(physToCol, StringComparison.OrdinalIgnoreCase) &&
                             r.ToTable.Equals(physFromTable, StringComparison.OrdinalIgnoreCase) &&
                             r.ToColumn.Equals(physFromCol, StringComparison.OrdinalIgnoreCase)));

                        if (!exists)
                        {
                            analysis.AddRelationship(physFromTable, physFromCol, physToTable, physToCol, XmSqlJoinType.Unknown);

                            // Mark columns as join keys
                            var fromTableInfo = analysis.GetOrAddTable(physFromTable);
                            fromTableInfo?.GetOrAddColumn(physFromCol)?.AddUsage(XmSqlColumnUsage.Join);

                            var toTableInfo = analysis.GetOrAddTable(physToTable);
                            toTableInfo?.GetOrAddColumn(physToCol)?.AddUsage(XmSqlColumnUsage.Join);
                        }
                    }
                }
            }
        }
    }
}
