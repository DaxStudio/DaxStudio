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
        // (select [$Table].[col1] as [c1], ... from [schema].[TableName] as [$Table]) AS [t4]
        // Also handles extra wrapper parens: ((...)) AS [t4]
        // Supports any schema name
        private static readonly Regex SubselectWithDollarTablePattern = new Regex(
            @"\(\s*\(*\s*select\s+(?<columns>.*?)\s+from\s+\[(?<schema>[^\]]+)\]\s*\.\s*\[(?<table>[^\]]+)\]\s+as\s+\[\$Table\]\s*\)\s*\)*\s*AS\s+\[(?<alias>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Pattern 2: Direct table reference [schema].[TableName] AS [alias]
        // Handles both standalone and in JOIN context
        // Supports any schema name (dbo, sys, gold, raw, staging, etc.)
        private static readonly Regex DirectTableAliasPattern = new Regex(
            @"\[(?<schema>[^\]]+)\]\s*\.\s*\[(?<table>[^\]]+)\]\s+AS\s+\[(?<alias>[^\]]+)\]",
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

        // Pattern 9: Schema-less table reference (no dot) - [TableName] AS [alias]
        // Some DirectQuery sources use simple table names without schema
        private static readonly Regex SchemalessTablePattern = new Regex(
            @"(?:FROM|JOIN)\s+\[(?<table>[^\]\.]+)\]\s+(?:AS\s+)?\[(?<alias>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern 10: OPENQUERY pattern - OPENQUERY(LinkedServer, 'SELECT ... FROM table')
        private static readonly Regex OpenQueryPattern = new Regex(
            @"OPENQUERY\s*\(\s*\[?(?<server>[^\],\)]+)\]?\s*,\s*'(?<innerQuery>[^']+)'",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Pattern 11: 4-part naming - [server].[database].[schema].[table] AS [alias]
        private static readonly Regex FourPartNamePattern = new Regex(
            @"\[(?<server>[^\]]+)\]\.\[(?<database>[^\]]+)\]\.\[(?<schema>[^\]]+)\]\.\[(?<table>[^\]]+)\]\s+AS\s+\[(?<alias>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern 12: 3-part naming - [database].[schema].[table] AS [alias]
        private static readonly Regex ThreePartNamePattern = new Regex(
            @"(?<!\.)\[(?<database>[^\]]+)\]\.\[(?<schema>[^\]]+)\]\.\[(?<table>[^\]]+)\]\s+AS\s+\[(?<alias>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern 13: Unbracketed table names - FROM schema.table AS alias (no brackets)
        // Common in PostgreSQL, MySQL, Databricks, Snowflake, etc.
        private static readonly Regex UnbracketedTablePattern = new Regex(
            @"(?:FROM|JOIN)\s+(?<schema>\w+)\.(?<table>\w+)\s+(?:AS\s+)?(?<alias>\w+)(?=\s|$|,|\))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern 14: Unbracketed table without schema - FROM table AS alias
        private static readonly Regex UnbracketedSimpleTablePattern = new Regex(
            @"(?:FROM|JOIN)\s+(?<table>\w+)\s+(?:AS\s+)?(?<alias>\w+)(?=\s+(?:ON|WHERE|INNER|LEFT|RIGHT|FULL|CROSS|GROUP|ORDER|HAVING|\)|$))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern 15: Quoted table names with double quotes (common in PostgreSQL, Oracle)
        private static readonly Regex DoubleQuotedTablePattern = new Regex(
            @"(?:FROM|JOIN)\s+""(?<schema>[^""]+)""\s*\.\s*""(?<table>[^""]+)""\s+(?:AS\s+)?""?(?<alias>\w+)""?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern 16: Backtick quoted tables (MySQL, BigQuery)
        private static readonly Regex BacktickTablePattern = new Regex(
            @"(?:FROM|JOIN)\s+`(?<schema>[^`]+)`\s*\.\s*`(?<table>[^`]+)`\s+(?:AS\s+)?`?(?<alias>\w+)`?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern 17: Simple bracketed table without schema - [Table Name] AS [alias]
        // Very common in DirectQuery to SQL Server/Azure SQL without explicit schema
        // Note: Table name can contain spaces. Matches anywhere, not just after FROM/JOIN
        private static readonly Regex SimpleBracketedTablePattern = new Regex(
            @"(?<!\[)\[(?<table>[^\]\.]+)\]\s+AS\s+\[(?<alias>[^\]]+)\]",
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

            // Pattern 4-part naming: [server].[database].[schema].[table] AS [alias]
            var fourPartMatches = FourPartNamePattern.Matches(_sql);
            foreach (Match match in fourPartMatches)
            {
                var tableName = match.Groups["table"].Value;
                var alias = match.Groups["alias"].Value;

                if (alias.Equals("$Table", StringComparison.OrdinalIgnoreCase))
                    continue;
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
                    Log.Debug("Pass1 4-part: [{Alias}] -> {Table}", alias, tableName);
                }
            }

            // Pattern 3-part naming: [database].[schema].[table] AS [alias]
            var threePartMatches = ThreePartNamePattern.Matches(_sql);
            foreach (Match match in threePartMatches)
            {
                var tableName = match.Groups["table"].Value;
                var alias = match.Groups["alias"].Value;

                if (alias.Equals("$Table", StringComparison.OrdinalIgnoreCase))
                    continue;
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
                    Log.Debug("Pass1 3-part: [{Alias}] -> {Table}", alias, tableName);
                }
            }

            // Pattern schema-less: FROM [TableName] AS [alias] or JOIN [TableName] [alias]
            var schemalessMatches = SchemalessTablePattern.Matches(_sql);
            foreach (Match match in schemalessMatches)
            {
                var tableName = match.Groups["table"].Value;
                var alias = match.Groups["alias"].Value;

                if (alias.Equals("$Table", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (_aliasMap.ContainsKey(alias))
                    continue;
                // Skip if table name contains a dot (it was part of a schema.table pattern)
                if (tableName.Contains("."))
                    continue;

                if (!string.IsNullOrEmpty(tableName) && !string.IsNullOrEmpty(alias))
                {
                    EnsureTable(tableName);
                    _aliasMap[alias] = new AliasInfo
                    {
                        IsBaseTable = true,
                        BaseTableName = tableName
                    };
                    Log.Debug("Pass1 schemaless: [{Alias}] -> {Table}", alias, tableName);
                }
            }

            // OPENQUERY pattern - extract tables from inner SQL
            var openQueryMatches = OpenQueryPattern.Matches(_sql);
            foreach (Match match in openQueryMatches)
            {
                var server = match.Groups["server"].Value;
                var innerQuery = match.Groups["innerQuery"].Value;
                
                // Try to extract table name from inner query (simple FROM clause)
                var innerFromMatch = Regex.Match(innerQuery, 
                    @"\bFROM\s+(?:\[?(?<schema>\w+)\]?\.)?\[?(?<table>\w+)\]?",
                    RegexOptions.IgnoreCase);
                
                if (innerFromMatch.Success)
                {
                    var tableName = innerFromMatch.Groups["table"].Value;
                    if (!string.IsNullOrEmpty(tableName))
                    {
                        EnsureTable(tableName);
                        Log.Debug("Pass1 OPENQUERY: Found table {Table} from server {Server}", tableName, server);
                    }
                }
            }

            // Pattern: Unbracketed schema.table (PostgreSQL, MySQL, Databricks, Snowflake style)
            var unbracketedMatches = UnbracketedTablePattern.Matches(_sql);
            foreach (Match match in unbracketedMatches)
            {
                var tableName = match.Groups["table"].Value;
                var alias = match.Groups["alias"].Value;

                // Skip SQL keywords that might be captured
                if (IsReservedWord(alias) || IsReservedWord(tableName))
                    continue;
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
                    Log.Debug("Pass1 unbracketed: [{Alias}] -> {Table}", alias, tableName);
                }
            }

            // Pattern: Unbracketed simple table (no schema)
            var unbracketedSimpleMatches = UnbracketedSimpleTablePattern.Matches(_sql);
            foreach (Match match in unbracketedSimpleMatches)
            {
                var tableName = match.Groups["table"].Value;
                var alias = match.Groups["alias"].Value;

                if (IsReservedWord(alias) || IsReservedWord(tableName))
                    continue;
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
                    Log.Debug("Pass1 unbracketed-simple: [{Alias}] -> {Table}", alias, tableName);
                }
            }

            // Pattern: Double-quoted tables (PostgreSQL, Oracle)
            var doubleQuotedMatches = DoubleQuotedTablePattern.Matches(_sql);
            foreach (Match match in doubleQuotedMatches)
            {
                var tableName = match.Groups["table"].Value;
                var alias = match.Groups["alias"].Value;

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
                    Log.Debug("Pass1 double-quoted: [{Alias}] -> {Table}", alias, tableName);
                }
            }

            // Pattern: Backtick-quoted tables (MySQL, BigQuery)
            var backtickMatches = BacktickTablePattern.Matches(_sql);
            foreach (Match match in backtickMatches)
            {
                var tableName = match.Groups["table"].Value;
                var alias = match.Groups["alias"].Value;

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
                    Log.Debug("Pass1 backtick: [{Alias}] -> {Table}", alias, tableName);
                }
            }

            // Pattern 17: Simple bracketed table without schema [Table Name] AS [alias]
            // This is very common in DirectQuery to SQL Server/Azure SQL
            // Run LAST so it doesn't interfere with more specific patterns
            var simpleBracketedMatches = SimpleBracketedTablePattern.Matches(_sql);
            foreach (Match match in simpleBracketedMatches)
            {
                var tableName = match.Groups["table"].Value;
                var alias = match.Groups["alias"].Value;

                // Skip $Table references (those are internal to subselects)
                if (alias.Equals("$Table", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip aliases that look like output column aliases (a0, c0, etc.)
                if (Regex.IsMatch(alias, @"^[ac]\d+$", RegexOptions.IgnoreCase))
                    continue;

                // Skip if we already have this alias
                if (_aliasMap.ContainsKey(alias))
                    continue;

                // Skip if the "table" name is actually a column pattern (like t4.Column)
                // This can happen due to the regex being broad
                if (tableName.StartsWith("t") && Regex.IsMatch(tableName, @"^t\d+$", RegexOptions.IgnoreCase))
                    continue;

                if (!string.IsNullOrEmpty(tableName) && !string.IsNullOrEmpty(alias))
                {
                    EnsureTable(tableName);
                    _aliasMap[alias] = new AliasInfo
                    {
                        IsBaseTable = true,
                        BaseTableName = tableName
                    };
                    Log.Debug("Pass1 simple-bracketed: [{Alias}] -> {Table}", alias, tableName);
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
        /// Pass 3: Extract columns from ON, WHERE, GROUP BY, aggregates, and SELECT clauses.
        /// </summary>
        private void Pass3_ExtractUsedColumns()
        {
            // Extract from ON clauses (join columns)
            var onMatches = OnConditionPattern.Matches(_sql);
            foreach (Match match in onMatches)
            {
                AddColumnUsage(match.Groups["leftAlias"].Value, match.Groups["leftCol"].Value, XmSqlColumnUsage.Join);
                AddColumnUsage(match.Groups["rightAlias"].Value, match.Groups["rightCol"].Value, XmSqlColumnUsage.Join);
            }

            // Extract from WHERE clauses (filter columns)
            ExtractWhereClauseColumns();

            // Extract from aggregate functions (SUM, COUNT, etc.)
            ExtractAggregateColumns();

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
        /// Extract columns from WHERE clauses.
        /// </summary>
        private void ExtractWhereClauseColumns()
        {
            // Find WHERE clause(s) - can be multiple in nested subqueries
            // Pattern: WHERE ... (until GROUP BY, ORDER BY, HAVING, or end of subquery)
            var wherePattern = new Regex(
                @"\bWHERE\s+(?<conditions>.*?)(?=\bGROUP\s+BY\b|\bORDER\s+BY\b|\bHAVING\b|\)\s*(?:AS|INNER|LEFT|RIGHT|FULL|CROSS|$)|\)\s*$)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var whereMatches = wherePattern.Matches(_sql);
            foreach (Match whereMatch in whereMatches)
            {
                var conditions = whereMatch.Groups["conditions"].Value;
                
                // Extract column references [alias].[column] from the WHERE clause
                var colRefPattern = new Regex(@"\[(?<alias>[^\]]+)\]\s*\.\s*\[(?<col>[^\]]+)\]", RegexOptions.IgnoreCase);
                var colRefs = colRefPattern.Matches(conditions);
                
                foreach (Match colRef in colRefs)
                {
                    var alias = colRef.Groups["alias"].Value;
                    var col = colRef.Groups["col"].Value;
                    
                    // Skip if the alias looks like an output alias (a0, c0, etc.)
                    if (Regex.IsMatch(alias, @"^[ac]\d+$", RegexOptions.IgnoreCase))
                        continue;
                    
                    AddColumnUsage(alias, col, XmSqlColumnUsage.Filter);
                }
            }

            // Also look for simple comparisons that might not have alias prefix
            // e.g., WHERE [Column] = value or WHERE [Column] IN (...)
            var simpleWherePattern = new Regex(
                @"\bWHERE\b.*?\[(?<col>[^\]\.]+)\]\s*(?:=|<>|!=|<|>|<=|>=|IN\s*\(|LIKE|IS\s+(?:NOT\s+)?NULL|BETWEEN)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            var simpleMatches = simpleWherePattern.Matches(_sql);
            foreach (Match match in simpleMatches)
            {
                // These are harder to resolve without alias, but we can try
                // to find them in the context
                var col = match.Groups["col"].Value;
                // For now, just log that we found a potential filter column
                Log.Debug("Found potential unaliased filter column: {Column}", col);
            }
        }

        /// <summary>
        /// Extract columns from aggregate functions (SUM, COUNT, AVG, MIN, MAX, etc.)
        /// </summary>
        private void ExtractAggregateColumns()
        {
            // Pattern for aggregate functions with column references
            // Matches: COUNT_BIG(DISTINCT [t4].[Column]), SUM([t4].[Amount]), AVG([alias].[col]), etc.
            var aggregatePattern = new Regex(
                @"\b(?<func>COUNT_BIG|COUNT|SUM|AVG|MIN|MAX|STDEV|STDEVP|VAR|VARP|CHECKSUM_AGG)\s*\(\s*(?:DISTINCT\s+)?(?:\[(?<alias>[^\]]+)\]\s*\.\s*)?\[(?<col>[^\]]+)\]",
                RegexOptions.IgnoreCase);

            var matches = aggregatePattern.Matches(_sql);
            foreach (Match match in matches)
            {
                var func = match.Groups["func"].Value.ToUpperInvariant();
                var alias = match.Groups["alias"].Value;
                var col = match.Groups["col"].Value;

                // All aggregate functions use the Aggregate usage type
                XmSqlColumnUsage usage = XmSqlColumnUsage.Aggregate;

                if (!string.IsNullOrEmpty(alias) && !string.IsNullOrEmpty(col))
                {
                    // Skip output aliases like a0, c0
                    if (Regex.IsMatch(alias, @"^[ac]\d+$", RegexOptions.IgnoreCase))
                        continue;

                    AddColumnUsage(alias, col, usage);
                    Log.Debug("Found aggregate column: {Func}([{Alias}].[{Col}])", func, alias, col);
                }
                else if (!string.IsNullOrEmpty(col))
                {
                    // Column without alias - try to find it
                    Log.Debug("Found unaliased aggregate column: {Func}([{Col}])", func, col);
                }
            }

            // Also look for CASE WHEN expressions with column references
            var casePattern = new Regex(
                @"\bCASE\s+WHEN\s+\[(?<alias>[^\]]+)\]\s*\.\s*\[(?<col>[^\]]+)\]",
                RegexOptions.IgnoreCase);

            var caseMatches = casePattern.Matches(_sql);
            foreach (Match match in caseMatches)
            {
                var alias = match.Groups["alias"].Value;
                var col = match.Groups["col"].Value;

                if (!string.IsNullOrEmpty(alias) && !string.IsNullOrEmpty(col))
                {
                    if (Regex.IsMatch(alias, @"^[ac]\d+$", RegexOptions.IgnoreCase))
                        continue;

                    // CASE WHEN typically used for conditional aggregation or filtering
                    AddColumnUsage(alias, col, XmSqlColumnUsage.Aggregate);
                    Log.Debug("Found CASE WHEN column: [{Alias}].[{Col}]", alias, col);
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

        // Common SQL reserved words to exclude when pattern matching unbracketed tables
        private static readonly HashSet<string> ReservedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT", "FROM", "WHERE", "AND", "OR", "NOT", "IN", "IS", "NULL", "LIKE",
            "INNER", "LEFT", "RIGHT", "OUTER", "FULL", "CROSS", "JOIN", "ON",
            "GROUP", "BY", "HAVING", "ORDER", "ASC", "DESC", "LIMIT", "TOP", "OFFSET",
            "UNION", "ALL", "DISTINCT", "AS", "INTO", "VALUES", "INSERT", "UPDATE", "DELETE",
            "SET", "CREATE", "ALTER", "DROP", "TABLE", "INDEX", "VIEW", "PROCEDURE",
            "FUNCTION", "TRIGGER", "DATABASE", "SCHEMA", "COLUMN", "CONSTRAINT",
            "PRIMARY", "KEY", "FOREIGN", "REFERENCES", "UNIQUE", "CHECK", "DEFAULT",
            "CASE", "WHEN", "THEN", "ELSE", "END", "CAST", "CONVERT", "COALESCE",
            "COUNT", "SUM", "AVG", "MIN", "MAX", "OVER", "PARTITION", "ROW_NUMBER",
            "TRUE", "FALSE", "WITH", "RECURSIVE", "EXISTS", "ANY", "SOME",
            "BETWEEN", "ESCAPE", "USING", "NATURAL"
        };

        private static bool IsReservedWord(string word)
        {
            return ReservedWords.Contains(word);
        }

        #endregion

        #region Diagnostics

        /// <summary>
        /// Gets diagnostic information about what was parsed.
        /// </summary>
        public string GetDiagnostics()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== ALIAS MAP ===");
            foreach (var kvp in _aliasMap)
            {
                if (kvp.Value.IsBaseTable)
                    sb.AppendLine($"  [{kvp.Key}] -> {kvp.Value.BaseTableName}");
                else
                    sb.AppendLine($"  [{kvp.Key}] -> (composite)");
            }
            
            sb.AppendLine();
            sb.AppendLine("=== COLUMN ALIAS MAP ===");
            foreach (var kvp in _columnAliasMap)
            {
                sb.AppendLine($"  [{kvp.Key}] -> {kvp.Value.TableName}.{kvp.Value.ColumnName}");
            }
            
            sb.AppendLine();
            sb.AppendLine("=== TABLES FOUND ===");
            foreach (var table in Tables.Keys)
            {
                sb.AppendLine($"  {table}");
            }
            
            sb.AppendLine();
            sb.AppendLine("=== RELATIONSHIPS FOUND ===");
            foreach (var rel in Relationships)
            {
                sb.AppendLine($"  [{rel.FromTable}].[{rel.FromColumn}] -> [{rel.ToTable}].[{rel.ToColumn}]");
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Gets a sample of the SQL for debugging (first N characters).
        /// </summary>
        public string GetSqlSample(int length = 200)
        {
            if (string.IsNullOrEmpty(_sql))
                return "(empty)";
            return _sql.Length <= length ? _sql : _sql.Substring(0, length) + "...";
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
