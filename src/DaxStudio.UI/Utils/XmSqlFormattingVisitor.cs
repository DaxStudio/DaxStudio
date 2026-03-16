using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using DaxStudio.UI.Grammars.Generated;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DaxStudio.UI.Utils
{
    /// <summary>
    /// ANTLR visitor that walks an xmSQL parse tree and emits formatted, simplified text.
    /// Replaces the regex chain in TraceStorageEngineExtensions for formatting and simplification.
    /// </summary>
    internal class XmSqlFormattingVisitor : xmSQLBaseVisitor<object>
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private readonly bool _simplify;
        private readonly bool _format;
        private const string Indent = "    ";

        // Extracted estimated size data
        public long EstimatedRows { get; private set; }
        public long EstimatedBytes { get; private set; }
        public bool HasEstimatedSize { get; private set; }

        private static readonly Regex GuidPattern = new Regex(
            @"([_-]\{?([0-9a-fA-F]){8}-([0-9a-fA-F]){4}-([0-9a-fA-F]){4}-([0-9a-fA-F]){4}-([0-9a-fA-F]){12}\}?)",
            RegexOptions.Compiled);

        private static readonly Regex RowNumberGuidPattern = new Regex(
            @"RowNumber [0-9A-F ]*", RegexOptions.Compiled);

        private static readonly Regex LineagePattern = new Regex(
            @"\s*\(\s*\d+\s*\)\s*", RegexOptions.Compiled);

        private static readonly Regex EstimatedSizePattern = new Regex(
            @"Estimated size .*?:\s*(?<rows>\d+),\s*(?<bytes>\d+)", RegexOptions.Compiled);

        private static readonly Regex PremiumTagsPattern = new Regex(
            @"<pii>|</pii>|<ccon>|</ccon>", RegexOptions.Compiled);

        private readonly Dictionary<string, string> _remapColumns;
        private readonly Dictionary<string, string> _remapTables;

        /// <param name="simplify">When true, removes aliases, lineage, GUIDs, brackets, etc.</param>
        /// <param name="format">When true, applies structural formatting (indentation, line breaks).</param>
        /// <param name="remapColumns">Optional dictionary mapping lineage IDs to friendly column names.</param>
        /// <param name="remapTables">Optional dictionary mapping lineage IDs to friendly table names.</param>
        public XmSqlFormattingVisitor(bool simplify, bool format,
            Dictionary<string, string> remapColumns = null,
            Dictionary<string, string> remapTables = null)
        {
            _simplify = simplify;
            _format = format;
            _remapColumns = remapColumns;
            _remapTables = remapTables;
        }

        public string GetFormattedText() => _sb.ToString();

        // ==================== HELPERS ====================

        /// <summary>Cleans a table name (removes quotes/brackets, lineage, GUIDs, premium tags).</summary>
        private string CleanTableName(string raw)
        {
            if (raw == null) return "";
            // Strip surrounding single quotes
            if (raw.Length >= 2 && raw[0] == '\'' && raw[raw.Length - 1] == '\'')
                raw = raw.Substring(1, raw.Length - 2);
            // Strip surrounding brackets
            if (raw.Length >= 2 && raw[0] == '[' && raw[raw.Length - 1] == ']')
                raw = raw.Substring(1, raw.Length - 2);

            if (_simplify)
            {
                // Check remap before removing lineage
                if (_remapTables != null)
                {
                    var lineageMatch = LineagePattern.Match(raw);
                    if (lineageMatch.Success)
                    {
                        var id = lineageMatch.Value.Trim().Trim('(', ')').Trim();
                        if (_remapTables.TryGetValue(id, out var remapped))
                            return remapped;
                    }
                }
                raw = LineagePattern.Replace(raw, "");
                raw = RemoveGuids(raw);
                raw = PremiumTagsPattern.Replace(raw, "");
                raw = raw.TrimEnd();
            }
            return raw;
        }

        /// <summary>Cleans a column name (removes brackets, lineage, GUIDs, RowNumber GUIDs).</summary>
        private string CleanColumnName(string raw)
        {
            if (raw == null) return "";
            // Strip surrounding brackets
            if (raw.Length >= 2 && raw[0] == '[' && raw[raw.Length - 1] == ']')
                raw = raw.Substring(1, raw.Length - 2);

            if (_simplify)
            {
                // Check remap before removing lineage
                if (_remapColumns != null)
                {
                    var lineageMatch = LineagePattern.Match(raw);
                    if (lineageMatch.Success)
                    {
                        var id = lineageMatch.Value.Trim().Trim('(', ')').Trim();
                        if (_remapColumns.TryGetValue(id, out var remapped))
                            return remapped;
                    }
                }
                // Remove lineage IDs like " ( 123 ) "
                raw = LineagePattern.Replace(raw, "");
                raw = RemoveGuids(raw);
                // Simplify RowNumber GUIDs
                raw = RowNumberGuidPattern.Replace(raw, "RowNumber");
                raw = PremiumTagsPattern.Replace(raw, "");
                raw = raw.TrimEnd();
            }
            return raw;
        }

        private string RemoveGuids(string text)
        {
            return GuidPattern.Replace(text, "");
        }

        /// <summary>Formats a table.column reference based on simplify mode.</summary>
        private void AppendTableColumnRef(xmSQLParser.TableColumnRefContext ctx)
        {
            if (ctx == null) return;

            // Grammar: tableRef BRACKETED_NAME | tableRef DOT BRACKETED_NAME
            var tableRefCtx = ctx.tableRef();

            // Get table name from tableRef (handles both QUOTED_TABLE_NAME and BRACKETED_NAME)
            string tableName = null;
            if (tableRefCtx != null)
            {
                var quoted = tableRefCtx.QUOTED_TABLE_NAME()?.GetText();
                var bracketed = tableRefCtx.BRACKETED_NAME()?.GetText();
                tableName = quoted ?? bracketed;
            }

            // Column is the BRACKETED_NAME that's a direct child of tableColumnRef
            string columnName = ctx.BRACKETED_NAME()?.GetText();

            if (_simplify)
            {
                _sb.Append("'").Append(CleanTableName(tableName)).Append("'");
                _sb.Append("[").Append(CleanColumnName(columnName)).Append("]");
            }
            else
            {
                _sb.Append(tableName);
                if (ctx.DOT() != null) _sb.Append(".");
                _sb.Append(columnName);
            }
        }

        private void AppendTableRef(xmSQLParser.TableRefContext ctx)
        {
            if (ctx == null) return;
            var quoted = ctx.QUOTED_TABLE_NAME()?.GetText();
            var bracketed = ctx.BRACKETED_NAME()?.GetText();
            var raw = quoted ?? bracketed;
            if (_simplify)
                _sb.Append("'").Append(CleanTableName(raw)).Append("'");
            else
                _sb.Append(raw);
        }

        /// <summary>Checks a BRACKETED_NAME token for estimated size annotation and extracts data.</summary>
        private bool TryExtractEstimatedSize(string bracketedText)
        {
            if (bracketedText == null) return false;
            var inner = bracketedText;
            if (inner.Length >= 2 && inner[0] == '[' && inner[inner.Length - 1] == ']')
                inner = inner.Substring(1, inner.Length - 2);
            if (inner.Length >= 2 && inner[0] == '\'' && inner[inner.Length - 1] == '\'')
                inner = inner.Substring(1, inner.Length - 2);

            var m = EstimatedSizePattern.Match(inner);
            if (m.Success)
            {
                if (long.TryParse(m.Groups["rows"].Value, out long rows)
                    && long.TryParse(m.Groups["bytes"].Value, out long bytes))
                {
                    EstimatedRows = rows;
                    EstimatedBytes = bytes;
                    HasEstimatedSize = true;
                    // Emit formatted version
                    _sb.Append("Estimated size: rows = ")
                       .Append(rows.ToString("#,0"))
                       .Append("  bytes = ")
                       .Append(bytes.ToString("#,0"));
                    return true;
                }
            }
            return false;
        }

        /// <summary>Removes alias (AS 'name') from a string token.</summary>
        private string StripAlias(string text)
        {
            if (text == null) return "";
            // Simple approach: find " AS '" and remove to end of next quote
            var idx = text.IndexOf(" AS '", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var endIdx = text.IndexOf('\'', idx + 5);
                if (endIdx >= 0)
                    return text.Substring(0, idx) + text.Substring(endIdx + 1);
            }
            return text;
        }

        // ==================== VISITOR METHODS ====================

        public override object VisitQuery(xmSQLParser.QueryContext context)
        {
            bool first = true;
            foreach (var stmt in context.statement())
            {
                if (!first)
                    _sb.Append(_format ? "\r\n" : ", ");
                first = false;
                Visit(stmt);
            }
            return null;
        }

        public override object VisitSetDirective(xmSQLParser.SetDirectiveContext context)
        {
            _sb.Append("SET ");
            _sb.Append(context.IDENTIFIER().GetText());
            _sb.Append("=");
            _sb.Append(context.QUOTED_STRING().GetText());
            _sb.Append(";");
            return null;
        }

        public override object VisitDefineTableStatement(xmSQLParser.DefineTableStatementContext context)
        {
            _sb.Append("DEFINE TABLE ");
            AppendTableRef(context.tableRef());
            _sb.Append(" :=");
            if (_format)
                _sb.Append("\r\n");
            else
                _sb.Append(" ");
            VisitSelectBody(context.selectBody());
            return null;
        }

        public override object VisitReducedByStatement(xmSQLParser.ReducedByStatementContext context)
        {
            _sb.Append("REDUCED BY ");
            AppendTableRef(context.tableRef());
            _sb.Append(" :=");
            if (_format)
                _sb.Append("\r\n");
            else
                _sb.Append(" ");
            VisitSelectBody(context.selectBody());
            return null;
        }

        public override object VisitCreateShallowRelationStatement(xmSQLParser.CreateShallowRelationStatementContext context)
        {
            _sb.Append("CREATE SHALLOW RELATION ");
            AppendTableRef(context.tableRef());

            foreach (var mod in context.relationModifier())
            {
                if (_format)
                {
                    _sb.Append("\r\n" + Indent);
                    _sb.Append(mod.GetText().ToUpperInvariant());
                }
                else
                {
                    _sb.Append(" ").Append(mod.GetText().ToUpperInvariant());
                }
            }

            var fromRef = context.tableColumnRef(0);
            var toRef = context.tableColumnRef(1);

            if (_format)
            {
                _sb.Append("\r\n" + Indent + "FROM ");
                AppendTableColumnRef(fromRef);
                _sb.Append("\r\n" + Indent + Indent + "TO ");
                AppendTableColumnRef(toRef);
            }
            else
            {
                _sb.Append(" FROM ");
                AppendTableColumnRef(fromRef);
                _sb.Append(" TO ");
                AppendTableColumnRef(toRef);
            }
            return null;
        }

        public override object VisitSelectQueryStatement(xmSQLParser.SelectQueryStatementContext context)
        {
            var withClause = context.withClause();
            if (withClause != null)
            {
                VisitWithClause(withClause);
                if (_format)
                    _sb.Append("\r\n");
                else
                    _sb.Append(" ");
            }
            VisitSelectBody(context.selectBody());
            if (context.SEMICOLON() != null)
                _sb.Append(" ;");
            else if (context.COMMA() != null)
                _sb.Append(",");
            return null;
        }

        public override object VisitWithClause(xmSQLParser.WithClauseContext context)
        {
            _sb.Append("WITH ");
            bool first = true;
            foreach (var expr in context.exprDefinition())
            {
                if (!first) _sb.Append(" ");
                first = false;
                _sb.Append(expr.EXPR_REF().GetText());
                _sb.Append(" := (");
                AppendExpression(expr.expression());
                _sb.Append(")");
            }
            return null;
        }

        public override object VisitSelectBody(xmSQLParser.SelectBodyContext context)
        {
            if (context == null) return null;

            // SELECT
            _sb.Append("SELECT");
            var selectList = context.selectList();
            if (selectList != null)
            {
                var items = selectList.selectItem();
                for (int i = 0; i < items.Length; i++)
                {
                    if (i > 0) _sb.Append(",");
                    if (_format)
                        _sb.Append("\r\n" + Indent);
                    else
                        _sb.Append(" ");
                    VisitSelectItem(items[i]);
                }
            }

            // FROM
            var fromClause = context.fromClause();
            if (fromClause != null)
            {
                if (_format)
                    _sb.Append("\r\n");
                else
                    _sb.Append(" ");
                _sb.Append("FROM ");
                AppendTableRef(fromClause.tableRef());
            }

            // JOINs
            foreach (var join in context.joinClause())
            {
                if (_format)
                    _sb.Append("\r\n" + Indent);
                else
                    _sb.Append(" ");
                VisitJoinClause(join);
            }

            // WHERE
            var whereClause = context.whereClause();
            if (whereClause != null)
            {
                if (_format)
                    _sb.Append("\r\n");
                else
                    _sb.Append(" ");
                VisitWhereClause(whereClause);
            }

            return null;
        }

        public override object VisitSelectItem(xmSQLParser.SelectItemContext context)
        {
            var aggExpr = context.aggregationExpr();
            if (aggExpr != null)
            {
                VisitAggregationExpr(aggExpr);
                AppendAlias(context.alias());
                return null;
            }

            var cbExpr = context.callbackExpr();
            if (cbExpr != null)
            {
                VisitCallbackExpr(cbExpr);
                AppendAlias(context.alias());
                return null;
            }

            var tcRef = context.tableColumnRef();
            if (tcRef != null)
            {
                AppendTableColumnRef(tcRef);
                var cbDataId = context.CALLBACKDATAID();
                if (cbDataId != null)
                {
                    _sb.Append(" ");
                    _sb.Append(cbDataId.GetText());
                }
                AppendAlias(context.alias());
                return null;
            }

            var exprAtRef = context.EXPR_AT_REF();
            if (exprAtRef != null)
            {
                _sb.Append(exprAtRef.GetText());
                AppendAlias(context.alias());
                return null;
            }

            // catch-all expression
            var expr = context.expression();
            if (expr != null)
            {
                AppendExpression(expr);
                return null;
            }

            return null;
        }

        /// <summary>Emits an alias clause (AS [name] or AS 'name'). Skipped when simplifying.</summary>
        private void AppendAlias(xmSQLParser.AliasContext ctx)
        {
            if (ctx == null || _simplify) return;
            _sb.Append(" ");
            _sb.Append(ctx.GetText());
        }

        public override object VisitAggregationExpr(xmSQLParser.AggregationExprContext context)
        {
            var fn = context.aggFunction();
            if (fn == null)
            {
                // Error recovery may produce an aggregationExpr without an aggFunction
                // (e.g., when a keyword like SUM appears without parentheses).
                // Fall back to emitting the raw token text.
                _sb.Append(context.GetText());
                return null;
            }
            _sb.Append(fn.GetText().ToUpperInvariant());
            _sb.Append(" ( ");

            var expr = context.expression();
            if (expr != null)
            {
                AppendExpression(expr);
            }
            else
            {
                var exprRef = context.EXPR_AT_REF();
                if (exprRef != null)
                    _sb.Append(exprRef.GetText());
            }

            _sb.Append(" )");
            return null;
        }

        public override object VisitCallbackExpr(xmSQLParser.CallbackExprContext context)
        {
            var encodeCb = context.ENCODECALLBACK();
            var cbDataId = context.CALLBACKDATAID();
            var cb = context.CALLBACK();

            if (encodeCb != null)
            {
                _sb.Append(encodeCb.GetText());
                _sb.Append(" ( ");
                AppendTableColumnRef(context.tableColumnRef());
                _sb.Append(" )");
            }
            else if (cbDataId != null)
            {
                _sb.Append(cbDataId.GetText());
                _sb.Append(" ( ");
                AppendTableColumnRef(context.tableColumnRef());
                _sb.Append(" )");
            }
            else if (cb != null)
            {
                _sb.Append(cb.GetText());
                var expr = context.expression();
                if (expr != null)
                {
                    _sb.Append(" ");
                    AppendExpression(expr);
                }
                _sb.Append(" ");
                AppendTableColumnRef(context.tableColumnRef());
            }
            return null;
        }

        public override object VisitJoinClause(xmSQLParser.JoinClauseContext context)
        {
            var reverseBitmap = context.reverseBitmapJoin();
            if (reverseBitmap != null)
            {
                _sb.Append("REVERSE BITMAP JOIN ");
                AppendTableRef(reverseBitmap.tableRef());
                var onRefs = reverseBitmap.tableColumnRef();
                if (_format)
                {
                    _sb.Append("\r\n" + Indent + Indent + "ON ");
                }
                else
                {
                    _sb.Append(" ON ");
                }
                AppendTableColumnRef(onRefs[0]);
                _sb.Append("=");
                AppendTableColumnRef(onRefs[1]);
                return null;
            }

            var joinType = context.joinType();
            if (joinType != null)
            {
                // LEFT OUTER JOIN or INNER JOIN
                var leftKw = joinType.LEFT();
                if (leftKw != null)
                    _sb.Append("LEFT OUTER JOIN ");
                else
                    _sb.Append("INNER JOIN ");
            }

            var tableRef = context.tableRef();
            if (tableRef != null)
            {
                AppendTableRef(tableRef);
            }
            else
            {
                var tcRef = context.tableColumnRef();
                if (tcRef != null)
                    AppendTableColumnRef(tcRef);
            }

            var onClause = context.onClause();
            if (onClause != null)
            {
                if (_format)
                    _sb.Append("\r\n" + Indent + Indent + "ON ");
                else
                    _sb.Append(" ON ");
                var onRefs = onClause.tableColumnRef();
                AppendTableColumnRef(onRefs[0]);
                _sb.Append("=");
                AppendTableColumnRef(onRefs[1]);
            }

            return null;
        }

        public override object VisitWhereClause(xmSQLParser.WhereClauseContext context)
        {
            if (_format)
                _sb.Append("WHERE\r\n" + Indent);
            else
                _sb.Append("WHERE ");

            var predicates = context.filterPredicate();
            var logicalOps = context.logicalOp();

            for (int i = 0; i < predicates.Length; i++)
            {
                if (i > 0 && i - 1 < logicalOps.Length)
                {
                    _sb.Append(" ");
                    _sb.Append(logicalOps[i - 1].GetText().ToUpperInvariant());
                    if (_format)
                    {
                        _sb.Append("\r\n");
                        _sb.Append(Indent);
                    }
                    else
                    {
                        _sb.Append(" ");
                    }
                }
                AppendFilterPredicate(predicates[i]);
            }
            return null;
        }

        private void AppendFilterPredicate(xmSQLParser.FilterPredicateContext ctx)
        {
            // Coalesce filter: COALESCE(tableColumnRef) = value
            var coalesce = ctx.coalesceFilter();
            if (coalesce != null)
            {
                AppendCoalesceFilter(coalesce);
                return;
            }

            // COALESCE/PFCASTCOALESCE wrapping an expression: COALESCE((expr))
            var directCoal = ctx.COALESCE();
            var directPfCoal = ctx.PFCASTCOALESCE();
            if (directCoal != null || directPfCoal != null)
            {
                _sb.Append(directCoal != null ? directCoal.GetText() : directPfCoal.GetText());
                _sb.Append(" (  ( ");
                var coalExpr = ctx.expression();
                if (coalExpr != null)
                {
                    AppendExpression(coalExpr);
                }
                _sb.Append(" )  )");
                return;
            }

            var tcRefs = ctx.tableColumnRef();
            if (tcRefs != null && tcRefs.Length > 0)
            {
                // Tuple IN filter: (col1, col2) IN {(v1,v2), ...}
                // Must check before appending first tcRef to avoid double output
                var lbrace = ctx.LBRACE();
                if (lbrace != null)
                {
                    _sb.Append("( ");
                    for (int i = 0; i < tcRefs.Length; i++)
                    {
                        if (i > 0) _sb.Append(", ");
                        AppendTableColumnRef(tcRefs[i]);
                    }
                    _sb.Append(" ) IN { ");
                    var tupleList = ctx.tupleList();
                    if (tupleList != null)
                    {
                        AppendTupleList(tupleList);
                    }
                    _sb.Append(" }");
                    return;
                }

                AppendTableColumnRef(tcRefs[0]);

                // comparison
                var comp = ctx.comparisonOp();
                if (comp != null)
                {
                    _sb.Append(" ").Append(comp.GetText()).Append(" ");
                    AppendFilterValue(ctx.filterValue(0));
                    return;
                }

                // IN
                var inKw = ctx.IN();
                if (inKw != null)
                {
                    _sb.Append(" IN ( ");
                    AppendValueList(ctx.valueList());
                    _sb.Append(" )");
                    return;
                }

                // NIN
                var ninKw = ctx.NIN();
                if (ninKw != null)
                {
                    _sb.Append(" NIN ( ");
                    AppendValueList(ctx.valueList());
                    _sb.Append(" )");
                    return;
                }

                // BETWEEN
                var betweenKw = ctx.BETWEEN();
                if (betweenKw != null)
                {
                    _sb.Append(" BETWEEN ");
                    AppendFilterValue(ctx.filterValue(0));
                    _sb.Append(" AND ");
                    AppendFilterValue(ctx.filterValue(1));
                    return;
                }

                // ININDEX
                var inindexKw = ctx.ININDEX();
                if (inindexKw != null && tcRefs.Length > 1)
                {
                    _sb.Append(" ININDEX ");
                    AppendTableColumnRef(tcRefs[1]);
                    return;
                }

                // callback in filter
                var cbDataId = ctx.CALLBACKDATAID();
                if (cbDataId != null)
                {
                    _sb.Append(" ").Append(cbDataId.GetText());
                    return;
                }
            }

            // catch-all expression
            var expr = ctx.expression();
            if (expr != null)
            {
                AppendExpression(expr);
            }
        }

        private void AppendCoalesceFilter(xmSQLParser.CoalesceFilterContext ctx)
        {
            var pfcc = ctx.PFCASTCOALESCE();
            var coal = ctx.COALESCE();
            _sb.Append(pfcc != null ? pfcc.GetText() : coal[0].GetText());
            _sb.Append(" ( ");
            AppendTableColumnRef(ctx.tableColumnRef());

            // AS type
            var asKw = ctx.AS();
            var identifier = ctx.IDENTIFIER();
            if (asKw != null && identifier != null && !_simplify)
            {
                _sb.Append(" AS ").Append(identifier.GetText());
            }

            _sb.Append(" ) ");
            _sb.Append(ctx.comparisonOp().GetText());
            _sb.Append(" ");

            // Second COALESCE wrapping value (optional)
            if (coal != null && coal.Length > 1)
            {
                _sb.Append("COALESCE ( ");
            }

            var filterValue = ctx.filterValue();
            if (filterValue != null)
            {
                AppendFilterValue(filterValue);
            }

            if (coal != null && coal.Length > 1)
            {
                _sb.Append(" )");
            }
        }

        private void AppendFilterValue(xmSQLParser.FilterValueContext ctx)
        {
            if (ctx == null) return;
            var text = ctx.GetText();

            if (_simplify)
            {
                text = RemoveGuids(text);
                text = PremiumTagsPattern.Replace(text, "");
            }

            _sb.Append(text);
        }

        private void AppendTupleList(xmSQLParser.TupleListContext ctx)
        {
            if (ctx == null) return;
            var valueLists = ctx.valueList();
            for (int i = 0; i < valueLists.Length; i++)
            {
                if (i > 0) _sb.Append(", ");
                _sb.Append("( ");
                AppendValueList(valueLists[i]);
                _sb.Append(" )");
            }
        }

        private void AppendValueList(xmSQLParser.ValueListContext ctx)
        {
            if (ctx == null) return;
            var values = ctx.filterValue();
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) _sb.Append(", ");
                AppendFilterValue(values[i]);
            }
            var trunc = ctx.truncationIndicator();
            if (trunc != null)
            {
                _sb.Append("..");
                var bracketedName = trunc.BRACKETED_NAME()?.GetText();
                if (bracketedName != null)
                {
                    // Check if this is an estimated size annotation
                    if (!TryExtractEstimatedSize(bracketedName))
                    {
                        // Format total values if present
                        var inner = CleanColumnName(bracketedName);
                        _sb.Append("[").Append(FormatTotalValues(inner)).Append("]");
                    }
                }
            }
        }

        /// <summary>Formats large numbers in "N total tuples" text.</summary>
        private static readonly Regex TotalValuesPattern = new Regex(
            @"(?<=^)(\d+)(?=\s+total\s)", RegexOptions.Compiled);

        private static string FormatTotalValues(string text)
        {
            return TotalValuesPattern.Replace(text, m =>
            {
                if (long.TryParse(m.Value, out long num))
                    return num.ToString("#,#");
                return m.Value;
            });
        }

        private void AppendExpression(xmSQLParser.ExpressionContext ctx)
        {
            if (ctx == null) return;
            var atoms = ctx.expressionAtom();
            var ops = ctx.expressionOp();
            for (int i = 0; i < atoms.Length; i++)
            {
                if (i > 0 && i - 1 < ops.Length)
                {
                    _sb.Append(" ").Append(ops[i - 1].GetText()).Append(" ");
                }
                AppendExpressionAtom(atoms[i]);
            }
        }

        private void AppendExpressionAtom(xmSQLParser.ExpressionAtomContext ctx)
        {
            var tcRef = ctx.tableColumnRef();
            if (tcRef != null) { AppendTableColumnRef(tcRef); return; }

            var exprAtRef = ctx.EXPR_AT_REF();
            if (exprAtRef != null) { _sb.Append(exprAtRef.GetText()); return; }

            var exprRef = ctx.EXPR_REF();
            if (exprRef != null) { _sb.Append(exprRef.GetText()); return; }

            var funcCall = ctx.functionCall();
            if (funcCall != null) { AppendFunctionCall(funcCall); return; }

            var parenExpr = ctx.expression();
            if (parenExpr != null)
            {
                _sb.Append("( ");
                AppendExpression(parenExpr);
                _sb.Append(" )");
                return;
            }

            var filterVal = ctx.filterValue();
            if (filterVal != null) { AppendFilterValue(filterVal); return; }

            // AS IDENTIFIER
            var asKw = ctx.AS();
            var ident = ctx.IDENTIFIER();
            if (asKw != null && ident != null)
            {
                if (!_simplify)
                {
                    _sb.Append("AS ").Append(ident.GetText());
                }
                return;
            }
        }

        private void AppendFunctionCall(xmSQLParser.FunctionCallContext ctx)
        {
            var pfcast = ctx.PFCAST();
            if (pfcast != null)
            {
                _sb.Append("PFCAST ( ");
                AppendExpression(ctx.expression());
                if (!_simplify)
                {
                    _sb.Append(" AS ").Append(ctx.IDENTIFIER().GetText());
                }
                _sb.Append(" )");
                return;
            }

            var ident = ctx.IDENTIFIER();
            if (ident != null)
            {
                _sb.Append(ident.GetText());
            }
            else
            {
                // Check for COALESCE / PFCASTCOALESCE used as function names
                var coal = ctx.COALESCE();
                var pfcastCoal = ctx.PFCASTCOALESCE();
                if (coal != null)
                {
                    _sb.Append(coal.GetText());
                }
                else if (pfcastCoal != null)
                {
                    _sb.Append(pfcastCoal.GetText());
                }
                else
                {
                    // Function name is a QUOTED_TABLE_NAME (e.g. 'LogAbsValueCallback') or
                    // BRACKETED_NAME (e.g. [MinMaxColumnPositionCallback]) — strip quotes/brackets
                    var quoted = ctx.QUOTED_TABLE_NAME();
                    if (quoted != null)
                    {
                        _sb.Append(quoted.GetText().Trim('\''));
                    }
                    else
                    {
                        var bracketed = ctx.BRACKETED_NAME();
                        if (bracketed != null)
                        {
                            var name = bracketed.GetText();
                            _sb.Append(name.Substring(1, name.Length - 2));
                        }
                    }
                }
            }

            _sb.Append(" ( ");
            var exprList = ctx.expressionList();
            if (exprList != null)
            {
                var exprs = exprList.expression();
                for (int i = 0; i < exprs.Length; i++)
                {
                    if (i > 0) _sb.Append(", ");
                    AppendExpression(exprs[i]);
                }
            }
            _sb.Append(" )");
        }
    }
}
