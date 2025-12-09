using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog;

namespace DaxStudio.UI.Utils
{
    /// <summary>
    /// Represents a block (parenthesized subquery) in a SQL statement.
    /// Builds a hierarchical tree structure for complex nested SQL.
    /// </summary>
    public class SqlBlock
    {
        /// <summary>
        /// Start position in the SQL string (position of opening paren).
        /// </summary>
        public int StartPos { get; set; }

        /// <summary>
        /// End position in the SQL string (position of closing paren).
        /// </summary>
        public int EndPos { get; set; }

        /// <summary>
        /// The alias assigned to this block, e.g., "t4", "basetable0", "semijoin1".
        /// Null if no alias.
        /// </summary>
        public string Alias { get; set; }

        /// <summary>
        /// Parent block that contains this one. Null for root.
        /// </summary>
        public SqlBlock Parent { get; set; }

        /// <summary>
        /// Child blocks contained within this one.
        /// </summary>
        public List<SqlBlock> Children { get; } = new List<SqlBlock>();

        /// <summary>
        /// The type of block: "select", "dollarTable", "join", "root", "wrapper"
        /// </summary>
        public string BlockType { get; set; }

        /// <summary>
        /// For [$Table] blocks, the actual table name.
        /// </summary>
        public string BaseTable { get; set; }

