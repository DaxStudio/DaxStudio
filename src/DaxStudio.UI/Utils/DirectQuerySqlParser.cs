using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DaxStudio.UI.Model;
using Serilog;

namespace DaxStudio.UI.Utils
{
    /// <summary>
    /// Multi-pass parser for DirectQuery SQL statements.
    /// 
    /// DirectQuery SQL has multiple formats:
    /// 
    /// Format 1 - Subselect with [$Table]:
    ///   (select [$Table].[col] as [col] from [dbo].[TableName] as [$Table]) AS [t4]
    /// 
    /// Format 2 - Direct table reference:
    ///   [dbo].[TableName] AS [t8]
    /// 
    /// Format 3 - Bare table in JOIN:
    ///   INNER JOIN [dbo].[TableName] AS [t7] ON (...)
    /// 
    /// This parser builds an alias map and traces relationships through the hierarchy.
    /// </summary>
    public class DirectQuerySqlParser
    {
        private readonly string _sql;
        
        /// <summary>
        /// Tables found with their used columns.
        /// </summary>
        public Dictionary<string, DqTableInfo> Tables { get; } = new Dictionary<string, DqTableInfo>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Relationships found between tables.
        /// </summary>
        public List<DqRelationship> Relationships { get; } = new List<DqRelationship>();

        /// <summary>
        /// Maps aliases to their source table or composite info.
        /// </summary>
        private readonly Dictionary<string, AliasInfo> _aliasMap = new Dictionary<string, AliasInfo>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Maps [alias].[columnAlias] to actual table.column.
        /// </summary>
        private readonly Dictionary<string, ColumnMapping> _columnAliasMap = new Dictionary<string, ColumnMapping>(StringComparer.OrdinalIgnoreCase);

        // ==================== REGEX PATTERNS ====================

