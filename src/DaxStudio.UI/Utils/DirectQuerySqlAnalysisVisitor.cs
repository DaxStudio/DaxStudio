using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using DaxStudio.UI.Grammars.Generated;
using DaxStudio.UI.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using DqParser = DaxStudio.UI.Grammars.Generated.DirectQuerySqlParser;

namespace DaxStudio.UI.Utils
{
    /// <summary>
    /// ANTLR visitor that walks the DirectQuery SQL parse tree and populates an XmSqlAnalysis.
    /// Handles alias resolution and column mapping for T-SQL DirectQuery statements.
    /// </summary>
    internal class DirectQuerySqlAnalysisVisitor : DirectQuerySqlBaseVisitor<object>
    {
        private readonly XmSqlAnalysis _analysis;
        private readonly XmSqlParser.SeEventMetrics _metrics;

        // Maps outer aliases (e.g. "t4") to physical table names (e.g. "Product")
        private readonly Dictionary<string, string> _aliasToTable
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Maps outer alias → { aliasColumn → sourceColumn }
        // e.g. "t4" → { "ProductKey" → "ProductKey", "ProductName" → "ProductName" }
        private readonly Dictionary<string, Dictionary<string, string>> _aliasColumnMap
            = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public DirectQuerySqlAnalysisVisitor(XmSqlAnalysis analysis, XmSqlParser.SeEventMetrics metrics)
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

        private static string GetBracketedName(DqParser.BracketedNameContext ctx)
        {
            return ctx == null ? null : StripBrackets(ctx.GetText());
        }

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

        /// <summary>
        /// Resolves [alias].[column] to (physicalTable, physicalColumn).
        /// </summary>
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
        /// Extracts [alias].[column] from an expression context (walks through unary → qualifiedColumnRef).
        /// </summary>
        private (string Alias, string Column)? ExtractQualifiedColumn(DqParser.ExpressionContext expr)
        {
            if (expr == null) return null;

            // Direct unary → qualifiedColumnRef
            var unary = expr.unaryExpression();
            if (unary != null)
            {
                var qualRef = unary.qualifiedColumnRef();
                if (qualRef != null)
                {
                    var parts = qualRef.bracketedName();
                    if (parts.Length == 2)
                        return (GetBracketedName(parts[0]), GetBracketedName(parts[1]));
                }
            }

            // Parenthesized expression: (expr)
            var innerExpr = expr.expression();
            if (innerExpr != null && innerExpr.Length == 1)
                return ExtractQualifiedColumn(innerExpr[0]);

            return null;
        }

        /// <summary>Strips surrounding single quotes from a string literal.</summary>
        private static string StripQuotes(string s)
        {
            if (s == null) return null;
            if (s.Length >= 2 && s.StartsWith("'") && s.EndsWith("'"))
                return s.Substring(1, s.Length - 2).Replace("''", "'");
            // N'...' prefix
            if (s.Length >= 3 && s.StartsWith("N'", StringComparison.OrdinalIgnoreCase) && s.EndsWith("'"))
                return s.Substring(2, s.Length - 3).Replace("''", "'");
            return s;
        }

        // ==================== PASS 1: TABLE & ALIAS REGISTRATION ====================

        public override object VisitSubselectBlock(DqParser.SubselectBlockContext context)
        {
            // Extract table from: (select [$Table].[col] from [schema].[table] as [$Table]) AS [alias]
            var schemaTable = context.schemaTable();
            if (schemaTable != null)
            {
                var bracketedNames = schemaTable.bracketedName();
                if (bracketedNames.Length == 2)
                {
                    var tableName = GetBracketedName(bracketedNames[1]);
                    var aliasBracket = context.bracketedName();
                    var alias = aliasBracket != null ? GetBracketedName(aliasBracket) : null;

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
                        var columns = context.subselectColumnList()?.subselectColumn();
                        if (columns != null && alias != null)
                        {
                            var colMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var col in columns)
                            {
                                var bracketNames = col.bracketedName();
                                if (bracketNames.Length >= 2)
                                {
                                    var srcCol = GetBracketedName(bracketNames[0]);
                                    var aliasCol = GetBracketedName(bracketNames[1]);
                                    if (srcCol != null && aliasCol != null)
                                        colMap[aliasCol] = srcCol;
                                }
                            }
                            _aliasColumnMap[alias] = colMap;
                        }
                    }
                }
            }

