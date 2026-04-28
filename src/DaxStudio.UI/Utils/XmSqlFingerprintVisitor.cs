using Antlr4.Runtime.Tree;
using DaxStudio.UI.Grammars.Generated;
using DaxStudio.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DaxStudio.UI.Utils
{
    /// <summary>
    /// Lightweight ANTLR visitor that extracts structural elements from an xmSQL parse tree
    /// and computes fingerprint hashes for grouping similar queries.
    /// Does NOT populate XmSqlAnalysis — this is purely for structural comparison.
    /// </summary>
    internal class XmSqlFingerprintVisitor : xmSQLBaseVisitor<object>
    {
        // Lineage tracking (shared with analysis visitor approach)
        private readonly Dictionary<string, FingerprintTempTableLineage> _tempTableLineage
            = new Dictionary<string, FingerprintTempTableLineage>(StringComparer.OrdinalIgnoreCase);

        // Extracted structural elements for the main (final) select body
        private readonly List<string> _selectColumns = new List<string>();
        private readonly List<string> _aggregations = new List<string>();
        private string _fromTable;
        private readonly List<string> _joinSignatures = new List<string>();
        private readonly List<string> _whereColumnSignatures = new List<string>();

        // Context flags


        /// <summary>
        /// Computes a fingerprint for the given parse tree.
        /// </summary>
        public XmSqlQueryFingerprint ComputeFingerprint(xmSQLParser.QueryContext tree)
        {
            // First pass: build temp table lineage
            BuildLineage(tree);

            // Second pass: find the main (final) select statement and extract structure
            ExtractMainSelectStructure(tree);

            return BuildFingerprint();
        }

        #region Lineage Building

        private void BuildLineage(xmSQLParser.QueryContext tree)
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

            var lineage = new FingerprintTempTableLineage { TempTableName = tempName };

            var fromTable = GetTableName(body.fromClause()?.tableRef());
            if (fromTable != null)
            {
                if (IsTempTable(fromTable))
                    lineage.SourceTempTables.Add(fromTable);
                else
                    lineage.SourcePhysicalTables.Add(fromTable);
            }

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
                    lineage.ColumnMappings[col] = (srcTable, srcCol);
                    if (!IsTempTable(srcTable))
                        lineage.SourcePhysicalTables.Add(srcTable);
                }
                else if (!IsTempTable(tc.Value.Table))
                {
                    lineage.SourcePhysicalTables.Add(tc.Value.Table);
                    lineage.ColumnMappings[col] = (tc.Value.Table, col);
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

        #endregion

        #region Structure Extraction

        private void ExtractMainSelectStructure(xmSQLParser.QueryContext tree)
        {
            // Find the main select statement (the last selectQueryStatement, or
            // the final DEFINE TABLE if no standalone select exists)
            xmSQLParser.SelectBodyContext mainBody = null;

            foreach (var stmt in tree.statement())
            {
                var selectQuery = stmt.selectQueryStatement();
                if (selectQuery != null)
                {
                    mainBody = selectQuery.selectBody();
                }

                // For DEFINE TABLE statements, use the last one as the "main" body
                var define = stmt.defineTableStatement();
                if (define != null)
                {
                    mainBody = define.selectBody();
                }

                var reduced = stmt.reducedByStatement();
                if (reduced != null)
                {
                    mainBody = reduced.selectBody();
                }
            }

            if (mainBody != null)
            {
                // collecting structure from the main select body
                ExtractSelectBodyStructure(mainBody);
            }
        }

        private void ExtractSelectBodyStructure(xmSQLParser.SelectBodyContext body)
        {
            if (body == null) return;

            // FROM clause
            var fromCtx = body.fromClause();
            if (fromCtx != null)
            {
                var tableName = GetTableName(fromCtx.tableRef());
                _fromTable = ResolveTableName(tableName);
            }

            // SELECT clause
            var selectList = body.selectList();
            if (selectList != null)
            {
                foreach (var item in selectList.selectItem())
                {
                    ExtractSelectItemStructure(item);
                }
            }

            // JOIN clauses
            foreach (var joinCtx in body.joinClause())
            {
                ExtractJoinStructure(joinCtx);
            }

            // WHERE clause
            var whereCtx = body.whereClause();
            if (whereCtx != null)
            {
                ExtractWhereStructure(whereCtx);
            }
        }

        private void ExtractSelectItemStructure(xmSQLParser.SelectItemContext item)
        {
            // Aggregation expression
            var aggExpr = item.aggregationExpr();
            if (aggExpr != null)
            {
                var aggFunc = aggExpr.aggFunction().GetText().ToUpperInvariant();
                var tcRef = FindTableColumnRef(aggExpr.expression());
                if (tcRef != null)
                {
                    var tc = GetTableColumn(tcRef);
                    if (tc != null)
                    {
                        var resolved = ResolveToPhysical(tc.Value.Table, tc.Value.Column);
                        if (resolved != null)
                            _aggregations.Add($"{aggFunc}({resolved.Value.Table}.{resolved.Value.Column})");
                        else
                            _aggregations.Add($"{aggFunc}({tc.Value.Table}.{tc.Value.Column})");
                    }
                }
                else
                {
                    // COUNT() without column
                    _aggregations.Add($"{aggFunc}()");
                }
                return;
            }

            // Callback expression
            var cbExpr = item.callbackExpr();
            if (cbExpr != null)
            {
                var tcRef = cbExpr.tableColumnRef();
                if (tcRef != null)
                {
                    var tc = GetTableColumn(tcRef);
                    if (tc != null)
                    {
                        var resolved = ResolveToPhysical(tc.Value.Table, tc.Value.Column);
                        if (resolved != null)
                            _selectColumns.Add($"CB({resolved.Value.Table}.{resolved.Value.Column})");
                        else
                            _selectColumns.Add($"CB({tc.Value.Table}.{tc.Value.Column})");
                    }
                }
                return;
            }

            // Table[Column] reference
            var tableColRef = item.tableColumnRef();
            if (tableColRef != null)
            {
                var tc = GetTableColumn(tableColRef);
                if (tc != null)
                {
                    var resolved = ResolveToPhysical(tc.Value.Table, tc.Value.Column);
                    if (resolved != null)
                        _selectColumns.Add($"{resolved.Value.Table}.{resolved.Value.Column}");
                    else
                        _selectColumns.Add($"{tc.Value.Table}.{tc.Value.Column}");
                }
            }
        }

        private void ExtractJoinStructure(xmSQLParser.JoinClauseContext joinCtx)
        {
            // REVERSE BITMAP JOIN
            var rbj = joinCtx.reverseBitmapJoin();
            if (rbj != null)
            {
                var tableName = ResolveTableName(GetTableName(rbj.tableRef()));
                var refs = rbj.tableColumnRef();
                if (refs != null && refs.Length == 2)
                {
                    var from = GetTableColumn(refs[0]);
                    var to = GetTableColumn(refs[1]);
                    if (from != null && to != null)
                    {
                        var rFrom = ResolveToPhysical(from.Value.Table, from.Value.Column);
                        var rTo = ResolveToPhysical(to.Value.Table, to.Value.Column);
                        var fromStr = rFrom != null ? $"{rFrom.Value.Table}.{rFrom.Value.Column}" : $"{from.Value.Table}.{from.Value.Column}";
                        var toStr = rTo != null ? $"{rTo.Value.Table}.{rTo.Value.Column}" : $"{to.Value.Table}.{to.Value.Column}";
                        _joinSignatures.Add($"RBJ:{tableName}|ON:{fromStr}={toStr}");
                    }
                }
                else
                {
                    _joinSignatures.Add($"RBJ:{tableName}");
                }
                return;
            }

            // Standard JOIN
            var joinTypeCtx = joinCtx.joinType();
            var joinTypeStr = "JOIN";
            if (joinTypeCtx != null)
            {
                if (joinTypeCtx.LEFT() != null) joinTypeStr = "LOJ";
                else if (joinTypeCtx.INNER() != null) joinTypeStr = "IJ";
            }

            var tableRef = joinCtx.tableRef();
            string joinTableName = null;
            if (tableRef != null)
            {
                joinTableName = ResolveTableName(GetTableName(tableRef));
            }
            else
            {
                var tcRef = joinCtx.tableColumnRef();
                if (tcRef != null)
                {
                    var tc = GetTableColumn(tcRef);
                    if (tc != null)
                    {
                        var resolved = ResolveToPhysical(tc.Value.Table, tc.Value.Column);
                        joinTableName = resolved?.Table ?? tc.Value.Table;
                    }
                }
            }

            // ON clause
            var onCtx = joinCtx.onClause();
            if (onCtx != null)
            {
                var refs = onCtx.tableColumnRef();
                if (refs.Length == 2)
                {
                    var from = GetTableColumn(refs[0]);
                    var to = GetTableColumn(refs[1]);
                    if (from != null && to != null)
                    {
                        var rFrom = ResolveToPhysical(from.Value.Table, from.Value.Column);
                        var rTo = ResolveToPhysical(to.Value.Table, to.Value.Column);
                        var fromStr = rFrom != null ? $"{rFrom.Value.Table}.{rFrom.Value.Column}" : $"{from.Value.Table}.{from.Value.Column}";
                        var toStr = rTo != null ? $"{rTo.Value.Table}.{rTo.Value.Column}" : $"{to.Value.Table}.{to.Value.Column}";
                        _joinSignatures.Add($"{joinTypeStr}:{joinTableName}|ON:{fromStr}={toStr}");
                        return;
                    }
                }
            }

            if (joinTableName != null)
            {
                _joinSignatures.Add($"{joinTypeStr}:{joinTableName}");
            }
        }

        private void ExtractWhereStructure(xmSQLParser.WhereClauseContext whereCtx)
        {
            foreach (var pred in whereCtx.filterPredicate())
            {
                ExtractFilterPredicateStructure(pred);
            }
        }

        private void ExtractFilterPredicateStructure(xmSQLParser.FilterPredicateContext pred)
        {
            var tcRefs = pred.tableColumnRef();
            if (tcRefs != null && tcRefs.Length > 0)
            {
                var tc = GetTableColumn(tcRefs[0]);
                if (tc != null)
                {
                    var resolved = ResolveToPhysical(tc.Value.Table, tc.Value.Column);
                    var colRef = resolved != null
                        ? $"{resolved.Value.Table}.{resolved.Value.Column}"
                        : $"{tc.Value.Table}.{tc.Value.Column}";

                    // Determine operator type
                    string op = "?";
                    if (pred.comparisonOp() != null)
                        op = pred.comparisonOp().GetText();
                    else if (pred.IN() != null)
                        op = "IN";
                    else if (pred.NIN() != null)
                        op = "NIN";
                    else if (pred.BETWEEN() != null)
                        op = "BETWEEN";
                    else if (pred.ININDEX() != null)
                        op = "ININDEX";
                    else if (pred.CALLBACKDATAID() != null)
                        op = "CBID";

                    _whereColumnSignatures.Add($"{colRef}({op})");
                }
            }

            // COALESCE filter
            var coalesceCtx = pred.coalesceFilter();
            if (coalesceCtx != null)
            {
                var coalesceTableRef = coalesceCtx.tableRef();
                var bracketedName = coalesceCtx.BRACKETED_NAME();
                if (coalesceTableRef != null && bracketedName != null)
                {
                    var table = GetTableName(coalesceTableRef);
                    var column = GetBracketedContent(bracketedName);
                    var tc = (!string.IsNullOrEmpty(table) && !string.IsNullOrEmpty(column))
                        ? ((string Table, string Column)?)(table, column)
                        : null;
                    if (tc != null)
                    {
                        var resolved = ResolveToPhysical(tc.Value.Table, tc.Value.Column);
                        var colRef = resolved != null
                            ? $"{resolved.Value.Table}.{resolved.Value.Column}"
                            : $"{tc.Value.Table}.{tc.Value.Column}";
                        var op = coalesceCtx.comparisonOp()?.GetText() ?? "?";
                        _whereColumnSignatures.Add($"COALESCE:{colRef}({op})");
                    }
                }
            }
        }

        #endregion

        #region Fingerprint Building

        private XmSqlQueryFingerprint BuildFingerprint()
        {
            // Sort for deterministic ordering
            _selectColumns.Sort(StringComparer.OrdinalIgnoreCase);
            _aggregations.Sort(StringComparer.OrdinalIgnoreCase);
            _joinSignatures.Sort(StringComparer.OrdinalIgnoreCase);
            _whereColumnSignatures.Sort(StringComparer.OrdinalIgnoreCase);

            var selectSig = string.Join(",", _selectColumns.Concat(_aggregations));
            var fromJoinSig = _fromTable ?? "";
            if (_joinSignatures.Count > 0)
                fromJoinSig += "|" + string.Join("|", _joinSignatures);
            var whereSig = string.Join(",", _whereColumnSignatures);

            // Full structural hash: SELECT + FROM + JOIN + WHERE columns
            var fullStructure = $"S:{selectSig}|F:{fromJoinSig}|W:{whereSig}";
            var fullHash = ComputeHash(fullStructure);

            // Table access hash: FROM + JOIN + WHERE columns only (ignores SELECT)
            var accessStructure = $"F:{fromJoinSig}|W:{whereSig}";
            var accessHash = ComputeHash(accessStructure);

            return new XmSqlQueryFingerprint
            {
                FullStructuralHash = fullHash,
                TableAccessHash = accessHash,
                SelectSignature = selectSig,
                FromJoinSignature = fromJoinSig,
                WhereColumnsSignature = whereSig
            };
        }

        private static string ComputeHash(string input)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                // Use first 8 bytes (16 hex chars) for a compact but collision-resistant hash
                return BitConverter.ToString(bytes, 0, 8).Replace("-", "").ToLowerInvariant();
            }
        }

        #endregion

        #region Helpers

        private static string GetTableName(ITerminalNode node)
        {
            if (node == null) return null;
            var text = node.GetText();
            return text.Length >= 2 ? text.Substring(1, text.Length - 2) : text;
        }

        private static string GetTableName(xmSQLParser.TableRefContext ctx)
        {
            return ctx == null ? null : GetTableName(ctx.QUOTED_TABLE_NAME());
        }

        private static string GetBracketedContent(ITerminalNode node)
        {
            if (node == null) return null;
            var text = node.GetText();
            return text.Length >= 2 ? text.Substring(1, text.Length - 2) : text;
        }

        private (string Table, string Column)? GetTableColumn(xmSQLParser.TableColumnRefContext ctx)
        {
            if (ctx == null) return null;
            var table = GetTableName(ctx.tableRef());
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

        private string ResolveTableName(string tableName)
        {
            if (tableName == null) return null;
            if (!IsTempTable(tableName)) return tableName;
            if (_tempTableLineage.TryGetValue(tableName, out var lineage) && lineage.SourcePhysicalTables.Count > 0)
                return string.Join("+", lineage.SourcePhysicalTables.OrderBy(t => t, StringComparer.OrdinalIgnoreCase));
            return tableName;
        }

        private (string Table, string Column)? ResolveToPhysical(string tableName, string columnName)
        {
            var dollarIdx = columnName.IndexOf('$');
            if (dollarIdx > 0)
            {
                var sourceTable = columnName.Substring(0, dollarIdx);
                var sourceColumn = columnName.Substring(dollarIdx + 1);
                return (sourceTable, sourceColumn);
            }

            if (IsTempTable(tableName) && _tempTableLineage.TryGetValue(tableName, out var lineage))
            {
                if (lineage.ColumnMappings.TryGetValue(columnName, out var mapping))
                    return mapping;
                if (lineage.SourcePhysicalTables.Count == 1)
                    return (lineage.SourcePhysicalTables.First(), columnName);
            }

            if (!IsTempTable(tableName))
                return (tableName, columnName);

            return null;
        }

        #endregion

        #region Lineage Data Structures

        private class FingerprintTempTableLineage
        {
            public string TempTableName { get; set; }
            public HashSet<string> SourcePhysicalTables { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> SourceTempTables { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, (string Table, string Column)> ColumnMappings { get; }
                = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
        }

        #endregion
    }
}