        // Pattern 1: Subselect block with [$Table] and eventual alias
        // (select [$Table].[col1] as [c1], ... from [dbo].[TableName] as [$Table]) AS [t4]
        // Also handles extra wrapper parens: ((...)) AS [t4]
        private static readonly Regex SubselectWithDollarTablePattern = new Regex(
            @"\(\s*\(*\s*select\s+(?<columns>.*?)\s+from\s+\[(?<schema>[^\]]+)\]\s*\.\s*\[(?<table>[^\]]+)\]\s+as\s+\[\$Table\]\s*\)\s*\)*\s*AS\s+\[(?<alias>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Pattern 2: Direct table reference [dbo].[TableName] AS [alias]
        // Handles both standalone and in JOIN context
        private static readonly Regex DirectTableAliasPattern = new Regex(
            @"\[(?<schema>dbo|sys)\]\s*\.\s*\[(?<table>[^\]]+)\]\s+AS\s+\[(?<alias>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern 3: Column definition in subselect [$Table].[ColumnName] as [alias]
        private static readonly Regex DollarTableColumnPattern = new Regex(
            @"\[\$Table\]\s*\.\s*\[(?<column>[^\]]+)\]\s+as\s+\[(?<alias>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern 4: SELECT columns with alias: [t7].[Column] AS [t7_Column] or [t4].[Column] AS [c10]
        private static readonly Regex SelectColumnAliasPattern = new Regex(
            @"SELECT\s+(?<selectList>.*?)\s+FROM",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Pattern 5: Individual column in select: [alias].[Column] AS [outputAlias]
        private static readonly Regex ColumnAsPattern = new Regex(
            @"\[(?<alias>[^\]]+)\]\s*\.\s*\[(?<column>[^\]]+)\]\s+AS\s+\[(?<outputAlias>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern 6: ON clause conditions
        private static readonly Regex OnConditionPattern = new Regex(
            @"\[(?<leftAlias>[^\]]+)\]\s*\.\s*\[(?<leftCol>[^\]]+)\]\s*=\s*\[(?<rightAlias>[^\]]+)\]\s*\.\s*\[(?<rightCol>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern 7: GROUP BY columns
        private static readonly Regex GroupByColumnPattern = new Regex(
            @"\bGROUP\s+BY\s+(?<columns>.*?)(?=\bHAVING\b|\bORDER\b|\)\s*AS\s*\[|$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Pattern 8: Composite alias assignment ) AS [semijoin1] or ) AS [basetable0]
        private static readonly Regex CompositeAliasPattern = new Regex(
            @"\)\s*AS\s+\[(?<alias>semijoin\d+|basetable\d+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public DirectQuerySqlParser(string sql)
        {
            _sql = sql ?? string.Empty;
        }

        /// <summary>
        /// The hierarchical block parser for complex SQL analysis.
        /// </summary>
        private SqlBlockParser _blockParser;

        /// <summary>
        /// Parses the SQL and populates Tables and Relationships.
        /// </summary>
        public void Parse()
        {
            if (string.IsNullOrWhiteSpace(_sql))
                return;

            try
            {
                // NEW: Build hierarchical block structure first
                _blockParser = new SqlBlockParser(_sql);
                var rootBlock = _blockParser.Parse();
                
                if (rootBlock != null)
                {
                    Log.Debug("SQL Block Tree:\n{Tree}", _blockParser.GetTreeDiagram());
                }

                // Pass 1: Extract all table aliases (both subselect and direct)
                Pass1_BuildAliasMap();

                // Pass 2: Build column alias mappings from SELECT statements
                Pass2_BuildColumnAliasMap();

                // Pass 3: Extract columns that are actually used
                Pass3_ExtractUsedColumns();

                // Pass 4: Extract relationships from ON clauses (uses block parser for resolution)
                Pass4_ExtractRelationships();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "DirectQuerySqlParser failed to parse SQL");
            }
        }

        /// <summary>
        /// Pass 1: Build alias-to-table mapping from all patterns.
        /// </summary>
        private void Pass1_BuildAliasMap()
        {
            // Pattern 1: Subselects with [$Table] - these have column lists
            var subselectMatches = SubselectWithDollarTablePattern.Matches(_sql);
            foreach (Match match in subselectMatches)
            {
                var tableName = match.Groups["table"].Value;
                var alias = match.Groups["alias"].Value;
                var columnsText = match.Groups["columns"].Value;

                if (!string.IsNullOrEmpty(tableName) && !string.IsNullOrEmpty(alias))
                {
                    EnsureTable(tableName);
                    _aliasMap[alias] = new AliasInfo
                    {
                        IsBaseTable = true,
                        BaseTableName = tableName
                    };
                    Log.Debug("Pass1 Pattern1: [{Alias}] -> {Table}", alias, tableName);

                    // Extract column mappings from this subselect
                    var colMatches = DollarTableColumnPattern.Matches(columnsText);
                    foreach (Match colMatch in colMatches)
                    {
                        var actualColumn = colMatch.Groups["column"].Value;
                        var colAlias = colMatch.Groups["alias"].Value;
                        
                        // If no explicit alias, use the column name itself
                        if (string.IsNullOrEmpty(colAlias))
                            colAlias = actualColumn;
                            
                        var key = $"{alias}.{colAlias}";
                        _columnAliasMap[key] = new ColumnMapping
                        {
                            TableName = tableName,
                            ColumnName = actualColumn
                        };
                        Log.Debug("Pass1 ColumnMap: [{Key}] -> {Table}.{Col}", key, tableName, actualColumn);
                    }
                }
            }

            // Pattern 1b: Find all [$Table] subselects and trace outward to find their alias
            // This handles complex nested structures where extra parens separate the subselect from its alias
            Pass1b_FindDollarTableSubselects();

            // Pattern 2: Direct table references [dbo].[Table] AS [alias]
            // Need to avoid matching inside subselects we already processed
            var directMatches = DirectTableAliasPattern.Matches(_sql);
            foreach (Match match in directMatches)
            {
                var tableName = match.Groups["table"].Value;
                var alias = match.Groups["alias"].Value;

                // Skip $Table alias (that's internal to subselects)
                if (alias.Equals("$Table", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip if we already have this alias from a subselect pattern
                if (_aliasMap.ContainsKey(alias))
                    continue;

                if (!string.IsNullOrEmpty(tableName) && !string.IsNullOrEmpty(alias))
                {
                    EnsureTable(tableName);
                    _aliasMap[alias] = new AliasInfo
                    {
                        IsBaseTable = true,
                        BaseTableName = tableName
                    };
                }
            }

            // Pattern 3: Composite aliases (semijoin, basetable)
            var compositeMatches = CompositeAliasPattern.Matches(_sql);
            foreach (Match match in compositeMatches)
            {
                var alias = match.Groups["alias"].Value;
                if (!_aliasMap.ContainsKey(alias))
                {
                    _aliasMap[alias] = new AliasInfo
                    {
                        IsBaseTable = false,
                        SourceAliases = new List<string>()
                    };
                }
            }

            Log.Debug("DirectQuerySqlParser: Found {Count} aliases: {Aliases}",
                _aliasMap.Count, string.Join(", ", _aliasMap.Keys));
        }

        /// <summary>
        /// Pass 1b: Find [$Table] subselects and trace outward to find their containing alias.
        /// This handles complex nesting where the alias is separated by extra wrapper parens/SELECTs.
        /// </summary>
        private void Pass1b_FindDollarTableSubselects()
        {
            // Find all occurrences of "from [dbo].[TableName] as [$Table]"
            var dollarTablePattern = new Regex(
                @"from\s+\[(?<schema>[^\]]+)\]\s*\.\s*\[(?<table>[^\]]+)\]\s+as\s+\[\$Table\]",
                RegexOptions.IgnoreCase);
            
            var matches = dollarTablePattern.Matches(_sql);
            foreach (Match match in matches)
            {
                var tableName = match.Groups["table"].Value;
                var matchEndPos = match.Index + match.Length;
                
                // Skip if we already have this table mapped via Pattern 1
                bool alreadyMapped = _aliasMap.Values.Any(a => 
                    a.IsBaseTable && a.BaseTableName.Equals(tableName, StringComparison.OrdinalIgnoreCase));
                if (alreadyMapped)
                    continue;
                
                EnsureTable(tableName);
                
                // Find the corresponding SELECT block for this [$Table]
                // Search backwards for "select" and extract columns
                var beforeMatch = _sql.Substring(0, match.Index);
                var selectStartMatch = Regex.Match(beforeMatch, 
                    @"select\s+(?<cols>[\s\S]*?)$", 
                    RegexOptions.IgnoreCase | RegexOptions.RightToLeft);
                
                if (!selectStartMatch.Success)
                    continue;
                    
                var columnsText = selectStartMatch.Groups["cols"].Value;
                
                // Now find the alias for this subselect by scanning forward from matchEndPos
                // Look for the pattern: ) ... ) AS [alias] accounting for nested parens
                var afterMatch = _sql.Substring(matchEndPos);
                
                // Find closing parens followed by AS [alias]
                // We need to skip past any intermediate structure
                var aliasMatch = Regex.Match(afterMatch, 
                    @"^\s*\)\s*\)*\s*AS\s+\[(?<alias>t\d+)\]",
                    RegexOptions.IgnoreCase);
                
                if (aliasMatch.Success)
                {
                    var alias = aliasMatch.Groups["alias"].Value;
                    
                    // Don't overwrite if already exists
                    if (!_aliasMap.ContainsKey(alias))
                    {
                        _aliasMap[alias] = new AliasInfo
                        {
                            IsBaseTable = true,
                            BaseTableName = tableName
                        };
                        Log.Debug("Pass1b: [{Alias}] -> {Table}", alias, tableName);
                    }
                    
                    // Extract column mappings
                    var colMatches = DollarTableColumnPattern.Matches(columnsText);
                    foreach (Match colMatch in colMatches)
                    {
                        var actualColumn = colMatch.Groups["column"].Value;
                        var colAlias = colMatch.Groups["alias"].Value;
                        if (string.IsNullOrEmpty(colAlias))
                            colAlias = actualColumn;
                            
                        var key = $"{alias}.{colAlias}";
                        if (!_columnAliasMap.ContainsKey(key))
                        {
                            _columnAliasMap[key] = new ColumnMapping
                            {
                                TableName = tableName,
                                ColumnName = actualColumn
                            };
                            Log.Debug("Pass1b ColumnMap: [{Key}] -> {Table}.{Col}", key, tableName, actualColumn);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Pass 2: Build column alias map from SELECT statements.
        /// Maps columns through the alias chain, e.g.:
        /// [t4].[PODs Key] AS [c10] -> Distribution_Volume_Weekly.PODs Key
        /// [basetable0].[c10] -> (traces through) -> Distribution_Volume_Weekly.PODs Key
        /// </summary>
        private void Pass2_BuildColumnAliasMap()
        {
            // Strategy: Find ) AS [alias] blocks and trace the column mappings
            // Process innermost blocks first so outer blocks can resolve through them
            
            var blockAliasPattern = new Regex(
                @"\)\s*AS\s+\[(?<alias>t\d+|basetable\d+|semijoin\d+)\]",
                RegexOptions.IgnoreCase);
            
            // Collect all blocks with their open paren positions
            var blocks = new List<(string Alias, int OpenPos, int ClosePos)>();
            
            var blockMatches = blockAliasPattern.Matches(_sql);
            foreach (Match blockMatch in blockMatches)
            {
                var blockAlias = blockMatch.Groups["alias"].Value;
                var closeParenPos = blockMatch.Index;
                var openParenPos = FindMatchingOpenParen(_sql, closeParenPos);
                if (openParenPos >= 0)
                {
                    blocks.Add((blockAlias, openParenPos, closeParenPos));
                }
            }
            
            // Sort by open position descending (process innermost first = highest position first for nested blocks)
            // Actually, sort by close position - innermost closes first
            blocks.Sort((a, b) => a.ClosePos.CompareTo(b.ClosePos));
            
            foreach (var block in blocks)
            {
                ProcessBlockColumnMappings(block.Alias, block.OpenPos, block.ClosePos);
            }
        }
        
        private void ProcessBlockColumnMappings(string blockAlias, int openPos, int closePos)
        {
            // Extract the block content
            var blockContent = _sql.Substring(openPos + 1, closePos - openPos - 1);
            
            // Find the FIRST SELECT in this block (the one that outputs to the alias)
            var selectMatch = Regex.Match(blockContent, 
                @"^\s*SELECT\s+(?<cols>.*?)\s+FROM",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            if (!selectMatch.Success)
                return;
                
            var selectCols = selectMatch.Groups["cols"].Value;
            
            // Parse columns from this SELECT
            var colMatches = ColumnAsPattern.Matches(selectCols);
            foreach (Match colMatch in colMatches)
            {
                var sourceAlias = colMatch.Groups["alias"].Value;
                var column = colMatch.Groups["column"].Value;
                var outputAlias = colMatch.Groups["outputAlias"].Value;
                
                // Skip artifacts
                if (sourceAlias.Equals("TOP", StringComparison.OrdinalIgnoreCase))
                    continue;
                
                // Resolve the source column (will use previously built mappings)
                var resolved = ResolveColumn(sourceAlias, column);
                if (resolved != null)
                {
                    // Map [blockAlias].[outputAlias] -> resolved
                    var key = $"{blockAlias}.{outputAlias}";
                    if (!_columnAliasMap.ContainsKey(key))
                    {
                        _columnAliasMap[key] = resolved;
                        Log.Debug("Column alias: [{Key}] -> {Table}.{Col}", 
                            key, resolved.TableName, resolved.ColumnName);
                    }
                }
            }
        }

        /// <summary>
        /// Find the position of the opening parenthesis that matches the closing paren at closePos
        /// </summary>
        private int FindMatchingOpenParen(string sql, int closePos)
        {
            int depth = 0;
            for (int i = closePos; i >= 0; i--)
            {
                if (sql[i] == ')')
                    depth++;
                else if (sql[i] == '(')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Pass 3: Extract columns from ON, GROUP BY, and SELECT clauses.
        /// </summary>
        private void Pass3_ExtractUsedColumns()
        {
            // Extract from ON clauses
            var onMatches = OnConditionPattern.Matches(_sql);
            foreach (Match match in onMatches)
            {
                AddColumnUsage(match.Groups["leftAlias"].Value, match.Groups["leftCol"].Value, XmSqlColumnUsage.Join);
                AddColumnUsage(match.Groups["rightAlias"].Value, match.Groups["rightCol"].Value, XmSqlColumnUsage.Join);
            }

            // Extract from GROUP BY clauses
            var groupByMatches = GroupByColumnPattern.Matches(_sql);
            foreach (Match gbMatch in groupByMatches)
            {
                var columnsText = gbMatch.Groups["columns"].Value;
                var colRefs = Regex.Matches(columnsText, @"\[(?<alias>[^\]]+)\]\s*\.\s*\[(?<col>[^\]]+)\]");
                foreach (Match colRef in colRefs)
                {
                    AddColumnUsage(colRef.Groups["alias"].Value, colRef.Groups["col"].Value, XmSqlColumnUsage.GroupBy);
                }
            }
        }

        /// <summary>
        /// Pass 4: Extract relationships from ON clauses.
        /// </summary>
        private void Pass4_ExtractRelationships()
        {
            var onMatches = OnConditionPattern.Matches(_sql);
            foreach (Match match in onMatches)
            {
                var leftAlias = match.Groups["leftAlias"].Value;
                var leftCol = match.Groups["leftCol"].Value;
                var rightAlias = match.Groups["rightAlias"].Value;
                var rightCol = match.Groups["rightCol"].Value;

                // Resolve to actual tables and columns
                var leftResolved = ResolveColumn(leftAlias, leftCol);
                var rightResolved = ResolveColumn(rightAlias, rightCol);

                if (leftResolved != null && rightResolved != null)
                {
                    // Skip if same table (self-join artifacts)
                    if (leftResolved.TableName.Equals(rightResolved.TableName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Check for duplicate
                    bool exists = Relationships.Any(r =>
                        (r.FromTable.Equals(leftResolved.TableName, StringComparison.OrdinalIgnoreCase) &&
                         r.FromColumn.Equals(leftResolved.ColumnName, StringComparison.OrdinalIgnoreCase) &&
                         r.ToTable.Equals(rightResolved.TableName, StringComparison.OrdinalIgnoreCase) &&
                         r.ToColumn.Equals(rightResolved.ColumnName, StringComparison.OrdinalIgnoreCase)) ||
                        (r.FromTable.Equals(rightResolved.TableName, StringComparison.OrdinalIgnoreCase) &&
                         r.FromColumn.Equals(rightResolved.ColumnName, StringComparison.OrdinalIgnoreCase) &&
                         r.ToTable.Equals(leftResolved.TableName, StringComparison.OrdinalIgnoreCase) &&
                         r.ToColumn.Equals(leftResolved.ColumnName, StringComparison.OrdinalIgnoreCase)));

                    if (!exists)
                    {
                        Relationships.Add(new DqRelationship
                        {
                            FromTable = leftResolved.TableName,
                            FromColumn = leftResolved.ColumnName,
                            ToTable = rightResolved.TableName,
                            ToColumn = rightResolved.ColumnName,
                            JoinType = DetermineJoinType(match.Index)
                        });
                    }
                }
            }
        }

        private void AddColumnUsage(string alias, string columnOrAlias, XmSqlColumnUsage usage)
        {
            var resolved = ResolveColumn(alias, columnOrAlias);
            if (resolved != null)
            {
                EnsureTableColumn(resolved.TableName, resolved.ColumnName, usage);
            }
        }

        private ColumnMapping ResolveColumn(string alias, string columnOrAlias)
        {
            // First try: direct column alias lookup
            var key = $"{alias}.{columnOrAlias}";
            if (_columnAliasMap.TryGetValue(key, out var mapping))
            {
                return mapping;
            }

            // Second try: alias maps directly to a table
            if (_aliasMap.TryGetValue(alias, out var aliasInfo))
            {
                if (aliasInfo.IsBaseTable)
                {
                    return new ColumnMapping
                    {
                        TableName = aliasInfo.BaseTableName,
                        ColumnName = columnOrAlias
                    };
                }
                else
                {
                    // Composite alias (semijoin, basetable) - need to trace the column
                    // The column name might be like "c11" which we need to resolve
                    // Search all column mappings for this output alias
                    foreach (var kvp in _columnAliasMap)
                    {
                        if (kvp.Key.EndsWith($".{columnOrAlias}", StringComparison.OrdinalIgnoreCase))
                        {
                            return kvp.Value;
                        }
                    }
                }
            }

            // Third try: columnOrAlias might be the actual column name from a known table
            // (when alias IS a table name due to direct reference)
            if (Tables.ContainsKey(alias))
            {
                return new ColumnMapping
                {
                    TableName = alias,
                    ColumnName = columnOrAlias
                };
            }

            // Fourth try: Use the hierarchical block parser for deep resolution
            if (_blockParser != null)
            {
                var resolved = _blockParser.ResolveColumn(alias, columnOrAlias);
                if (resolved.HasValue)
                {
                    Log.Debug("BlockParser resolved [{Alias}].[{Col}] -> {Table}.{ActualCol}",
                        alias, columnOrAlias, resolved.Value.TableName, resolved.Value.ColumnName);
                    return new ColumnMapping
                    {
                        TableName = resolved.Value.TableName,
                        ColumnName = resolved.Value.ColumnName
                    };
                }
            }

            return null;
        }

        private void EnsureTable(string tableName)
        {
            if (!Tables.ContainsKey(tableName))
            {
                Tables[tableName] = new DqTableInfo(tableName);
            }
        }

        private void EnsureTableColumn(string tableName, string columnName, XmSqlColumnUsage usage)
        {
            EnsureTable(tableName);
            var table = Tables[tableName];

            if (!table.Columns.TryGetValue(columnName, out var column))
            {
                column = new DqColumnInfo(columnName);
                table.Columns[columnName] = column;
            }
            column.Usages.Add(usage);
        }

        private XmSqlJoinType DetermineJoinType(int position)
        {
            var beforeOn = _sql.Substring(0, position);
            var lastLeftOuter = beforeOn.LastIndexOf("LEFT OUTER JOIN", StringComparison.OrdinalIgnoreCase);
            var lastInner = beforeOn.LastIndexOf("INNER JOIN", StringComparison.OrdinalIgnoreCase);

            if (lastLeftOuter > lastInner)
                return XmSqlJoinType.LeftOuterJoin;
            return XmSqlJoinType.InnerJoin;
        }

        #region Helper Classes

        private class AliasInfo
        {
            public bool IsBaseTable { get; set; }
            public string BaseTableName { get; set; }
            public List<string> SourceAliases { get; set; }
        }

        private class ColumnMapping
        {
            public string TableName { get; set; }
            public string ColumnName { get; set; }
        }

        #endregion
    }

    #region Result Classes

    public class DqTableInfo
    {
        public string Name { get; }
        public Dictionary<string, DqColumnInfo> Columns { get; } = new Dictionary<string, DqColumnInfo>(StringComparer.OrdinalIgnoreCase);

        public DqTableInfo(string name)
        {
            Name = name;
        }
    }

    public class DqColumnInfo
    {
        public string Name { get; }
        public HashSet<XmSqlColumnUsage> Usages { get; } = new HashSet<XmSqlColumnUsage>();

        public DqColumnInfo(string name)
        {
            Name = name;
        }
    }

    public class DqRelationship
    {
        public string FromTable { get; set; }
        public string FromColumn { get; set; }
        public string ToTable { get; set; }
        public string ToColumn { get; set; }
        public XmSqlJoinType JoinType { get; set; }
    }

    #endregion
}
