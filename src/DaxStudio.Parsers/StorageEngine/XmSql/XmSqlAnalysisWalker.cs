using Parlot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DaxStudio.Parsers.StorageEngine.XmSql
{
    /// <summary>
    /// Walks an <see cref="XmSqlQuery"/> and populates an XmSqlAnalysis. This is the Parlot counterpart of
    /// <see cref="XmSqlAnalysisVisitor"/>: the methods mirror the visitor's and make the same mutations.
    /// Handles lineage tracking, temp table resolution, and all xmSQL constructs.
    /// </summary>
    internal sealed class XmSqlAnalysisWalker
    {
        private readonly XmSqlAnalysis _analysis;
        private readonly XmSqlParser.SeEventMetrics _metrics;

        // Lineage tracking
        private readonly Dictionary<string, TempTableLineage> _tempTableLineage
            = new Dictionary<string, TempTableLineage>(StringComparer.OrdinalIgnoreCase);


        public XmSqlAnalysisWalker(XmSqlAnalysis analysis, XmSqlParser.SeEventMetrics metrics)
        {
            _analysis = analysis;
            _metrics = metrics;
        }

        // Canonical set of callback function names recognized by the parser.
        // Must be kept in sync with the callback name lists in xmSQL.xshd and
        // TraceStorageEngineExtensions so the definition of a "callback" is consistent.
        private static readonly HashSet<string> CallbackFunctionNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CallbackDataID",
                "EncodeCallback",
                "LogAbsValueCallback",
                "RoundValueCallback",
                "MinMaxColumnPositionCallback",
                "Cond",
            };

        // ==================== HELPERS ====================

        /// <summary>Extracts the name inside a QUOTED_TABLE_NAME or BRACKETED_NAME token ('TableName' -> TableName, [ColumnName] -> ColumnName)</summary>
        private static string Unwrap(TextSpan span)
        {
            if (span.IsMissing()) return null;
            return span.Length >= 2 ? span.Buffer.Substring(span.Offset + 1, span.Length - 2) : span.ToString();
        }

        /// <summary>Extracts table name from a tableRef (handles both 'Table' and [Table])</summary>
        private static string GetTableName(TableRef ctx)
        {
            if (ctx == null) return null;
            return Unwrap(ctx.Text);
        }

        /// <summary>Gets table and column from a tableColumnRef.
        /// Handles both 'Table'[Column] and [Table].[Column] forms.</summary>
        private static (string Table, string Column)? GetTableColumn(TableColumnRef ctx)
        {
            if (ctx == null) return null;
            var table = GetTableName(ctx.Table);
            var column = Unwrap(ctx.Column);
            if (string.IsNullOrEmpty(table) || string.IsNullOrEmpty(column)) return null;
            return (table, column);
        }

        /// <summary>
        /// Walks an expression tree to find the first tableColumnRef (for aggregation expressions
        /// that may wrap a column reference in function calls like callbacks).
        /// </summary>
        private static TableColumnRef FindTableColumnRef(Expression expr)
        {
            if (expr == null) return null;
            foreach (var atom in expr.Atoms)
            {
                var tcRef = atom.Column;
                if (tcRef != null) return tcRef;

                var funcCall = atom.Function;
                // Like the visitor, only the expressionList of a call is searched, not the PFCAST argument
                if (funcCall != null && funcCall.NameKind != FunctionNameKind.PfCast && funcCall.Arguments != null)
                {
                    foreach (var innerExpr in funcCall.Arguments)
                    {
                        var found = FindTableColumnRef(innerExpr);
                        if (found != null) return found;
                    }
                }

                var parenExpr = atom.Parenthesized;
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

        /// <summary>Builds temp table lineage before the main walk.</summary>
        public void BuildLineage(XmSqlQuery tree)
        {
            foreach (var stmt in tree.Statements)
            {
                // DEFINE TABLE and REDUCED BY
                if (stmt is TempTableStatement temp) ParseTempTableLineage(temp);
            }
            ResolveTransitiveLineage();
        }

        private void ParseTempTableLineage(TempTableStatement ctx)
        {
            var tempName = GetTableName(ctx.Table);
            if (tempName == null) return;
            ParseSelectBodyLineage(tempName, ctx.Body);
        }

        private void ParseSelectBodyLineage(string tempName, SelectBody body)
        {
            if (_tempTableLineage.ContainsKey(tempName) || body == null) return;

            var lineage = new TempTableLineage { TempTableName = tempName };

            // FROM clause
            var fromTable = GetTableName(body.From);
            if (fromTable != null)
            {
                if (IsTempTable(fromTable))
                    lineage.SourceTempTables.Add(fromTable);
                else
                    lineage.SourcePhysicalTables.Add(fromTable);
            }

            // JOIN clauses (the visitor reads joinClause.tableRef(), which a REVERSE BITMAP JOIN doesn't have)
            foreach (var join in body.Joins)
            {
                if (join.Kind == JoinKind.ReverseBitmap) continue;
                var joinTable = GetTableName(join.Table);
                if (joinTable != null)
                {
                    if (IsTempTable(joinTable))
                        lineage.SourceTempTables.Add(joinTable);
                    else
                        lineage.SourcePhysicalTables.Add(joinTable);
                }
            }

            // Column mappings from SELECT clause
            foreach (var item in body.Items)
            {
                var tcRef = item.Column;
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

        // ==================== MAIN WALKER METHODS ====================

        /// <summary>Second pass: extracts the analysis data, statement by statement.</summary>
        public void Walk(XmSqlQuery tree)
        {
            foreach (var stmt in tree.Statements)
            {
                switch (stmt)
                {
                    case SelectQuery select: WalkSelectQueryStatement(select); break;
                    case TempTableStatement temp: WalkTempTableStatement(temp); break;
                    case CreateShallowRelation relation: WalkCreateShallowRelationStatement(relation); break;
                    // SET directives carry nothing to analyze
                }
            }
        }

        private void WalkSelectQueryStatement(SelectQuery context)
        {
            // Visit WITH clause first
            var withClause = context.With;
            if (withClause != null)
            {
                WalkWithClause(withClause);
            }

            // Visit the select body
            WalkSelectBody(context.Body);
        }

        private void WalkTempTableStatement(TempTableStatement context)
        {
            WalkSelectBody(context.Body);
        }

        private void WalkWithClause(IReadOnlyList<ExprDefinition> definitions)
        {
            // Find all table[column] references in WITH expressions and mark as Expression usage
            foreach (var expr in definitions)
            {
                WalkExpressionForTableColumns(expr.Expression, XmSqlColumnUsage.Expression);
                MarkCallbacksInExpression(expr.Expression);
            }
        }

        private void WalkSelectBody(SelectBody context)
        {
            if (context == null) return;

            // FROM clause
            if (context.From != null)
            {
                var fromTable = GetTableName(context.From);
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
            if (context.Items != null)
            {
                foreach (var item in context.Items)
                {
                    WalkSelectItem(item);
                }
            }


            // JOIN clauses
            foreach (var joinCtx in context.Joins)
            {
                WalkJoinClause(joinCtx);
            }

            // WHERE clause
            if (context.Where != null)
            {
                WalkWhereClause(context.Where);
            }
        }

        private void WalkSelectItem(SelectItem context)
        {
            // Handle aggregation expressions
            var aggExpr = context.Aggregation;
            if (aggExpr != null)
            {
                WalkAggregationExpr(aggExpr);
                MarkCallbacksInExpression(aggExpr.Expression);
                return;
            }

            // Handle callback expressions
            var cbExpr = context.Callback;
            if (cbExpr != null)
            {
                WalkCallbackExpr(cbExpr);
                return;
            }

            // Handle table[column] references in SELECT
            var tcRef = context.Column;
            if (tcRef != null)
            {
                var tc = GetTableColumn(tcRef);
                if (tc != null)
                {
                    AddColumnUsage(tc.Value.Table, tc.Value.Column, XmSqlColumnUsage.Select);

                    // Check for CALLBACKDATAID suffix
                    if (context.CallbackDataId.IsPresent())
                    {
                        MarkCallback(tc.Value.Table, tc.Value.Column, "CallbackDataID");
                    }
                }
            }

            // Handle catch-all expressions (e.g. callback functions emitted as function
            // calls such as 'LogAbsValueCallback'('Sales'[Amount]))
            var exprCtx = context.Expression;
            if (exprCtx != null)
            {
                MarkCallbacksInExpression(exprCtx);
            }
        }

        private void WalkAggregationExpr(Aggregation context)
        {
            var aggFunc = context.Function.ToString().ToUpperInvariant();
            var tcRef = FindTableColumnRef(context.Expression);
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
        }

        private void WalkCallbackExpr(CallbackExpr context)
        {
            var tcRef = context.Column;
            if (tcRef != null)
            {
                var tc = GetTableColumn(tcRef);
                if (tc != null)
                {
                    string callbackType = "Callback";
                    if (context.Kind == CallbackKind.EncodeCallback) callbackType = "EncodeCallback";
                    else if (context.Kind == CallbackKind.CallbackDataId) callbackType = "CallbackDataID";

                    MarkCallback(tc.Value.Table, tc.Value.Column, callbackType);
                }
            }
        }

        private void WalkJoinClause(JoinClause context)
        {
            // Handle REVERSE BITMAP JOIN
            if (context.Kind == JoinKind.ReverseBitmap)
            {
                WalkReverseBitmapJoin(context);
                return;
            }

            // Determine join type
            var joinType = context.Kind == JoinKind.LeftOuter ? XmSqlJoinType.LeftOuterJoin : XmSqlJoinType.InnerJoin;

            // Mark the joined table
            var tableRef = context.Table;
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
            var tcRef = context.TableColumn;
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
            if (context.OnLeft != null && context.OnRight != null)
            {
                AddJoinRelationship(context.OnLeft, context.OnRight, joinType);
            }
        }

        /// <summary>
        /// Handles REVERSE BITMAP JOIN: extracts the joined table and relationship from ON clause.
        /// Grammar: REVERSE BITMAP JOIN tableRef ON tableColumnRef EQUALS tableColumnRef
        /// </summary>
        private void WalkReverseBitmapJoin(JoinClause context)
        {
            // Mark the joined table
            var tableRef = context.Table;
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
            if (context.OnLeft != null && context.OnRight != null)
            {
                AddJoinRelationship(context.OnLeft, context.OnRight, XmSqlJoinType.InnerJoin);
            }
        }

        /// <summary>The ON clause handling shared by the visitor's VisitJoinClause and VisitReverseBitmapJoin.</summary>
        private void AddJoinRelationship(TableColumnRef left, TableColumnRef right, XmSqlJoinType joinType)
        {
            var from = GetTableColumn(left);
            var to = GetTableColumn(right);
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

        private void WalkWhereClause(WhereClause context)
        {
            foreach (var pred in context.Predicates)
            {
                WalkFilterPredicate(pred);
            }
        }

        private void WalkFilterPredicate(FilterPredicate context)
        {
            // Like the visitor, a parenthesized predicate is not looked into: none of its parts are
            // direct children of the outer predicate.

            // Get the table column reference (if any)
            var tcRef = context.Columns;
            if (tcRef != null && tcRef.Count > 0)
            {
                var tc = GetTableColumn(tcRef[0]);
                if (tc != null)
                {
                    AddColumnUsage(tc.Value.Table, tc.Value.Column, XmSqlColumnUsage.Filter);

                    switch (context.Kind)
                    {
                        // Extract operator and values
                        case FilterKind.Comparison:
                            if (context.Operator.IsPresent() && context.Value.IsPresent())
                            {
                                var op = context.Operator.ToString();
                                var value = StripQuotes(context.Value.GetText());
                                AddFilterValue(tc.Value.Table, tc.Value.Column, value, op);
                            }
                            break;

                        // IN clause (a tuple IN has no direct valueList)
                        case FilterKind.In:
                            if (context.Values != null)
                            {
                                foreach (var v in context.Values.Values)
                                {
                                    var value = StripQuotes(v.GetText());
                                    AddFilterValue(tc.Value.Table, tc.Value.Column, value, "IN");
                                }
                            }
                            break;

                        // NIN clause
                        case FilterKind.NotIn:
                            AddColumnUsage(tc.Value.Table, tc.Value.Column, XmSqlColumnUsage.Filter);
                            break;

                        // BETWEEN clause
                        case FilterKind.Between:
                            if (context.Value.IsPresent() && context.Value2.IsPresent())
                            {
                                var val1 = StripQuotes(context.Value.GetText());
                                var val2 = StripQuotes(context.Value2.GetText());
                                AddFilterValue(tc.Value.Table, tc.Value.Column, $"{val1} to {val2}", "BETWEEN");
                            }
                            break;

                        // ININDEX
                        case FilterKind.InIndex:
                            // The physical column is already marked as Filter above
                            // The second reference is the temp table
                            break;
                    }
                }
            }

            // COALESCE filter
            var coalesceCtx = context.Coalesce;
            if (coalesceCtx != null)
            {
                WalkCoalesceFilter(coalesceCtx);
            }

            // Catch-all expression predicate may contain callback functions
            var filterExpr = context.Expression;
            if (filterExpr != null)
            {
                MarkCallbacksInExpression(filterExpr);
            }
        }

        private void WalkCoalesceFilter(CoalesceFilter context)
        {
            var coalesceTableRef = context.Table;
            var bracketedName = context.Column;
            if (coalesceTableRef != null && bracketedName.IsPresent())
            {
                var table = GetTableName(coalesceTableRef);
                var column = Unwrap(bracketedName);
                var tc = (!string.IsNullOrEmpty(table) && !string.IsNullOrEmpty(column))
                    ? ((string Table, string Column)?)(table, column)
                    : null;
                if (tc != null)
                {
                    AddColumnUsage(tc.Value.Table, tc.Value.Column, XmSqlColumnUsage.Filter);

                    if (context.Operator.IsPresent() && context.Value.IsPresent())
                    {
                        var op = context.Operator.ToString();
                        var value = StripQuotes(context.Value.GetText());
                        AddFilterValue(tc.Value.Table, tc.Value.Column, value, op);
                    }
                }
            }
        }

        private void WalkCreateShallowRelationStatement(CreateShallowRelation context)
        {
            var fromRef = context.From;
            var toRef = context.To;
            if (fromRef == null || toRef == null) return;

            var from = GetTableColumn(fromRef);
            var to = GetTableColumn(toRef);
            if (from == null || to == null) return;

            var resolvedFrom = ResolveToPhysical(from.Value.Table, from.Value.Column);
            var resolvedTo = ResolveToPhysical(to.Value.Table, to.Value.Column);
            if (resolvedFrom == null || resolvedTo == null) return;
            if (IsTempTable(resolvedFrom.Value.Table) || IsTempTable(resolvedTo.Value.Table)) return;

            // Check for modifiers
            bool isManyToMany = false;
            bool isBoth = false;
            foreach (var mod in context.Modifiers)
            {
                if (mod.EqualsIgnoreCase("MANYTOMANY")) isManyToMany = true;
                if (mod.EqualsIgnoreCase("BOTH")) isBoth = true;
            }

            // Check if relationship already exists
            XmSqlRelationship existing = null;
            foreach (var r in _analysis.Relationships)
            {
                if ((r.FromTable.Equals(resolvedFrom.Value.Table, StringComparison.OrdinalIgnoreCase) &&
                     r.FromColumn.Equals(resolvedFrom.Value.Column, StringComparison.OrdinalIgnoreCase) &&
                     r.ToTable.Equals(resolvedTo.Value.Table, StringComparison.OrdinalIgnoreCase) &&
                     r.ToColumn.Equals(resolvedTo.Value.Column, StringComparison.OrdinalIgnoreCase)) ||
                    (r.FromTable.Equals(resolvedTo.Value.Table, StringComparison.OrdinalIgnoreCase) &&
                     r.FromColumn.Equals(resolvedTo.Value.Column, StringComparison.OrdinalIgnoreCase) &&
                     r.ToTable.Equals(resolvedFrom.Value.Table, StringComparison.OrdinalIgnoreCase) &&
                     r.ToColumn.Equals(resolvedFrom.Value.Column, StringComparison.OrdinalIgnoreCase)))
                {
                    existing = r;
                    break;
                }
            }

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
        }

        // ==================== HELPER METHODS ====================

        private void WalkExpressionForTableColumns(Expression ctx, XmSqlColumnUsage usage)
        {
            if (ctx == null) return;

            foreach (var atom in ctx.Atoms)
            {
                var tcRef = atom.Column;
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
                var nestedExpr = atom.Parenthesized;
                if (nestedExpr != null)
                    WalkExpressionForTableColumns(nestedExpr, usage);

                // The arguments of a call, or the PFCAST argument
                var funcCall = atom.Function;
                if (funcCall != null && funcCall.Arguments != null)
                {
                    foreach (var expr in funcCall.Arguments)
                        WalkExpressionForTableColumns(expr, usage);
                }
            }
        }

        /// <summary>Gets the unwrapped function name from a functionCall
        /// (strips surrounding quotes/brackets). Returns null for PFCAST calls.</summary>
        private static string GetFunctionName(FunctionCall ctx)
        {
            if (ctx == null) return null;
            switch (ctx.NameKind)
            {
                case FunctionNameKind.Identifier: return ctx.Name.ToString();
                case FunctionNameKind.Quoted: return Unwrap(ctx.Name);
                case FunctionNameKind.Bracketed: return StripCallbackArguments(Unwrap(ctx.Name));
                case FunctionNameKind.InlineCallback: return ctx.Name.ToString();
                // PFCAST, COALESCE and PFCASTCOALESCE
                default: return null;
            }
        }

        /// <summary>
        /// Some callbacks are emitted with their DAX expression embedded in the bracketed name,
        /// e.g. [CallbackDataID(Mod([CustomerKey]],100))]. Returns just the leading function name.
        /// </summary>
        private static string StripCallbackArguments(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int paren = name.IndexOf('(');
            return paren > 0 ? name.Substring(0, paren).Trim() : name;
        }

        /// <summary>Finds the first tableColumnRef within a function call's arguments (or PFCAST argument).</summary>
        private static TableColumnRef FindTableColumnRefInFunctionCall(FunctionCall funcCall)
        {
            if (funcCall == null || funcCall.Arguments == null) return null;
            foreach (var e in funcCall.Arguments)
            {
                var found = FindTableColumnRef(e);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Recursively scans an expression for callback functions (e.g. LogAbsValueCallback,
        /// MinMaxColumnPositionCallback) that are emitted as ordinary function calls, and flags
        /// the referenced column as having a callback. This keeps the parser's definition of a
        /// callback consistent with the callback name lists in xmSQL.xshd and
        /// TraceStorageEngineExtensions.
        /// </summary>
        private void MarkCallbacksInExpression(Expression expr)
        {
            if (expr == null) return;
            foreach (var atom in expr.Atoms)
            {
                var funcCall = atom.Function;
                if (funcCall != null)
                {
                    var name = GetFunctionName(funcCall);
                    if (name != null && CallbackFunctionNames.Contains(name))
                    {
                        var tcRef = FindTableColumnRefInFunctionCall(funcCall);
                        if (tcRef != null)
                        {
                            var tc = GetTableColumn(tcRef);
                            if (tc != null)
                                MarkCallback(tc.Value.Table, tc.Value.Column, name);
                        }
                    }

                    // Recurse into the function's arguments (or the PFCAST argument)
                    if (funcCall.Arguments != null)
                    {
                        foreach (var inner in funcCall.Arguments)
                            MarkCallbacksInExpression(inner);
                    }
                }

                var parenExpr = atom.Parenthesized;
                if (parenExpr != null)
                    MarkCallbacksInExpression(parenExpr);
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

        private sealed class TempTableLineage
        {
            public string TempTableName { get; set; }
            public HashSet<string> SourcePhysicalTables { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> SourceTempTables { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, ColumnLineageEntry> ColumnMappings { get; } = new Dictionary<string, ColumnLineageEntry>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class ColumnLineageEntry
        {
            public string TempColumnName { get; set; }
            public string SourceTable { get; set; }
            public string SourceColumn { get; set; }
        }
    }
}
