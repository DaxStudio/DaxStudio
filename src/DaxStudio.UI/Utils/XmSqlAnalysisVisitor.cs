using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using DaxStudio.UI.Grammars.Generated;
using DaxStudio.UI.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DaxStudio.UI.Utils
{
    /// <summary>
    /// ANTLR visitor that walks the xmSQL parse tree and populates an XmSqlAnalysis.
    /// Handles lineage tracking, temp table resolution, and all xmSQL constructs.
    /// </summary>
    internal class XmSqlAnalysisVisitor : xmSQLBaseVisitor<object>
    {
        private readonly XmSqlAnalysis _analysis;
        private readonly XmSqlParser.SeEventMetrics _metrics;

        // Lineage tracking
        private readonly Dictionary<string, TempTableLineage> _tempTableLineage
            = new Dictionary<string, TempTableLineage>(StringComparer.OrdinalIgnoreCase);


        public XmSqlAnalysisVisitor(XmSqlAnalysis analysis, XmSqlParser.SeEventMetrics metrics)
        {
            _analysis = analysis;
            _metrics = metrics;
        }

        // ==================== HELPERS ====================

        /// <summary>Extracts table name from a QUOTED_TABLE_NAME token ('TableName' -> TableName)</summary>
        private static string GetTableName(ITerminalNode node)
        {
            if (node == null) return null;
            var text = node.GetText();
            return text.Length >= 2 ? text.Substring(1, text.Length - 2) : text;
        }

        /// <summary>Extracts table name from a tableRef context (handles both 'Table' and [Table])</summary>
        private static string GetTableName(xmSQLParser.TableRefContext ctx)
        {
            if (ctx == null) return null;
            var quoted = ctx.QUOTED_TABLE_NAME();
            if (quoted != null) return GetTableName(quoted);
            var bracketed = ctx.BRACKETED_NAME();
            return bracketed != null ? GetBracketedContent(bracketed) : null;
        }

        /// <summary>Extracts column name from a BRACKETED_NAME token ([ColumnName] -> ColumnName)</summary>
        private static string GetBracketedContent(ITerminalNode node)
        {
            if (node == null) return null;
            var text = node.GetText();
            return text.Length >= 2 ? text.Substring(1, text.Length - 2) : text;
        }

        /// <summary>Gets table and column from a tableColumnRef context.
        /// Handles both 'Table'[Column] and [Table].[Column] forms.</summary>
        private (string Table, string Column)? GetTableColumn(xmSQLParser.TableColumnRefContext ctx)
        {
            if (ctx == null) return null;
            var table = GetTableName(ctx.tableRef());
            // Column is the BRACKETED_NAME that's a direct child of tableColumnRef
            var column = GetBracketedContent(ctx.BRACKETED_NAME());
            if (string.IsNullOrEmpty(table) || string.IsNullOrEmpty(column)) return null;
            return (table, column);
        }

        /// <summary>
        /// Walks an expression tree to find the first tableColumnRef (for aggregation expressions
        /// that may wrap a column reference in function calls like callbacks).
        /// </summary>
        private xmSQLParser.TableColumnRefContext FindTableColumnRef(xmSQLParser.ExpressionContext expr)
        {
            if (expr == null) return null;
            foreach (var atom in expr.expressionAtom())
            {
                var tcRef = atom.tableColumnRef();
                if (tcRef != null) return tcRef;

                var funcCall = atom.functionCall();
                if (funcCall != null)
                {
                    var exprList = funcCall.expressionList();
                    if (exprList != null)
                    {
                        foreach (var innerExpr in exprList.expression())
                        {
                            var found = FindTableColumnRef(innerExpr);
                            if (found != null) return found;
                        }
                    }
                }

                var parenExpr = atom.expression();
                if (parenExpr != null)
                {
                    var found = FindTableColumnRef(parenExpr);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private static bool IsTempTable(string tableName)
        {
            return tableName != null && tableName.StartsWith("$T", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Resolves a table/column reference through temp table lineage to physical tables.</summary>
        private (string Table, string Column)? ResolveToPhysical(string tableName, string columnName)
        {
            // Check for $ column naming convention (e.g., "Calendar$Date")
            var dollarIdx = columnName.IndexOf('$');
            if (dollarIdx > 0)
            {
                var sourceTable = columnName.Substring(0, dollarIdx);
                var sourceColumn = columnName.Substring(dollarIdx + 1);
                return (sourceTable, sourceColumn);
            }

            if (IsTempTable(tableName) && _tempTableLineage.TryGetValue(tableName, out var lineage))
            {
                if (lineage.ColumnMappings.TryGetValue(columnName, out var colLineage))
                    return (colLineage.SourceTable, colLineage.SourceColumn);

                if (lineage.SourcePhysicalTables.Count == 1)
                    return (lineage.SourcePhysicalTables.First(), columnName);
            }

            if (!IsTempTable(tableName))
                return (tableName, columnName);

            return null;
        }

        /// <summary>Adds a table/column, resolving temp tables, with the given usage.</summary>
        private void AddColumnUsage(string tableName, string columnName, XmSqlColumnUsage usage)
        {
            var resolved = ResolveToPhysical(tableName, columnName);
            if (resolved == null || IsTempTable(resolved.Value.Table)) return;

            var table = _analysis.GetOrAddTable(resolved.Value.Table);
            var column = table?.GetOrAddColumn(resolved.Value.Column);
            column?.AddUsage(usage);
        }

        private void TrackTableMetrics(XmSqlTableInfo table)
        {
            if (table == null || _metrics == null) return;

            if (_metrics.IsCacheHit)
                table.CacheHits++;
            else
                table.CacheMisses++;

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
            }

            if (_metrics.CpuTimeMs.HasValue && _metrics.CpuTimeMs.Value > 0)
                table.TotalCpuTimeMs += _metrics.CpuTimeMs.Value;

            if (_metrics.NetParallelDurationMs.HasValue && _metrics.NetParallelDurationMs.Value > 0)
                table.TotalParallelDurationMs += _metrics.NetParallelDurationMs.Value;

            if (_metrics.CpuFactor.HasValue && _metrics.CpuFactor.Value > 1.0)
            {
                table.ParallelQueryCount++;
                if (_metrics.CpuFactor.Value > table.MaxCpuFactor)
                    table.MaxCpuFactor = _metrics.CpuFactor.Value;
            }

            if (_metrics.QueryId > 0)
                table.QueryIds.Add(_metrics.QueryId);
        }

        // ==================== FIRST PASS: Build Lineage ====================

        /// <summary>Builds temp table lineage before the main visit.</summary>
        public void BuildLineage(xmSQLParser.QueryContext tree)
        {
            foreach (var stmt in tree.statement())
            {
                var define = stmt.defineTableStatement();
                if (define != null) ParseDefineTableLineage(define);

                var reduced = stmt.reducedByStatement();
                if (reduced != null) ParseReducedByLineage(reduced);
            }
            ResolveTransitiveLineage();
        }

        private void ParseDefineTableLineage(xmSQLParser.DefineTableStatementContext ctx)
        {
            var tempName = GetTableName(ctx.tableRef());
            if (tempName == null) return;
            ParseSelectBodyLineage(tempName, ctx.selectBody());
        }

        private void ParseReducedByLineage(xmSQLParser.ReducedByStatementContext ctx)
        {
            var tempName = GetTableName(ctx.tableRef());
            if (tempName == null) return;
            ParseSelectBodyLineage(tempName, ctx.selectBody());
        }

        private void ParseSelectBodyLineage(string tempName, xmSQLParser.SelectBodyContext body)
        {
            if (_tempTableLineage.ContainsKey(tempName) || body == null) return;

            var lineage = new TempTableLineage { TempTableName = tempName };

            // FROM clause
            var fromTable = GetTableName(body.fromClause()?.tableRef());
            if (fromTable != null)
            {
                if (IsTempTable(fromTable))
                    lineage.SourceTempTables.Add(fromTable);
                else
                    lineage.SourcePhysicalTables.Add(fromTable);
            }

            // JOIN clauses
            foreach (var join in body.joinClause())
            {
                var joinTable = GetTableName(join.tableRef());
                if (joinTable != null)
                {
                    if (IsTempTable(joinTable))
                        lineage.SourceTempTables.Add(joinTable);
                    else
                        lineage.SourcePhysicalTables.Add(joinTable);
                }
            }

            // Column mappings from SELECT clause
            foreach (var item in body.selectList()?.selectItem() ?? Array.Empty<xmSQLParser.SelectItemContext>())
            {
                var tcRef = item.tableColumnRef();
                if (tcRef == null) continue;
                var tc = GetTableColumn(tcRef);
                if (tc == null) continue;

                var col = tc.Value.Column;
                var dollarIdx = col.IndexOf('$');
                if (dollarIdx > 0)
                {
                    var srcTable = col.Substring(0, dollarIdx);
                    var srcCol = col.Substring(dollarIdx + 1);
                    lineage.ColumnMappings[col] = new ColumnLineageEntry
                    {
                        TempColumnName = col,
                        SourceTable = srcTable,
                        SourceColumn = srcCol
                    };
                    if (!IsTempTable(srcTable))
                        lineage.SourcePhysicalTables.Add(srcTable);
                }
                else if (!IsTempTable(tc.Value.Table))
                {
                    lineage.SourcePhysicalTables.Add(tc.Value.Table);
                    lineage.ColumnMappings[col] = new ColumnLineageEntry
                    {
                        TempColumnName = col,
                        SourceTable = tc.Value.Table,
                        SourceColumn = col
                    };
                }
            }

            _tempTableLineage[tempName] = lineage;
        }

        private void ResolveTransitiveLineage()
        {
            bool changed;
            int iteration = 0;
            do
            {
                changed = false;
                iteration++;
                foreach (var lineage in _tempTableLineage.Values)
                {
                    foreach (var srcTemp in lineage.SourceTempTables.ToList())
                    {
                        if (_tempTableLineage.TryGetValue(srcTemp, out var srcLineage))
                        {
                            foreach (var phys in srcLineage.SourcePhysicalTables)
                            {
                                if (lineage.SourcePhysicalTables.Add(phys))
                                    changed = true;
                            }
                        }
                    }
                }
            } while (changed && iteration < 10);
        }

        // ==================== MAIN VISITOR METHODS ====================

        public override object VisitSelectQueryStatement(xmSQLParser.SelectQueryStatementContext context)
        {
            // Visit WITH clause first
            var withClause = context.withClause();
            if (withClause != null)
            {
                VisitWithClause(withClause);
            }

            // Visit the select body
            VisitSelectBody(context.selectBody());
            return null;
        }

        public override object VisitDefineTableStatement(xmSQLParser.DefineTableStatementContext context)
        {
            var _ = GetTableName(context.tableRef());
            VisitSelectBody(context.selectBody());

            return null;
        }

        public override object VisitReducedByStatement(xmSQLParser.ReducedByStatementContext context)
        {
            var _ = GetTableName(context.tableRef());
            VisitSelectBody(context.selectBody());
            return null;
        }

        public override object VisitWithClause(xmSQLParser.WithClauseContext context)
        {
            // Find all table[column] references in WITH expressions and mark as Expression usage
            foreach (var expr in context.exprDefinition())
            {
                VisitExpressionForTableColumns(expr.expression(), XmSqlColumnUsage.Expression);
            }
            return null;
        }

        public override object VisitSelectBody(xmSQLParser.SelectBodyContext context)
        {
            if (context == null) return null;

            // FROM clause
            var fromCtx = context.fromClause();
            if (fromCtx != null)
            {
                var fromTable = GetTableName(fromCtx.tableRef());
                if (fromTable != null && !IsTempTable(fromTable))
                {
                    var table = _analysis.GetOrAddTable(fromTable);
                    if (table != null)
                    {
                        table.IsFromTable = true;
                        table.HitCount++;
                        table.QueryCount++;
                        TrackTableMetrics(table);
                    }
                }
            }

            // SELECT clause
            var selectList = context.selectList();
            if (selectList != null)
            {
                foreach (var item in selectList.selectItem())
                {
                    VisitSelectItem(item);
                }
            }


            // JOIN clauses
            foreach (var joinCtx in context.joinClause())
            {
                VisitJoinClause(joinCtx);
            }

            // WHERE clause
            var whereCtx = context.whereClause();
            if (whereCtx != null)
            {
                VisitWhereClause(whereCtx);
            }

            return null;
        }

        public override object VisitSelectItem(xmSQLParser.SelectItemContext context)
        {
            // Handle aggregation expressions
            var aggExpr = context.aggregationExpr();
            if (aggExpr != null)
            {
                VisitAggregationExpr(aggExpr);
                return null;
            }

            // Handle callback expressions
            var cbExpr = context.callbackExpr();
            if (cbExpr != null)
            {
                VisitCallbackExpr(cbExpr);
                return null;
            }

            // Handle table[column] references in SELECT
            var tcRef = context.tableColumnRef();
            if (tcRef != null)
            {
                var tc = GetTableColumn(tcRef);
                if (tc != null)
                {
                    AddColumnUsage(tc.Value.Table, tc.Value.Column, XmSqlColumnUsage.Select);

                    // Check for CALLBACKDATAID suffix
                    if (context.CALLBACKDATAID() != null)
                    {
                        MarkCallback(tc.Value.Table, tc.Value.Column, "CallbackDataID");
                    }
                }
            }

            return null;
        }

        public override object VisitAggregationExpr(xmSQLParser.AggregationExprContext context)
        {
            var aggFunc = context.aggFunction().GetText().ToUpperInvariant();
            var tcRef = FindTableColumnRef(context.expression());
            if (tcRef != null)
            {
                var tc = GetTableColumn(tcRef);
                if (tc != null)
                {
                    var resolved = ResolveToPhysical(tc.Value.Table, tc.Value.Column);
                    if (resolved != null && !IsTempTable(resolved.Value.Table))
                    {
                        var table = _analysis.GetOrAddTable(resolved.Value.Table);
                        var column = table?.GetOrAddColumn(resolved.Value.Column);
                        column?.AddAggregation(aggFunc);
                    }
                }
            }
            return null;
        }

        public override object VisitCallbackExpr(xmSQLParser.CallbackExprContext context)
        {
            var tcRef = context.tableColumnRef();
            if (tcRef != null)
            {
                var tc = GetTableColumn(tcRef);
                if (tc != null)
                {
                    string callbackType = "Callback";
                    if (context.ENCODECALLBACK() != null) callbackType = "EncodeCallback";
                    else if (context.CALLBACKDATAID() != null) callbackType = "CallbackDataID";

                    MarkCallback(tc.Value.Table, tc.Value.Column, callbackType);
                }
            }
            return null;
        }

        public override object VisitJoinClause(xmSQLParser.JoinClauseContext context)
        {
            // Handle REVERSE BITMAP JOIN
            var rbj = context.reverseBitmapJoin();
            if (rbj != null)
            {
                return VisitReverseBitmapJoin(rbj);
            }

            // Determine join type
            var joinTypeCtx = context.joinType();
            var joinType = XmSqlJoinType.Unknown;
            if (joinTypeCtx != null)
            {
                if (joinTypeCtx.LEFT() != null)
                    joinType = XmSqlJoinType.LeftOuterJoin;
                else if (joinTypeCtx.INNER() != null)
                    joinType = XmSqlJoinType.InnerJoin;
            }

            // Mark the joined table
            var tableRef = context.tableRef();
            if (tableRef != null)
            {
                var tableName = GetTableName(tableRef);
                if (tableName != null && !IsTempTable(tableName))
                {
                    var table = _analysis.GetOrAddTable(tableName);
                    if (table != null)
                    {
                        table.IsJoinedTable = true;
                        table.HitCount++;
                        if (_metrics?.QueryId > 0)
                            table.QueryIds.Add(_metrics.QueryId);
                    }
                }
            }

            // Handle table column ref in JOIN (e.g., LEFT OUTER JOIN 'Geography'[GeographyKey])
            var tcRef = context.tableColumnRef();
            if (tcRef != null)
            {
                var tc = GetTableColumn(tcRef);
                if (tc != null && !IsTempTable(tc.Value.Table))
                {
                    var table = _analysis.GetOrAddTable(tc.Value.Table);
                    if (table != null)
                    {
                        table.IsJoinedTable = true;
                        table.HitCount++;
                        if (_metrics?.QueryId > 0)
                            table.QueryIds.Add(_metrics.QueryId);
                    }
                }
            }

            // ON clause -> relationship
            var onCtx = context.onClause();
            if (onCtx != null)
            {
                var refs = onCtx.tableColumnRef();
                if (refs.Length == 2)
                {
                    var from = GetTableColumn(refs[0]);
                    var to = GetTableColumn(refs[1]);
                    if (from != null && to != null)
                    {
                        var resolvedFrom = ResolveToPhysical(from.Value.Table, from.Value.Column);
                        var resolvedTo = ResolveToPhysical(to.Value.Table, to.Value.Column);

                        if (resolvedFrom != null && resolvedTo != null &&
                            !IsTempTable(resolvedFrom.Value.Table) && !IsTempTable(resolvedTo.Value.Table))
                        {
                            _analysis.AddRelationship(
                                resolvedFrom.Value.Table, resolvedFrom.Value.Column,
                                resolvedTo.Value.Table, resolvedTo.Value.Column,
                                joinType);

                            AddColumnUsage(resolvedFrom.Value.Table, resolvedFrom.Value.Column, XmSqlColumnUsage.Join);
                            AddColumnUsage(resolvedTo.Value.Table, resolvedTo.Value.Column, XmSqlColumnUsage.Join);
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Handles REVERSE BITMAP JOIN: extracts the joined table and relationship from ON clause.
        /// Grammar: REVERSE BITMAP JOIN tableRef ON tableColumnRef EQUALS tableColumnRef
        /// </summary>
        public override object VisitReverseBitmapJoin(xmSQLParser.ReverseBitmapJoinContext context)
        {
            // Mark the joined table
            var tableRef = context.tableRef();
            if (tableRef != null)
            {
                var tableName = GetTableName(tableRef);
                if (tableName != null && !IsTempTable(tableName))
                {
                    var table = _analysis.GetOrAddTable(tableName);
                    if (table != null)
                    {
                        table.IsJoinedTable = true;
                        table.HitCount++;
                        if (_metrics?.QueryId > 0)
                            table.QueryIds.Add(_metrics.QueryId);
                    }
                }
            }

            // ON clause columns -> relationship
            var refs = context.tableColumnRef();
            if (refs != null && refs.Length == 2)
            {
                var from = GetTableColumn(refs[0]);
                var to = GetTableColumn(refs[1]);
                if (from != null && to != null)
                {
                    var resolvedFrom = ResolveToPhysical(from.Value.Table, from.Value.Column);
                    var resolvedTo = ResolveToPhysical(to.Value.Table, to.Value.Column);

                    if (resolvedFrom != null && resolvedTo != null &&
                        !IsTempTable(resolvedFrom.Value.Table) && !IsTempTable(resolvedTo.Value.Table))
                    {
                        _analysis.AddRelationship(
                            resolvedFrom.Value.Table, resolvedFrom.Value.Column,
                            resolvedTo.Value.Table, resolvedTo.Value.Column,
                            XmSqlJoinType.InnerJoin);

                        AddColumnUsage(resolvedFrom.Value.Table, resolvedFrom.Value.Column, XmSqlColumnUsage.Join);
                        AddColumnUsage(resolvedTo.Value.Table, resolvedTo.Value.Column, XmSqlColumnUsage.Join);
                    }
                }
            }

            return null;
        }

        public override object VisitWhereClause(xmSQLParser.WhereClauseContext context)
        {
            foreach (var pred in context.filterPredicate())
            {
                VisitFilterPredicate(pred);
            }
            return null;
        }

        public override object VisitFilterPredicate(xmSQLParser.FilterPredicateContext context)
        {
            // Get the table column reference (if any)
            var tcRef = context.tableColumnRef();
            if (tcRef != null && tcRef.Length > 0)
            {
                var tc = GetTableColumn(tcRef[0]);
                if (tc != null)
                {
                    AddColumnUsage(tc.Value.Table, tc.Value.Column, XmSqlColumnUsage.Filter);

                    // Extract operator and values
                    var compOp = context.comparisonOp();
                    if (compOp != null && context.filterValue() != null && context.filterValue().Length > 0)
                    {
                        var op = compOp.GetText();
                        var value = StripQuotes(context.filterValue(0).GetText());
                        AddFilterValue(tc.Value.Table, tc.Value.Column, value, op);
                    }

                    // IN clause
                    if (context.IN() != null && context.valueList() != null)
                    {
                        var values = context.valueList().filterValue();
                        foreach (var v in values)
                        {
                            var value = StripQuotes(v.GetText());
                            AddFilterValue(tc.Value.Table, tc.Value.Column, value, "IN");
                        }
                    }

                    // NIN clause
                    if (context.NIN() != null)
                    {
                        AddColumnUsage(tc.Value.Table, tc.Value.Column, XmSqlColumnUsage.Filter);
                    }

                    // BETWEEN clause
                    if (context.BETWEEN() != null && context.filterValue() != null && context.filterValue().Length >= 2)
                    {
                        var val1 = StripQuotes(context.filterValue(0).GetText());
                        var val2 = StripQuotes(context.filterValue(1).GetText());
                        AddFilterValue(tc.Value.Table, tc.Value.Column, $"{val1} to {val2}", "BETWEEN");
                    }

                    // ININDEX
                    if (context.ININDEX() != null && tcRef.Length >= 2)
                    {
                        // The physical column is already marked as Filter above
                        // The second reference is the temp table
                    }
                }
            }

            // COALESCE filter
            var coalesceCtx = context.coalesceFilter();
            if (coalesceCtx != null)
            {
                VisitCoalesceFilter(coalesceCtx);
            }

            return null;
        }

        public override object VisitCoalesceFilter(xmSQLParser.CoalesceFilterContext context)
        {
            var tcRef = context.tableColumnRef();
            if (tcRef != null)
            {
                var tc = GetTableColumn(tcRef);
                if (tc != null)
                {
                    AddColumnUsage(tc.Value.Table, tc.Value.Column, XmSqlColumnUsage.Filter);

                    var compOp = context.comparisonOp();
                    var filterVal = context.filterValue();
                    if (compOp != null && filterVal != null)
                    {
                        var op = compOp.GetText();
                        var value = StripQuotes(filterVal.GetText());
                        AddFilterValue(tc.Value.Table, tc.Value.Column, value, op);
                    }
                }
            }
            return null;
        }

        public override object VisitCreateShallowRelationStatement(xmSQLParser.CreateShallowRelationStatementContext context)
        {
            var fromRef = context.tableColumnRef(0);
            var toRef = context.tableColumnRef(1);
            if (fromRef == null || toRef == null) return null;

            var from = GetTableColumn(fromRef);
            var to = GetTableColumn(toRef);
            if (from == null || to == null) return null;

            var resolvedFrom = ResolveToPhysical(from.Value.Table, from.Value.Column);
            var resolvedTo = ResolveToPhysical(to.Value.Table, to.Value.Column);
            if (resolvedFrom == null || resolvedTo == null) return null;
            if (IsTempTable(resolvedFrom.Value.Table) || IsTempTable(resolvedTo.Value.Table)) return null;

            // Check for modifiers
            bool isManyToMany = false;
            bool isBoth = false;
            foreach (var mod in context.relationModifier())
            {
                if (mod.MANYTOMANY() != null) isManyToMany = true;
                if (mod.BOTH() != null) isBoth = true;
            }

            // Check if relationship already exists
            var existing = _analysis.Relationships.FirstOrDefault(r =>
                (r.FromTable.Equals(resolvedFrom.Value.Table, StringComparison.OrdinalIgnoreCase) &&
                 r.FromColumn.Equals(resolvedFrom.Value.Column, StringComparison.OrdinalIgnoreCase) &&
                 r.ToTable.Equals(resolvedTo.Value.Table, StringComparison.OrdinalIgnoreCase) &&
                 r.ToColumn.Equals(resolvedTo.Value.Column, StringComparison.OrdinalIgnoreCase)) ||
                (r.FromTable.Equals(resolvedTo.Value.Table, StringComparison.OrdinalIgnoreCase) &&
                 r.FromColumn.Equals(resolvedTo.Value.Column, StringComparison.OrdinalIgnoreCase) &&
                 r.ToTable.Equals(resolvedFrom.Value.Table, StringComparison.OrdinalIgnoreCase) &&
                 r.ToColumn.Equals(resolvedFrom.Value.Column, StringComparison.OrdinalIgnoreCase)));

            if (existing != null)
            {
                existing.HitCount++;
                if (isManyToMany) existing.Cardinality = XmSqlCardinality.ManyToMany;
                if (isBoth) existing.CrossFilterDirection = XmSqlCrossFilterDirection.Both;
            }
            else
            {
                _analysis.Relationships.Add(new XmSqlRelationship
                {
                    FromTable = resolvedFrom.Value.Table,
                    FromColumn = resolvedFrom.Value.Column,
                    ToTable = resolvedTo.Value.Table,
                    ToColumn = resolvedTo.Value.Column,
                    JoinType = XmSqlJoinType.Unknown,
                    HitCount = 1,
                    Cardinality = isManyToMany ? XmSqlCardinality.ManyToMany : XmSqlCardinality.OneToMany,
                    CrossFilterDirection = isBoth ? XmSqlCrossFilterDirection.Both : XmSqlCrossFilterDirection.Single
                });
            }

            // Mark columns as join keys
            AddColumnUsage(resolvedFrom.Value.Table, resolvedFrom.Value.Column, XmSqlColumnUsage.Join);
            AddColumnUsage(resolvedTo.Value.Table, resolvedTo.Value.Column, XmSqlColumnUsage.Join);

            return null;
        }

        // ==================== HELPER METHODS ====================

        private void VisitExpressionForTableColumns(xmSQLParser.ExpressionContext ctx, XmSqlColumnUsage usage)
        {
            if (ctx == null) return;

            foreach (var atom in ctx.expressionAtom())
            {
                var tcRef = atom.tableColumnRef();
                if (tcRef != null)
                {
                    var tc = GetTableColumn(tcRef);
                    if (tc != null)
                    {
                        // Skip $Expr references
                        if (tc.Value.Column.StartsWith("$Expr", StringComparison.OrdinalIgnoreCase))
                            continue;
                        AddColumnUsage(tc.Value.Table, tc.Value.Column, usage);
                    }
                }

                // Recurse into nested expressions
                var nestedExpr = atom.expression();
                if (nestedExpr != null)
                    VisitExpressionForTableColumns(nestedExpr, usage);

                var funcCall = atom.functionCall();
                if (funcCall != null)
                {
                    var exprList = funcCall.expressionList();
                    if (exprList != null)
                    {
                        foreach (var expr in exprList.expression())
                            VisitExpressionForTableColumns(expr, usage);
                    }
                    var innerExpr = funcCall.expression();
                    if (innerExpr != null)
                        VisitExpressionForTableColumns(innerExpr, usage);
                }
            }
        }

        private void MarkCallback(string tableName, string columnName, string callbackType)
        {
            var resolved = ResolveToPhysical(tableName, columnName);
            if (resolved == null || IsTempTable(resolved.Value.Table)) return;

            var table = _analysis.GetOrAddTable(resolved.Value.Table);
            var column = table?.GetOrAddColumn(resolved.Value.Column);
            if (column != null)
            {
                column.HasCallback = true;
                if (string.IsNullOrEmpty(column.CallbackType))
                    column.CallbackType = callbackType;
            }
        }

        private void AddFilterValue(string tableName, string columnName, string value, string op)
        {
            var resolved = ResolveToPhysical(tableName, columnName);
            if (resolved == null || IsTempTable(resolved.Value.Table)) return;

            var table = _analysis.GetOrAddTable(resolved.Value.Table);
            var column = table?.GetOrAddColumn(resolved.Value.Column);
            column?.AddFilterValue(value, op);
        }

        private static string StripQuotes(string value)
        {
            if (value == null) return null;
            return value.Trim('\'', '"');
        }

        // ==================== LINEAGE DATA STRUCTURES ====================

        private class TempTableLineage
        {
            public string TempTableName { get; set; }
            public HashSet<string> SourcePhysicalTables { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> SourceTempTables { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, ColumnLineageEntry> ColumnMappings { get; } = new Dictionary<string, ColumnLineageEntry>(StringComparer.OrdinalIgnoreCase);
        }

        private class ColumnLineageEntry
        {
            public string TempColumnName { get; set; }
            public string SourceTable { get; set; }
            public string SourceColumn { get; set; }
        }
    }
}