            // Fallback: direct table reference within subselect block
            var tableRef = context.tableReference();
            if (tableRef != null)
                VisitTableReference(tableRef);

            return null;
        }

        public override object VisitTableReference(DqParser.TableReferenceContext context)
        {
            var schemaTable = context.schemaTable();
            if (schemaTable != null)
            {
                var bracketedNames = schemaTable.bracketedName();
                if (bracketedNames.Length == 2)
                {
                    var tableName = GetBracketedName(bracketedNames[1]);
                    var aliasBracket = context.bracketedName();
                    var alias = aliasBracket != null ? GetBracketedName(aliasBracket) : null;

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
            }
            return null;
        }

        // ==================== PASS 2: JOIN / RELATIONSHIP EXTRACTION ====================

        public override object VisitJoinClause(DqParser.JoinClauseContext context)
        {
            // Visit the join source first to register aliases
            var joinSource = context.tableJoinSource();
            if (joinSource != null)
            {
                var subselectBlock = joinSource.subselectBlock();
                if (subselectBlock != null)
                    VisitSubselectBlock(subselectBlock);

                var tableRef = joinSource.tableReference();
                if (tableRef != null)
                    VisitTableReference(tableRef);
            }

            // Process ON clause for relationships and Join usage
            var onClause = context.onJoinClause();
            if (onClause != null)
            {
                foreach (var condition in onClause.joinCondition())
                {
                    var exprs = condition.expression();
                    if (exprs.Length == 2)
                    {
                        var leftCol = ExtractQualifiedColumn(exprs[0]);
                        var rightCol = ExtractQualifiedColumn(exprs[1]);

                        if (leftCol != null && rightCol != null)
                        {
                            var leftTable = ResolveAlias(leftCol.Value.Alias);
                            var rightTable = ResolveAlias(rightCol.Value.Alias);
                            var leftColName = ResolveColumnName(leftCol.Value.Alias, leftCol.Value.Column);
                            var rightColName = ResolveColumnName(rightCol.Value.Alias, rightCol.Value.Column);

                            if (leftTable != null && rightTable != null)
                            {
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
                    }
                }
            }

            return null;
        }

        // ==================== PASS 3: SELECT LIST COLUMNS ====================

        public override object VisitSelectStatement(DqParser.SelectStatementContext context)
        {
            // Visit FROM first to register aliases (subselects must be processed before SELECT)
            var fromClause = context.fromClause();
            if (fromClause != null)
                Visit(fromClause);

            // Visit JOINs to register their aliases and relationships
            foreach (var join in context.joinClause())
                VisitJoinClause(join);

            // Now process SELECT list (aliases are resolved)
            var selectList = context.selectList();
            if (selectList != null)
                ProcessSelectList(selectList);

            // Process WHERE clause
            var whereClause = context.whereClause();
            if (whereClause != null)
                ProcessWhereClause(whereClause);

            // Process GROUP BY clause
            var groupBy = context.groupByClause();
            if (groupBy != null)
                ProcessGroupByClause(groupBy);

            return null;
        }

        private void ProcessSelectList(DqParser.SelectListContext selectList)
        {
            foreach (var item in selectList.selectItem())
            {
                var expr = item.expression();
                if (expr == null) continue;

                // Check if this is an aggregate function call
                if (TryExtractAggregate(expr))
                    continue;

                // Plain column reference: [alias].[col] AS [outputAlias]
                var qualCol = ExtractQualifiedColumn(expr);
                if (qualCol != null)
                {
                    AddColumnUsage(qualCol.Value.Alias, qualCol.Value.Column, XmSqlColumnUsage.Select);
                }
            }
        }

        /// <summary>
        /// Checks if an expression is an aggregate function call (SUM, COUNT, COUNT_BIG, etc.)
        /// and if so, records the aggregation. Returns true if it was an aggregate.
        /// </summary>
        private bool TryExtractAggregate(DqParser.ExpressionContext expr)
        {
            var unary = expr.unaryExpression();
            if (unary == null) return false;

            var funcCall = unary.functionCall();
            if (funcCall == null) return false;

            var funcName = funcCall.functionName();
            if (funcName == null) return false;

            string func = funcName.GetText().ToUpperInvariant();

            // Only process known aggregate functions
            if (func != "SUM" && func != "COUNT" && func != "COUNT_BIG" && func != "AVG" &&
                func != "MIN" && func != "MAX" && func != "STDEV" && func != "STDEVP" &&
                func != "VAR" && func != "VARP" && func != "CHECKSUM_AGG")
                return false;

            bool isDistinct = funcCall.DISTINCT() != null;
            string aggType = isDistinct ? "DISTINCT" : func;

            // Try to find the column inside the function arguments
            var exprList = funcCall.expressionList();
            if (exprList != null)
            {
                foreach (var argExpr in exprList.expression())
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

        private void ProcessWhereClause(DqParser.WhereClauseContext whereClause)
        {
            var expr = whereClause.expression();
            if (expr != null)
                ExtractFiltersFromExpression(expr);
        }

        private void ExtractFiltersFromExpression(DqParser.ExpressionContext expr)
        {
            if (expr == null) return;

            var subExprs = expr.expression();

            // IS [NOT] NULL: expression IS NOT? NULL
            if (expr.IS() != null)
            {
                var qualCol = ExtractQualifiedColumn(subExprs.Length > 0 ? subExprs[0] : null);
                if (qualCol != null)
                {
                    string op = expr.NOT() != null ? "IS NOT NULL" : "IS NULL";
                    AddColumnUsage(qualCol.Value.Alias, qualCol.Value.Column, XmSqlColumnUsage.Filter, filterOp: op);
                }
                return;
            }

            // AND / OR: recurse into both sides
            if (expr.AND() != null || expr.OR() != null)
            {
                if (subExprs.Length >= 2)
                {
                    ExtractFiltersFromExpression(subExprs[0]);
                    ExtractFiltersFromExpression(subExprs[1]);
                }
                return;
            }

            // Comparison: expr comparisonOp expr
            var compOp = expr.comparisonOp();
            if (compOp != null && subExprs.Length == 2)
            {
                var qualCol = ExtractQualifiedColumn(subExprs[0]);
                if (qualCol != null)
                {
                    string op = compOp.GetText();
                    string value = ExtractLiteralValue(subExprs[1]);
                    AddColumnUsage(qualCol.Value.Alias, qualCol.Value.Column, XmSqlColumnUsage.Filter,
                        filterValue: value, filterOp: op);
                }
                return;
            }

            // NOT expression: recurse
            if (expr.NOT() != null && subExprs.Length == 1)
            {
                ExtractFiltersFromExpression(subExprs[0]);
                return;
            }

            // Parenthesized expression
            if (subExprs.Length == 1 && expr.unaryExpression() == null)
            {
                ExtractFiltersFromExpression(subExprs[0]);
                return;
            }
        }

        /// <summary>Extracts a literal value from an expression (string, number, or null).</summary>
        private static string ExtractLiteralValue(DqParser.ExpressionContext expr)
        {
            if (expr == null) return null;
            var unary = expr.unaryExpression();
            if (unary == null) return null;

            var literal = unary.literal();
            if (literal != null)
            {
                if (literal.STRING_LITERAL() != null)
                    return StripQuotes(literal.STRING_LITERAL().GetText());
                if (literal.NUMBER() != null)
                    return literal.NUMBER().GetText();
                if (literal.NULL_() != null)
                    return "NULL";
            }

            // CAST(...) or function calls — return the raw text
            var funcCall = unary.functionCall();
            if (funcCall != null)
                return funcCall.GetText();

            return null;
        }

        // ==================== PASS 5: GROUP BY ====================

        private void ProcessGroupByClause(DqParser.GroupByClauseContext groupBy)
        {
            var exprList = groupBy.expressionList();
            if (exprList == null) return;

            foreach (var expr in exprList.expression())
            {
                var qualCol = ExtractQualifiedColumn(expr);
                if (qualCol != null)
                {
                    AddColumnUsage(qualCol.Value.Alias, qualCol.Value.Column, XmSqlColumnUsage.GroupBy);
                }
            }
        }
    }
}