        /// <summary>
        /// For SELECT blocks, maps output column alias to source expression.
        /// e.g., "c11" -> "[t4].[Date]"
        /// </summary>
        public Dictionary<string, string> OutputColumns { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// For [$Table] blocks, maps column alias to actual column name.
        /// e.g., "Date" -> "Date" (when SELECT [$Table].[Date] as [Date])
        /// </summary>
        public Dictionary<string, string> BaseTableColumns { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Raw content of this block (between the parens).
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Nesting depth (0 = root/outermost).
        /// </summary>
        public int Depth { get; set; }

        /// <summary>
        /// For join blocks, the join type (INNER, LEFT, etc.)
        /// </summary>
        public string JoinType { get; set; }

        /// <summary>
        /// For join blocks, the ON condition columns.
        /// List of (leftAlias, leftCol, rightAlias, rightCol) tuples.
        /// </summary>
        public List<(string LeftAlias, string LeftCol, string RightAlias, string RightCol)> JoinConditions { get; } 
            = new List<(string, string, string, string)>();

        public override string ToString()
        {
            return $"[{BlockType}] Alias={Alias ?? "(none)"}, Depth={Depth}, BaseTable={BaseTable ?? "(none)"}, Children={Children.Count}";
        }
    }

    /// <summary>
    /// Parses SQL into a hierarchical block structure based on parentheses.
    /// This provides a foundation for resolving column references through nested subqueries.
    /// </summary>
    public class SqlBlockParser
    {
        private readonly string _sql;
        private SqlBlock _root;
        private readonly Dictionary<string, SqlBlock> _blocksByAlias = new Dictionary<string, SqlBlock>(StringComparer.OrdinalIgnoreCase);

        // Regex patterns
        private static readonly Regex AliasAfterParenPattern = new Regex(
            @"^\s*AS\s+\[(?<alias>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex DollarTablePattern = new Regex(
            @"from\s+\[(?<schema>[^\]]+)\]\s*\.\s*\[(?<table>[^\]]+)\]\s+as\s+\[\$Table\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex DollarTableColumnPattern = new Regex(
            @"\[\$Table\]\s*\.\s*\[(?<column>[^\]]+)\](?:\s+as\s+\[(?<alias>[^\]]+)\])?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex SelectColumnPattern = new Regex(
            @"\[(?<sourceAlias>[^\]]+)\]\s*\.\s*\[(?<sourceCol>[^\]]+)\]\s+AS\s+\[(?<outputAlias>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex DirectTablePattern = new Regex(
            @"\[(?<schema>dbo|sys)\]\s*\.\s*\[(?<table>[^\]]+)\]\s+AS\s+\[(?<alias>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex JoinPattern = new Regex(
            @"(?<joinType>INNER|LEFT\s+OUTER|RIGHT\s+OUTER|FULL\s+OUTER|CROSS)?\s*JOIN",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex OnConditionPattern = new Regex(
            @"\[(?<leftAlias>[^\]]+)\]\s*\.\s*\[(?<leftCol>[^\]]+)\]\s*=\s*\[(?<rightAlias>[^\]]+)\]\s*\.\s*\[(?<rightCol>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public SqlBlockParser(string sql)
        {
            _sql = sql ?? string.Empty;
        }

        /// <summary>
        /// Parse the SQL into a hierarchical block structure.
        /// </summary>
        public SqlBlock Parse()
        {
            if (string.IsNullOrWhiteSpace(_sql))
                return null;

            try
            {
                // Step 1: Build block tree from parentheses
                _root = BuildBlockTree();

                // Step 2: Classify blocks and extract metadata
                ClassifyBlocks(_root);

                // Step 3: Find direct table references (outside of [$Table] subselects)
                FindDirectTableReferences();

                // Step 4: Extract column mappings from each block
                ExtractColumnMappings(_root);

                // Step 5: Extract join conditions
                ExtractJoinConditions();

                return _root;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "SqlBlockParser failed to parse SQL");
                return null;
            }
        }

        /// <summary>
        /// Find direct table references like [dbo].[TableName] AS [alias] that aren't [$Table] subselects.
        /// </summary>
        private void FindDirectTableReferences()
        {
            var directMatches = DirectTablePattern.Matches(_sql);
            foreach (Match match in directMatches)
            {
                var tableName = match.Groups["table"].Value;
                var alias = match.Groups["alias"].Value;

                // Skip $Table references (those are handled by dollarTable blocks)
                if (alias.Equals("$Table", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip if we already have this alias
                if (_blocksByAlias.ContainsKey(alias))
                    continue;

                // Create a virtual block for this direct table reference
                var virtualBlock = new SqlBlock
                {
                    Alias = alias,
                    BlockType = "directTable",
                    BaseTable = tableName,
                    Depth = -1 // Virtual, not in actual paren structure
                };
                _blocksByAlias[alias] = virtualBlock;
                
                Log.Debug("Found direct table reference: [{Alias}] -> {Table}", alias, tableName);
            }
        }

        /// <summary>
        /// Get a block by its alias.
        /// </summary>
        public SqlBlock GetBlockByAlias(string alias)
        {
            _blocksByAlias.TryGetValue(alias, out var block);
            return block;
        }

        /// <summary>
        /// Get all blocks that represent base tables ([$Table] subselects).
        /// </summary>
        public IEnumerable<SqlBlock> GetBaseTableBlocks()
        {
            return GetAllBlocks(_root).Where(b => b.BlockType == "dollarTable" && !string.IsNullOrEmpty(b.BaseTable));
        }

        /// <summary>
        /// Get all blocks flattened.
        /// </summary>
        public IEnumerable<SqlBlock> GetAllBlocks(SqlBlock root = null)
        {
            root = root ?? _root;
            if (root == null) yield break;

            yield return root;
            foreach (var child in root.Children)
            {
                foreach (var descendant in GetAllBlocks(child))
                {
                    yield return descendant;
                }
            }
        }

        /// <summary>
        /// Resolve a column reference through the block hierarchy.
        /// e.g., ResolveColumn("basetable0", "c11") -> ("Distribution_Volume_Weekly", "Date")
        /// </summary>
        public (string TableName, string ColumnName)? ResolveColumn(string alias, string columnName, int maxDepth = 10)
        {
            if (maxDepth <= 0) return null;

            // First, try to find the block with this alias
            if (!_blocksByAlias.TryGetValue(alias, out var block))
            {
                return null;
            }

            // If this is a direct table reference (from JOIN), return directly
            if (block.BlockType == "directTable" && !string.IsNullOrEmpty(block.BaseTable))
            {
                return (block.BaseTable, columnName);
            }

            // If this is a base table block ([$Table] subselect), return directly
            if (block.BlockType == "dollarTable" && !string.IsNullOrEmpty(block.BaseTable))
            {
                // Check if the column exists in base table columns
                if (block.BaseTableColumns.TryGetValue(columnName, out var actualCol))
                {
                    return (block.BaseTable, actualCol);
                }
                // Column name might be the actual column
                return (block.BaseTable, columnName);
            }

            // If this block has output columns, trace through them
            if (block.OutputColumns.TryGetValue(columnName, out var sourceExpr))
            {
                // Parse the source expression: [sourceAlias].[sourceCol]
                var match = Regex.Match(sourceExpr, @"\[(?<alias>[^\]]+)\]\s*\.\s*\[(?<col>[^\]]+)\]");
                if (match.Success)
                {
                    var sourceAlias = match.Groups["alias"].Value;
                    var sourceCol = match.Groups["col"].Value;

                    // Recursively resolve
                    return ResolveColumn(sourceAlias, sourceCol, maxDepth - 1);
                }
            }

            // Try looking in child blocks
            foreach (var child in block.Children)
            {
                if (child.Alias != null && child.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase))
                    continue; // Skip self

                if (child.BlockType == "dollarTable" && !string.IsNullOrEmpty(child.BaseTable))
                {
                    if (child.BaseTableColumns.TryGetValue(columnName, out var actualCol))
                    {
                        return (child.BaseTable, actualCol);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Build the block tree by parsing parentheses.
        /// </summary>
        private SqlBlock BuildBlockTree()
        {
            var root = new SqlBlock
            {
                StartPos = 0,
                EndPos = _sql.Length - 1,
                BlockType = "root",
                Depth = 0,
                Content = _sql
            };

            var stack = new Stack<SqlBlock>();
            stack.Push(root);

            for (int i = 0; i < _sql.Length; i++)
            {
                if (_sql[i] == '(')
                {
                    var newBlock = new SqlBlock
                    {
                        StartPos = i,
                        Parent = stack.Peek(),
                        Depth = stack.Count
                    };
                    stack.Peek().Children.Add(newBlock);
                    stack.Push(newBlock);
                }
                else if (_sql[i] == ')')
                {
                    if (stack.Count > 1) // Don't pop root
                    {
                        var block = stack.Pop();
                        block.EndPos = i;
                        block.Content = _sql.Substring(block.StartPos + 1, block.EndPos - block.StartPos - 1);

                        // Check for alias after closing paren
                        var afterParen = _sql.Substring(i + 1);
                        var aliasMatch = AliasAfterParenPattern.Match(afterParen);
                        if (aliasMatch.Success)
                        {
                            block.Alias = aliasMatch.Groups["alias"].Value;
                            _blocksByAlias[block.Alias] = block;
                        }
                    }
                }
            }

            return root;
        }

        /// <summary>
        /// Classify each block based on its content.
        /// </summary>
        private void ClassifyBlocks(SqlBlock block)
        {
            if (block == null) return;

            // Check for [$Table] pattern - this is a base table subselect
            var dollarMatch = DollarTablePattern.Match(block.Content ?? "");
            if (dollarMatch.Success)
            {
                block.BlockType = "dollarTable";
                block.BaseTable = dollarMatch.Groups["table"].Value;

                // Extract column mappings
                var colMatches = DollarTableColumnPattern.Matches(block.Content);
                foreach (Match colMatch in colMatches)
                {
                    var actualColumn = colMatch.Groups["column"].Value;
                    var colAlias = colMatch.Groups["alias"].Value;
                    if (string.IsNullOrEmpty(colAlias))
                        colAlias = actualColumn;
                    block.BaseTableColumns[colAlias] = actualColumn;
                }

                Log.Debug("Block [{Alias}] is dollarTable -> {Table} with {ColCount} columns",
                    block.Alias ?? "(no alias)", block.BaseTable, block.BaseTableColumns.Count);
            }
            // Check for direct table reference
            else if (block.Content != null)
            {
                var directMatch = DirectTablePattern.Match(block.Content);
                if (directMatch.Success && block.Children.Count == 0)
                {
                    // This block directly references a table
                    var tableAlias = directMatch.Groups["alias"].Value;
                    var tableName = directMatch.Groups["table"].Value;

                    // Create a virtual block for this direct reference
                    if (!_blocksByAlias.ContainsKey(tableAlias))
                    {
                        var virtualBlock = new SqlBlock
                        {
                            Alias = tableAlias,
                            BlockType = "directTable",
                            BaseTable = tableName,
                            Parent = block,
                            Depth = block.Depth + 1
                        };
                        _blocksByAlias[tableAlias] = virtualBlock;
                        Log.Debug("Found direct table reference: [{Alias}] -> {Table}", tableAlias, tableName);
                    }
                }

                // Check if it's a SELECT block
                if (block.Content.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
                    block.Content.TrimStart().StartsWith("select", StringComparison.OrdinalIgnoreCase))
                {
                    if (block.BlockType != "dollarTable")
                        block.BlockType = "select";
                }
                else if (block.BlockType == null)
                {
                    block.BlockType = "wrapper";
                }
            }

            // Recursively classify children
            foreach (var child in block.Children)
            {
                ClassifyBlocks(child);
            }
        }

        /// <summary>
        /// Extract column mappings from SELECT blocks.
        /// </summary>
        private void ExtractColumnMappings(SqlBlock block)
        {
            if (block == null) return;

            if ((block.BlockType == "select" || block.BlockType == "root") && block.Content != null)
            {
                // Find SELECT ... FROM pattern
                var selectMatch = Regex.Match(block.Content,
                    @"SELECT\s+(?:TOP\s*\(\d+\)\s+)?(?<cols>.*?)\s+FROM",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                if (selectMatch.Success)
                {
                    var colsText = selectMatch.Groups["cols"].Value;
                    var colMatches = SelectColumnPattern.Matches(colsText);

                    foreach (Match colMatch in colMatches)
                    {
                        var sourceAlias = colMatch.Groups["sourceAlias"].Value;
                        var sourceCol = colMatch.Groups["sourceCol"].Value;
                        var outputAlias = colMatch.Groups["outputAlias"].Value;

                        // Skip TOP artifacts
                        if (sourceAlias.Equals("TOP", StringComparison.OrdinalIgnoreCase))
                            continue;

                        block.OutputColumns[outputAlias] = $"[{sourceAlias}].[{sourceCol}]";
                    }

                    if (block.OutputColumns.Count > 0)
                    {
                        Log.Debug("Block [{Alias}] has {Count} output column mappings",
                            block.Alias ?? "(no alias)", block.OutputColumns.Count);
                    }
                }
            }

            // Recursively process children
            foreach (var child in block.Children)
            {
                ExtractColumnMappings(child);
            }
        }

        /// <summary>
        /// Extract join conditions from ON clauses throughout the SQL.
        /// </summary>
        private void ExtractJoinConditions()
        {
            // Find all ON conditions in the SQL
            var onMatches = OnConditionPattern.Matches(_sql);
            foreach (Match match in onMatches)
            {
                var leftAlias = match.Groups["leftAlias"].Value;
                var leftCol = match.Groups["leftCol"].Value;
                var rightAlias = match.Groups["rightAlias"].Value;
                var rightCol = match.Groups["rightCol"].Value;

                // Store at root level for now
                _root?.JoinConditions.Add((leftAlias, leftCol, rightAlias, rightCol));
            }

            Log.Debug("Found {Count} join conditions", _root?.JoinConditions.Count ?? 0);
        }

        /// <summary>
        /// Debug: Print the block tree structure.
        /// </summary>
        public string GetTreeDiagram()
        {
            var sb = new System.Text.StringBuilder();
            PrintBlock(_root, sb, "");
            return sb.ToString();
        }

        private void PrintBlock(SqlBlock block, System.Text.StringBuilder sb, string indent)
        {
            if (block == null) return;

            sb.AppendLine($"{indent}{block}");

            if (block.OutputColumns.Count > 0)
            {
                foreach (var col in block.OutputColumns.Take(5))
                {
                    sb.AppendLine($"{indent}  Output: {col.Key} <- {col.Value}");
                }
                if (block.OutputColumns.Count > 5)
                    sb.AppendLine($"{indent}  ... and {block.OutputColumns.Count - 5} more");
            }

            if (block.BaseTableColumns.Count > 0)
            {
                foreach (var col in block.BaseTableColumns.Take(5))
                {
                    sb.AppendLine($"{indent}  Column: {col.Key} = {col.Value}");
                }
                if (block.BaseTableColumns.Count > 5)
                    sb.AppendLine($"{indent}  ... and {block.BaseTableColumns.Count - 5} more");
            }

            foreach (var child in block.Children)
            {
                PrintBlock(child, sb, indent + "  ");
            }
        }

        /// <summary>
        /// Get a compact summary of all aliases and their resolved base tables.
        /// </summary>
        public string GetAliasSummary()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== ALIAS MAP ===");
            
            foreach (var kvp in _blocksByAlias.OrderBy(k => k.Key))
            {
                var block = kvp.Value;
                var baseTable = block.BaseTable ?? "(composite)";
                var type = block.BlockType;
                sb.AppendLine($"  [{kvp.Key}] -> {baseTable} ({type})");
                
                // Show output columns for composite blocks
                if (block.OutputColumns.Count > 0 && block.OutputColumns.Count <= 10)
                {
                    foreach (var col in block.OutputColumns)
                    {
                        sb.AppendLine($"    .{col.Key} <- {col.Value}");
                    }
                }
            }
            
            if (_root?.JoinConditions.Count > 0)
            {
                sb.AppendLine("\n=== JOIN CONDITIONS ===");
                foreach (var jc in _root.JoinConditions)
                {
                    sb.AppendLine($"  [{jc.LeftAlias}].[{jc.LeftCol}] = [{jc.RightAlias}].[{jc.RightCol}]");
                }
            }
            
            return sb.ToString();
        }
    }
}
