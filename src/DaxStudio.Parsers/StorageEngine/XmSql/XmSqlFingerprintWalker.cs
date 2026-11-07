using Parlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DaxStudio.Parsers.StorageEngine.XmSql
{
    /// <summary>
    /// Lightweight walker that extracts structural elements from an <see cref="XmSqlQuery"/>
    /// and computes fingerprint hashes for grouping similar queries. This is the Parlot counterpart of
    /// <see cref="XmSqlFingerprintVisitor"/> and computes the same fingerprints.
    /// Does NOT populate XmSqlAnalysis — this is purely for structural comparison.
    /// </summary>
    internal sealed class XmSqlFingerprintWalker
    {
        // Lineage tracking (shared with analysis walker approach)
        private readonly Dictionary<string, FingerprintTempTableLineage> _tempTableLineage
            = new Dictionary<string, FingerprintTempTableLineage>(StringComparer.OrdinalIgnoreCase);

        // Extracted structural elements for the main (final) select body
        private readonly List<string> _selectColumns = new List<string>();
        private readonly List<string> _aggregations = new List<string>();
        private string _fromTable;
        private readonly List<string> _joinSignatures = new List<string>();
        private readonly List<string> _whereColumnSignatures = new List<string>();

        /// <summary>
        /// Computes a fingerprint for the given syntax tree.
        /// </summary>
        public XmSqlQueryFingerprint ComputeFingerprint(XmSqlQuery tree)
        {
            // First pass: build temp table lineage
            BuildLineage(tree);

            // Second pass: find the main (final) select statement and extract structure
            ExtractMainSelectStructure(tree);

            return BuildFingerprint();
        }

        #region Lineage Building

        private void BuildLineage(XmSqlQuery tree)
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

            var lineage = new FingerprintTempTableLineage { TempTableName = tempName };

            var fromTable = GetTableName(body.From);
            if (fromTable != null)
            {
                if (IsTempTable(fromTable))
                    lineage.SourceTempTables.Add(fromTable);
                else
                    lineage.SourcePhysicalTables.Add(fromTable);
            }

            // The visitor reads joinClause.tableRef(), which a REVERSE BITMAP JOIN doesn't have
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

        private void ExtractMainSelectStructure(XmSqlQuery tree)
        {
            // Find the main select statement (the last selectQueryStatement, or
            // the final DEFINE TABLE if no standalone select exists)
            SelectBody mainBody = null;

            foreach (var stmt in tree.Statements)
            {
                if (stmt is SelectQuery selectQuery)
                {
                    mainBody = selectQuery.Body;
                }

                // For DEFINE TABLE (and REDUCED BY) statements, use the last one as the "main" body
                if (stmt is TempTableStatement temp)
                {
                    mainBody = temp.Body;
                }
            }

            if (mainBody != null)
            {
                // collecting structure from the main select body
                ExtractSelectBodyStructure(mainBody);
            }
        }

        private void ExtractSelectBodyStructure(SelectBody body)
        {
            if (body == null) return;

            // FROM clause
            if (body.From != null)
            {
                var tableName = GetTableName(body.From);
                _fromTable = ResolveTableName(tableName);
            }

            // SELECT clause
            if (body.Items != null)
            {
                foreach (var item in body.Items)
                {
                    ExtractSelectItemStructure(item);
                }
            }

            // JOIN clauses
            foreach (var joinCtx in body.Joins)
            {
                ExtractJoinStructure(joinCtx);
            }

            // WHERE clause
            if (body.Where != null)
            {
                ExtractWhereStructure(body.Where);
            }
        }

        private void ExtractSelectItemStructure(SelectItem item)
        {
            // Aggregation expression
            var aggExpr = item.Aggregation;
            if (aggExpr != null)
            {
                var aggFunc = aggExpr.Function.ToString().ToUpperInvariant();
                var tcRef = FindTableColumnRef(aggExpr.Expression);
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
            var cbExpr = item.Callback;
            if (cbExpr != null)
            {
                var tcRef = cbExpr.Column;
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
            var tableColRef = item.Column;
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

        private void ExtractJoinStructure(JoinClause joinCtx)
        {
            // REVERSE BITMAP JOIN
            if (joinCtx.Kind == JoinKind.ReverseBitmap)
            {
                var tableName = ResolveTableName(GetTableName(joinCtx.Table));
                if (joinCtx.OnLeft != null && joinCtx.OnRight != null)
                {
                    var from = GetTableColumn(joinCtx.OnLeft);
                    var to = GetTableColumn(joinCtx.OnRight);
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
            var joinTypeStr = joinCtx.Kind == JoinKind.LeftOuter ? "LOJ" : "IJ";

            var tableRef = joinCtx.Table;
            string joinTableName = null;
            if (tableRef != null)
            {
                joinTableName = ResolveTableName(GetTableName(tableRef));
            }
            else
            {
                var tcRef = joinCtx.TableColumn;
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
            if (joinCtx.OnLeft != null && joinCtx.OnRight != null)
            {
                var from = GetTableColumn(joinCtx.OnLeft);
                var to = GetTableColumn(joinCtx.OnRight);
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

            if (joinTableName != null)
            {
                _joinSignatures.Add($"{joinTypeStr}:{joinTableName}");
            }
        }

        private void ExtractWhereStructure(WhereClause whereCtx)
        {
            foreach (var pred in whereCtx.Predicates)
            {
                ExtractFilterPredicateStructure(pred);
            }
        }

        private void ExtractFilterPredicateStructure(FilterPredicate pred)
        {
            // Like the visitor, a parenthesized predicate is not looked into.
            var tcRefs = pred.Columns;
            if (tcRefs != null && tcRefs.Count > 0)
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
                    switch (pred.Kind)
                    {
                        case FilterKind.Comparison: op = pred.Operator.ToString(); break;
                        case FilterKind.In:
                        case FilterKind.TupleIn: op = "IN"; break;
                        case FilterKind.NotIn: op = "NIN"; break;
                        case FilterKind.Between: op = "BETWEEN"; break;
                        case FilterKind.InIndex: op = "ININDEX"; break;
                        case FilterKind.CallbackDataId: op = "CBID"; break;
                    }

                    _whereColumnSignatures.Add($"{colRef}({op})");
                }
            }

            // COALESCE filter
            var coalesceCtx = pred.Coalesce;
            if (coalesceCtx != null)
            {
                var coalesceTableRef = coalesceCtx.Table;
                var bracketedName = coalesceCtx.Column;
                if (coalesceTableRef != null && bracketedName.IsPresent())
                {
                    var table = GetTableName(coalesceTableRef);
                    var column = Unwrap(bracketedName);
                    var tc = (!string.IsNullOrEmpty(table) && !string.IsNullOrEmpty(column))
                        ? ((string Table, string Column)?)(table, column)
                        : null;
                    if (tc != null)
                    {
                        var resolved = ResolveToPhysical(tc.Value.Table, tc.Value.Column);
                        var colRef = resolved != null
                            ? $"{resolved.Value.Table}.{resolved.Value.Column}"
                            : $"{tc.Value.Table}.{tc.Value.Column}";
                        var op = coalesceCtx.Operator.GetText() ?? "?";
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

        /// <summary>Strips the surrounding quotes or brackets of a token.</summary>
        private static string Unwrap(TextSpan span)
        {
            if (span.IsMissing()) return null;
            return span.Length >= 2 ? span.Buffer.Substring(span.Offset + 1, span.Length - 2) : span.ToString();
        }

        /// <summary>Only quoted table names count, as in the visitor: a bracketed table name gives null.</summary>
        private static string GetTableName(TableRef ctx)
        {
            return ctx == null || !ctx.IsQuoted ? null : Unwrap(ctx.Text);
        }

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

        private sealed class FingerprintTempTableLineage
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
