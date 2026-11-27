using DaxStudio.Parsers.StorageEngine.XmSql;
using Parlot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DaxStudio.Parsers.StorageEngine.DirectQuery
{
    /// <summary>
    /// Walks a DirectQuery SQL tree from <see cref="DirectQuerySqlGrammar"/> and populates an XmSqlAnalysis,
    /// like <see cref="DirectQuerySqlAnalysisVisitor"/> does with the ANTLR tree, method for method.
    /// Handles alias resolution and column mapping for T-SQL DirectQuery statements.
    /// </summary>
    internal sealed class DirectQuerySqlAnalysisWalker
    {
        private readonly XmSqlAnalysis _analysis;
        private readonly XmSqlParser.SeEventMetrics _metrics;

        // Maps outer aliases (e.g. "t4") to physical table names (e.g. "Product")
        private readonly Dictionary<string, string> _aliasToTable
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Maps outer alias → { aliasColumn → sourceColumn }
        private readonly Dictionary<string, Dictionary<string, string>> _aliasColumnMap
            = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> AggregateFunctions = new HashSet<string>(StringComparer.Ordinal)
        {
            "SUM", "COUNT", "COUNT_BIG", "AVG", "MIN", "MAX", "STDEV", "STDEVP", "VAR", "VARP", "CHECKSUM_AGG",
        };

        public DirectQuerySqlAnalysisWalker(XmSqlAnalysis analysis, XmSqlParser.SeEventMetrics metrics)
        {
            _analysis = analysis;
            _metrics = metrics;
        }

        // ==================== HELPERS ====================

        private static string StripBrackets(string name)
        {
            if (name == null) return null;
            if (name.StartsWith("[") && name.EndsWith("]"))
                return name.Substring(1, name.Length - 2);
            return name;
        }

        private static string GetBracketedName(TextSpan token) => StripBrackets(token.GetText());

        private void TrackTableMetrics(XmSqlTableInfo table)
        {
            if (table == null || _metrics == null) return;

            if (_metrics.IsCacheHit) table.CacheHits++;
            else table.CacheMisses++;

            if (_metrics.EstimatedRows.HasValue && _metrics.EstimatedRows.Value > 0)
            {
                table.TotalEstimatedRows += _metrics.EstimatedRows.Value;
                if (_metrics.EstimatedRows.Value > table.MaxEstimatedRows)
                    table.MaxEstimatedRows = _metrics.EstimatedRows.Value;
            }

            if (_metrics.DurationMs.HasValue && _metrics.DurationMs.Value > 0)
            {
                table.TotalDurationMs += _metrics.DurationMs.Value;
                if (_metrics.DurationMs.Value > table.MaxDurationMs)
                    table.MaxDurationMs = _metrics.DurationMs.Value;
                table.TotalDirectQueryDurationMs += _metrics.DurationMs.Value;
            }

            if (_metrics.CpuTimeMs.HasValue && _metrics.CpuTimeMs.Value > 0)
                table.TotalCpuTimeMs += _metrics.CpuTimeMs.Value;

            if (_metrics.QueryId > 0)
                table.QueryIds.Add(_metrics.QueryId);
        }

        private string ResolveAlias(string alias)
        {
            if (alias == null) return null;
            if (_aliasToTable.TryGetValue(alias, out var tableName))
                return tableName;
            return null;
        }

        private string ResolveColumnName(string alias, string columnName)
        {
            if (alias != null && _aliasColumnMap.TryGetValue(alias, out var colMap))
            {
                if (colMap.TryGetValue(columnName, out var srcCol))
                    return srcCol;
            }
            return columnName;
        }

        /// <summary>Resolves [alias].[column] to (physicalTable, physicalColumn).</summary>
        private (string Table, string Column)? ResolveQualifiedColumn(string alias, string column)
        {
            var table = ResolveAlias(alias);
            if (table == null) return null;
            var physCol = ResolveColumnName(alias, column);
            return (table, physCol);
        }

        private void AddColumnUsage(string alias, string column, XmSqlColumnUsage usage,
            string aggregationType = null, string filterValue = null, string filterOp = null)
        {
            var resolved = ResolveQualifiedColumn(alias, column);
            if (resolved == null) return;

            var table = _analysis.GetOrAddTable(resolved.Value.Table);
            if (table == null) return;
            var col = table.GetOrAddColumn(resolved.Value.Column);
            if (col == null) return;

            col.AddUsage(usage);

            if (!string.IsNullOrEmpty(aggregationType))
                col.AddAggregation(aggregationType);

            if (!string.IsNullOrEmpty(filterValue))
                col.AddFilterValue(filterValue, filterOp ?? "=");
        }

        /// <summary>
        /// Extracts [alias].[column] from an expression (a qualified column reference, or an expression with a
        /// single child expression around one).
        /// </summary>
        private static (string Alias, string Column)? ExtractQualifiedColumn(DqExpression expr)
        {
            if (expr == null) return null;

            if (expr.Kind == DqExpressionKind.Unary)
            {
                var unary = expr.Unary;
                if (unary.Kind == DqUnaryKind.QualifiedColumnRef)
                    return (GetBracketedName(unary.Name), GetBracketedName(unary.Column));
            }

            // Parenthesized expression: (expr), and like the ANTLR visitor NOT expr and expr IS NULL
            if (expr.ChildCount == 1)
                return ExtractQualifiedColumn(expr.Left);

            return null;
        }

        /// <summary>Strips surrounding single quotes from a string literal.</summary>
        private static string StripQuotes(string s)
        {
            if (s == null) return null;
            if (s.Length >= 2 && s.StartsWith("'") && s.EndsWith("'"))
                return s.Substring(1, s.Length - 2).Replace("''", "'");
            if (s.Length >= 3 && s.StartsWith("N'", StringComparison.OrdinalIgnoreCase) && s.EndsWith("'"))
                return s.Substring(2, s.Length - 3).Replace("''", "'");
            return s;
        }

        // ==================== PASS 1: TABLE & ALIAS REGISTRATION ====================

        private void VisitSubselectBlock(DqSubselectBlock block)
        {
            // Extract table from: (select [$Table].[col] from [schema].[table] as [$Table]) AS [alias]
            var schemaTable = block.SchemaTable;
            if (schemaTable != null)
            {
                var tableName = GetBracketedName(schemaTable.Table);
                var alias = GetBracketedName(block.Alias);

                if (tableName != null)
                {
                    var table = _analysis.GetOrAddTable(tableName);
                    if (table != null)
                    {
                        table.IsFromTable = true;
                        table.HitCount++;
                        table.QueryCount++;
                        TrackTableMetrics(table);
                    }

                    if (alias != null)
                        _aliasToTable[alias] = tableName;

                    // Parse subselect columns for alias → column mapping
                    var columns = block.Columns;
                    if (columns != null && alias != null)
                    {
                        var colMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var col in columns)
                        {
                            var srcCol = GetBracketedName(col.Source);
                            var aliasCol = GetBracketedName(col.Alias);
                            if (srcCol != null && aliasCol != null)
                                colMap[aliasCol] = srcCol;
                        }
                        _aliasColumnMap[alias] = colMap;
                    }
                }
            }

            // Fallback: direct table reference within subselect block
            if (block.TableReference != null)
                VisitTableReference(block.TableReference);
        }

        private void VisitTableReference(DqTableReference reference)
        {
            var schemaTable = reference.SchemaTable;
            if (schemaTable == null) return;

            var tableName = GetBracketedName(schemaTable.Table);
            var alias = GetBracketedName(reference.Alias);

            if (tableName != null)
            {
                var table = _analysis.GetOrAddTable(tableName);
                if (table != null)
                {
                    table.IsFromTable = true;
                    table.HitCount++;
                    table.QueryCount++;
                    TrackTableMetrics(table);
                }

                if (alias != null)
                    _aliasToTable[alias] = tableName;
            }
        }

        // ==================== PASS 2: JOIN / RELATIONSHIP EXTRACTION ====================

        private void VisitJoinClause(DqJoinClause join)
        {
            // Visit the join source first to register aliases
            if (join.Source != null)
                VisitSubselectBlock(join.Source);

            // Process ON clause for relationships and Join usage
            if (join.On == null) return;

            foreach (var condition in join.On)
            {
                var leftCol = ExtractQualifiedColumn(condition.Left);
                var rightCol = ExtractQualifiedColumn(condition.Right);
                if (leftCol == null || rightCol == null) continue;

                var leftTable = ResolveAlias(leftCol.Value.Alias);
                var rightTable = ResolveAlias(rightCol.Value.Alias);
                var leftColName = ResolveColumnName(leftCol.Value.Alias, leftCol.Value.Column);
                var rightColName = ResolveColumnName(rightCol.Value.Alias, rightCol.Value.Column);

                if (leftTable == null || rightTable == null) continue;

                // Mark columns as Join usage
                AddColumnUsage(leftCol.Value.Alias, leftCol.Value.Column, XmSqlColumnUsage.Join);
                AddColumnUsage(rightCol.Value.Alias, rightCol.Value.Column, XmSqlColumnUsage.Join);

                // Add relationship if not duplicate
                var existing = _analysis.Relationships.FirstOrDefault(r =>
                    (r.FromTable.Equals(leftTable, StringComparison.OrdinalIgnoreCase) &&
                     r.FromColumn.Equals(leftColName, StringComparison.OrdinalIgnoreCase) &&
                     r.ToTable.Equals(rightTable, StringComparison.OrdinalIgnoreCase) &&
                     r.ToColumn.Equals(rightColName, StringComparison.OrdinalIgnoreCase)) ||
                    (r.FromTable.Equals(rightTable, StringComparison.OrdinalIgnoreCase) &&
                     r.FromColumn.Equals(rightColName, StringComparison.OrdinalIgnoreCase) &&
                     r.ToTable.Equals(leftTable, StringComparison.OrdinalIgnoreCase) &&
                     r.ToColumn.Equals(leftColName, StringComparison.OrdinalIgnoreCase)));

                if (existing == null)
                {
                    _analysis.Relationships.Add(new XmSqlRelationship
                    {
                        FromTable = leftTable,
                        FromColumn = leftColName,
                        ToTable = rightTable,
                        ToColumn = rightColName,
                        JoinType = XmSqlJoinType.InnerJoin
                    });
                }
            }
        }

        // ==================== PASS 3: SELECT LIST COLUMNS ====================

        public void VisitSelectStatement(DqSelectStatement statement)
        {
            // Visit FROM first to register aliases (subselects must be processed before SELECT)
            if (statement.From != null)
            {
                foreach (var source in statement.From)
                    VisitSubselectBlock(source);
            }

            // Visit JOINs to register their aliases and relationships
            if (statement.Joins != null)
            {
                foreach (var join in statement.Joins)
                    VisitJoinClause(join);
            }

            // Now process SELECT list (aliases are resolved)
            if (statement.SelectList != null)
                ProcessSelectList(statement.SelectList);

            // Process WHERE clause
            if (statement.Where != null)
                ExtractFiltersFromExpression(statement.Where);

            // Process GROUP BY clause
            if (statement.GroupBy != null)
                ProcessGroupByClause(statement.GroupBy);
        }

        private void ProcessSelectList(IReadOnlyList<DqSelectItem> selectList)
        {
            foreach (var item in selectList)
            {
                var expr = item.Expression;
                if (expr == null) continue;

                // Check if this is an aggregate function call
                if (TryExtractAggregate(expr))
                    continue;

                // Plain column reference: [alias].[col] AS [outputAlias]
                var qualCol = ExtractQualifiedColumn(expr);
                if (qualCol != null)
                    AddColumnUsage(qualCol.Value.Alias, qualCol.Value.Column, XmSqlColumnUsage.Select);
            }
        }

        /// <summary>
        /// Checks if an expression is an aggregate function call (SUM, COUNT, COUNT_BIG, etc.)
        /// and if so, records the aggregation. Returns true if it was an aggregate.
        /// </summary>
        private bool TryExtractAggregate(DqExpression expr)
        {
            if (expr.Kind != DqExpressionKind.Unary || expr.Unary.Kind != DqUnaryKind.FunctionCall) return false;

            var funcCall = expr.Unary.Function;
            var func = funcCall.Name.GetText().ToUpperInvariant();

            // Only process known aggregate functions
            if (!AggregateFunctions.Contains(func))
                return false;

            var aggType = funcCall.IsDistinct ? "DISTINCT" : func;

            // Try to find the column inside the function arguments
            if (funcCall.Arguments != null)
            {
                foreach (var argExpr in funcCall.Arguments)
                {
                    var qualCol = ExtractQualifiedColumn(argExpr);
                    if (qualCol != null)
                    {
                        AddColumnUsage(qualCol.Value.Alias, qualCol.Value.Column, XmSqlColumnUsage.Aggregate, aggregationType: aggType);
                        return true;
                    }
                }
            }

            // COUNT(*) with no specific column — still counted as aggregate
            return true;
        }

        // ==================== PASS 4: WHERE CLAUSE FILTERS ====================

        private void ExtractFiltersFromExpression(DqExpression expr)
        {
            if (expr == null) return;

            switch (expr.Kind)
            {
                case DqExpressionKind.IsNull:
                {
                    // IS [NOT] NULL: expression IS NOT? NULL
                    var qualCol = ExtractQualifiedColumn(expr.Left);
                    if (qualCol != null)
                    {
                        var op = expr.IsNot ? "IS NOT NULL" : "IS NULL";
                        AddColumnUsage(qualCol.Value.Alias, qualCol.Value.Column, XmSqlColumnUsage.Filter, filterOp: op);
                    }
                    return;
                }

                case DqExpressionKind.Binary:
                    if (expr.Operator == DqOperatorKind.And || expr.Operator == DqOperatorKind.Or)
                    {
                        // AND / OR: recurse into both sides
                        ExtractFiltersFromExpression(expr.Left);
                        ExtractFiltersFromExpression(expr.Right);
                    }
                    else if (expr.Operator == DqOperatorKind.Comparison)
                    {
                        // Comparison: expr comparisonOp expr
                        var qualCol = ExtractQualifiedColumn(expr.Left);
                        if (qualCol != null)
                        {
                            var op = expr.OperatorText.GetText();
                            var value = ExtractLiteralValue(expr.Right);
                            AddColumnUsage(qualCol.Value.Alias, qualCol.Value.Column, XmSqlColumnUsage.Filter,
                                filterValue: value, filterOp: op);
                        }
                    }
                    return;

                case DqExpressionKind.Not:
                case DqExpressionKind.Parenthesized:
                    // NOT expression and parenthesized expression: recurse
                    ExtractFiltersFromExpression(expr.Left);
                    return;
            }
        }

        /// <summary>Extracts a literal value from an expression (string, number, or null).</summary>
        private static string ExtractLiteralValue(DqExpression expr)
        {
            if (expr == null || expr.Kind != DqExpressionKind.Unary) return null;
            var unary = expr.Unary;

            if (unary.Kind == DqUnaryKind.Literal)
            {
                switch (unary.LiteralKind)
                {
                    case DqLiteralKind.String: return StripQuotes(unary.Literal.GetText());
                    case DqLiteralKind.Number: return unary.Literal.GetText();
                    default: return "NULL";
                }
            }

            // CAST(...) or function calls — return the raw text
            if (unary.Kind == DqUnaryKind.FunctionCall)
                return DirectQuerySqlLexer.TokenText(unary.Function.Name.Buffer, unary.Function.Start, unary.Function.End);

            return null;
        }

        // ==================== PASS 5: GROUP BY ====================

        private void ProcessGroupByClause(IReadOnlyList<DqExpression> groupBy)
        {
            foreach (var expr in groupBy)
            {
                var qualCol = ExtractQualifiedColumn(expr);
                if (qualCol != null)
                    AddColumnUsage(qualCol.Value.Alias, qualCol.Value.Column, XmSqlColumnUsage.GroupBy);
            }
        }
    }
}
