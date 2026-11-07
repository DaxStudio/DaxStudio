using Parlot;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DaxStudio.Parsers.StorageEngine.XmSql
{
    /// <summary>
    /// Walks an <see cref="XmSqlQuery"/> and emits formatted, simplified text. This is the Parlot counterpart of
    /// <see cref="XmSqlFormattingVisitor"/>: the methods mirror the visitor's and produce the same text.
    /// </summary>
    internal sealed class XmSqlFormattingWalker
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private readonly bool _simplify;
        private readonly bool _format;
        private const string Indent = "    ";

        private string _text;

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

        // Keep in sync with XmSqlAnalysisWalker and xmSQL.xshd.
        private static readonly Regex CallbackNamePattern = new Regex(
            @"^\s*(CallbackDataID|EncodeCallback|LogAbsValueCallback|RoundValueCallback|MinMaxColumnPositionCallback|Cond)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly Dictionary<string, string> _remapColumns;
        private readonly Dictionary<string, string> _remapTables;

        private readonly bool _convertDates;
        private readonly HashSet<string> _dateColumnIds;

        public XmSqlFormattingWalker(bool simplify, bool format, bool convertDates = false,
            Dictionary<string, string> remapColumns = null,
            Dictionary<string, string> remapTables = null,
            HashSet<string> dateColumnIds = null)
        {
            _simplify = simplify;
            _format = format;
            _convertDates = convertDates;
            _remapColumns = remapColumns;
            _remapTables = remapTables;
            _dateColumnIds = dateColumnIds;
        }

        public string GetFormattedText() => _sb.ToString();

        private void Append(TextSpan span) => _sb.Append(span.Buffer, span.Offset, span.Length);

        // ==================== HELPERS ====================

        /// <summary>Cleans a table name (removes quotes/brackets, lineage, GUIDs, premium tags).</summary>
        private string CleanTableName(TextSpan raw) => CleanTableName(StripDelimiters(raw, '\'', '\'', '[', ']'));

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
                    if (_remapTables.TryGetValue(raw, out var remapped))
                        return remapped;

                    var lineageMatch = MayHaveLineage(raw) ? LineagePattern.Match(raw) : Match.Empty;
                    if (lineageMatch.Success)
                    {
                        var id = lineageMatch.Value.Trim().Trim('(', ')').Trim();
                        if (_remapTables.TryGetValue(id, out var lineageRemapped))
                            return lineageRemapped;
                    }
                }
                if (MayHaveLineage(raw)) raw = LineagePattern.Replace(raw, "");
                raw = RemoveGuids(raw);
                if (MayHavePremiumTags(raw)) raw = PremiumTagsPattern.Replace(raw, "");
                raw = raw.TrimEnd();
            }
            return raw;
        }

        /// <summary>
        /// Returns the text of a token without its surrounding delimiters (quotes, then brackets), allocating
        /// one string instead of one per stripped pair.
        /// </summary>
        private static string StripDelimiters(TextSpan span, char open1, char close1, char open2, char close2)
        {
            var buffer = span.Buffer;
            int start = span.Offset, length = span.Length;
            if (length >= 2 && buffer[start] == open1 && buffer[start + length - 1] == close1)
            {
                start++;
                length -= 2;
            }
            if (length >= 2 && buffer[start] == open2 && buffer[start + length - 1] == close2)
            {
                start++;
                length -= 2;
            }
            return buffer.Substring(start, length);
        }

        // The patterns below cannot match without these chars: checking for them first skips most regex scans.
        private static bool MayHaveLineage(string text) => text.IndexOf('(') >= 0;

        private static bool MayHavePremiumTags(string text) => text.IndexOf('<') >= 0;

        /// <summary>Cleans a column name (removes brackets, lineage, GUIDs, RowNumber GUIDs).</summary>
        private string CleanColumnName(TextSpan raw) => CleanColumnName(StripDelimiters(raw, '[', ']', '\0', '\0'), stripped: true);

        /// <summary>Cleans a column name (removes brackets, lineage, GUIDs, RowNumber GUIDs).</summary>
        private string CleanColumnName(string raw, bool stripped = false)
        {
            if (raw == null) return "";
            // Strip surrounding brackets
            if (!stripped && raw.Length >= 2 && raw[0] == '[' && raw[raw.Length - 1] == ']')
                raw = raw.Substring(1, raw.Length - 2);
            raw = UnescapeBrackets(raw);

            if (_simplify)
            {
                // Check remap before removing lineage
                if (_remapColumns != null)
                {
                    if (_remapColumns.TryGetValue(raw, out var remapped))
                        return remapped;

                    var lineageMatch = MayHaveLineage(raw) ? LineagePattern.Match(raw) : Match.Empty;
                    if (lineageMatch.Success)
                    {
                        var id = lineageMatch.Value.Trim().Trim('(', ')').Trim();
                        if (_remapColumns.TryGetValue(id, out var lineageRemapped))
                            return lineageRemapped;
                    }
                }
                // Remove lineage IDs like " ( 123 ) "
                if (MayHaveLineage(raw)) raw = LineagePattern.Replace(raw, "");
                raw = RemoveGuids(raw);
                // Simplify RowNumber GUIDs
                if (raw.IndexOf("RowNumber ", StringComparison.Ordinal) >= 0) raw = RowNumberGuidPattern.Replace(raw, "RowNumber");
                if (MayHavePremiumTags(raw)) raw = PremiumTagsPattern.Replace(raw, "");
                raw = raw.TrimEnd();
            }
            return raw;
        }

        private static string RemoveGuids(string text)
        {
            // A GUID has four '-'
            return text.IndexOf('-') >= 0 ? GuidPattern.Replace(text, "") : text;
        }

        private static string UnescapeBrackets(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.IndexOf("]]", StringComparison.Ordinal) >= 0 ? text.Replace("]]", "]") : text;
        }

        /// <summary>Emits the original text between two offsets, applying simplification if enabled.</summary>
        private void EmitOriginalText(int start, int end)
        {
            string rawText = _text.Substring(start, end - start);

            if (_simplify)
            {
                rawText = LineagePattern.Replace(rawText, "");
                rawText = RemoveGuids(rawText);
                rawText = PremiumTagsPattern.Replace(rawText, "");
            }
            _sb.Append(rawText);
        }

        private bool IsDateColumnByName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return false;

            // Strategy 1: Check metadata-based date column IDs (positive match)
            if (_dateColumnIds != null && _dateColumnIds.Count > 0)
            {
                var lineageMatch = LineagePattern.Match(rawName);
                if (lineageMatch.Success)
                {
                    var id = lineageMatch.Value.Trim().Trim('(', ')').Trim();
                    if (_dateColumnIds.Contains(id)) return true;
                }
                else if (_dateColumnIds.Contains(rawName))
                {
                    return true;
                }
            }

            // Strategy 2: Fall back to name heuristic — check the cleaned/remapped column name
            string cleanedName = CleanColumnName("[" + rawName + "]");
            return cleanedName.IndexOf("date", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsDateColumn(TableColumnRef colRef)
        {
            if (colRef == null) return false;

            var bracketedName = colRef.Column.GetText();
            if (bracketedName == null) return false;
            var rawName = bracketedName;
            // Strip surrounding brackets
            if (rawName.Length >= 2 && rawName[0] == '[' && rawName[rawName.Length - 1] == ']')
                rawName = rawName.Substring(1, rawName.Length - 2);

            // Strategy 1: Check metadata-based date column IDs (positive match)
            if (_dateColumnIds != null && _dateColumnIds.Count > 0)
            {
                var lineageMatch = LineagePattern.Match(rawName);
                if (lineageMatch.Success)
                {
                    var id = lineageMatch.Value.Trim().Trim('(', ')').Trim();
                    if (_dateColumnIds.Contains(id)) return true;
                }
                else if (_dateColumnIds.Contains(rawName))
                {
                    return true;
                }
            }

            // Strategy 2: Fall back to name heuristic — check the cleaned/remapped column name
            string cleanedName = CleanColumnName(bracketedName);
            return cleanedName.IndexOf("date", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Formats a table.column reference based on simplify mode.</summary>
        private void AppendTableColumnRef(TableColumnRef ctx)
        {
            if (ctx == null) return;

            if (_simplify)
            {
                _sb.Append("'").Append(CleanTableName(ctx.Table.Text)).Append("'");
                _sb.Append("[").Append(CleanColumnName(ctx.Column)).Append("]");
            }
            else
            {
                Append(ctx.Table.Text);
                if (ctx.HasDot) _sb.Append(".");
                Append(ctx.Column);
            }
        }

        private void AppendTableRef(TableRef ctx)
        {
            if (ctx == null) return;
            if (_simplify)
                _sb.Append("'").Append(CleanTableName(ctx.Text)).Append("'");
            else
                Append(ctx.Text);
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

        // ==================== WALK ====================

        public void Walk(XmSqlQuery query)
        {
            _text = query.Text;
            bool first = true;
            foreach (var stmt in query.Statements)
            {
                if (!first)
                    _sb.Append(_format ? "\r\n" : ", ");
                first = false;
                switch (stmt)
                {
                    case SetDirective set: WalkSetDirective(set); break;
                    case TempTableStatement temp: WalkTempTableStatement(temp); break;
                    case CreateShallowRelation relation: WalkCreateShallowRelation(relation); break;
                    case SelectQuery select: WalkSelectQuery(select); break;
                }
            }
        }

        private void WalkSetDirective(SetDirective context)
        {
            _sb.Append("SET ");
            Append(context.Name);
            _sb.Append("=");
            Append(context.Value);
            _sb.Append(";");
        }

        private void WalkTempTableStatement(TempTableStatement context)
        {
            _sb.Append(context.IsReducedBy ? "REDUCED BY " : "DEFINE TABLE ");
            AppendTableRef(context.Table);
            _sb.Append(" :=");
            if (_format)
                _sb.Append("\r\n");
            else
                _sb.Append(" ");
            WalkSelectBody(context.Body);
        }

        private void WalkCreateShallowRelation(CreateShallowRelation context)
        {
            _sb.Append("CREATE SHALLOW RELATION ");
            AppendTableRef(context.Name);

            foreach (var mod in context.Modifiers)
            {
                if (_format)
                {
                    _sb.Append("\r\n" + Indent);
                    _sb.Append(mod.ToString().ToUpperInvariant());
                }
                else
                {
                    _sb.Append(" ").Append(mod.ToString().ToUpperInvariant());
                }
            }

            if (_format)
            {
                _sb.Append("\r\n" + Indent + "FROM ");
                AppendTableColumnRef(context.From);
                _sb.Append("\r\n" + Indent + Indent + "TO ");
                AppendTableColumnRef(context.To);
            }
            else
            {
                _sb.Append(" FROM ");
                AppendTableColumnRef(context.From);
                _sb.Append(" TO ");
                AppendTableColumnRef(context.To);
            }
        }

        private void WalkSelectQuery(SelectQuery context)
        {
            if (context.With != null)
            {
                WalkWithClause(context.With);
                if (_format)
                    _sb.Append("\r\n");
                else
                    _sb.Append(" ");
            }
            WalkSelectBody(context.Body);
            if (context.Terminator == ';')
                _sb.Append(" ;");
            else if (context.Terminator == ',')
                _sb.Append(",");
        }

        private void WalkWithClause(IReadOnlyList<ExprDefinition> definitions)
        {
            _sb.Append("WITH ");
            bool first = true;
            foreach (var expr in definitions)
            {
                if (!first) _sb.Append(" ");
                first = false;
                Append(expr.Name);
                _sb.Append(" := ");
                if (expr.IsParenthesized) _sb.Append("(");
                AppendExpression(expr.Expression);
                if (expr.IsParenthesized) _sb.Append(")");
            }
        }

        private void WalkSelectBody(SelectBody context)
        {
            if (context == null) return;

            // SELECT
            _sb.Append("SELECT");
            var items = context.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) _sb.Append(",");
                if (_format)
                    _sb.Append("\r\n" + Indent);
                else
                    _sb.Append(" ");
                WalkSelectItem(items[i]);
            }

            // FROM
            if (_format)
                _sb.Append("\r\n");
            else
                _sb.Append(" ");
            _sb.Append("FROM ");
            AppendTableRef(context.From);

            // JOINs
            foreach (var join in context.Joins)
            {
                if (_format)
                    _sb.Append("\r\n" + Indent);
                else
                    _sb.Append(" ");
                WalkJoinClause(join);
            }

            // WHERE
            if (context.Where != null)
            {
                if (_format)
                    _sb.Append("\r\n");
                else
                    _sb.Append(" ");
                WalkWhereClause(context.Where);
            }
        }

        private void WalkSelectItem(SelectItem context)
        {
            if (context.Aggregation != null)
            {
                WalkAggregationExpr(context.Aggregation);
                AppendAlias(context.Alias);
                return;
            }

            if (context.Callback != null)
            {
                WalkCallbackExpr(context.Callback);
                AppendAlias(context.Alias);
                return;
            }

            if (context.Column != null)
            {
                AppendTableColumnRef(context.Column);
                if (context.CallbackDataId.IsPresent())
                {
                    _sb.Append(" ");
                    Append(context.CallbackDataId);
                }
                AppendAlias(context.Alias);
                return;
            }

            if (context.ExprAtRef.IsPresent())
            {
                Append(context.ExprAtRef);
                AppendAlias(context.Alias);
                return;
            }

            // catch-all expression
            if (context.Expression != null)
            {
                AppendExpression(context.Expression);
                AppendAlias(context.Alias);
            }
        }

        /// <summary>Emits an alias clause (AS [name] or AS 'name'). Skipped when simplifying.</summary>
        private void AppendAlias(Alias ctx)
        {
            if (ctx == null || _simplify) return;
            _sb.Append(" ");
            // ANTLR's GetText() concatenates the tokens without the white space between them
            Append(ctx.As);
            Append(ctx.Name);
        }

        private void WalkAggregationExpr(Aggregation context)
        {
            _sb.Append(context.Function.ToString().ToUpperInvariant());
            _sb.Append(" ( ");

            if (context.Expression != null)
            {
                AppendExpression(context.Expression);
            }

            _sb.Append(" )");
        }

        private void WalkCallbackExpr(CallbackExpr context)
        {
            switch (context.Kind)
            {
                case CallbackKind.EncodeCallback:
                case CallbackKind.CallbackDataId:
                    Append(context.Keyword);
                    _sb.Append(" ( ");
                    AppendTableColumnRef(context.Column);
                    _sb.Append(" )");
                    break;
                case CallbackKind.Callback:
                    Append(context.Keyword);
                    if (context.Expression != null)
                    {
                        _sb.Append(" ");
                        AppendExpression(context.Expression);
                    }
                    _sb.Append(" ");
                    AppendTableColumnRef(context.Column);
                    break;
            }
        }

        private void WalkJoinClause(JoinClause context)
        {
            if (context.Kind == JoinKind.ReverseBitmap)
            {
                _sb.Append("REVERSE BITMAP JOIN ");
                AppendTableRef(context.Table);
                if (_format)
                {
                    _sb.Append("\r\n" + Indent + Indent + "ON ");
                }
                else
                {
                    _sb.Append(" ON ");
                }
                AppendTableColumnRef(context.OnLeft);
                _sb.Append("=");
                AppendTableColumnRef(context.OnRight);
                return;
            }

            // LEFT OUTER JOIN or INNER JOIN
            if (context.Kind == JoinKind.LeftOuter)
                _sb.Append("LEFT OUTER JOIN ");
            else
                _sb.Append("INNER JOIN ");

            if (context.Table != null)
            {
                AppendTableRef(context.Table);
            }
            else
            {
                AppendTableColumnRef(context.TableColumn);
            }

            if (context.OnLeft != null)
            {
                if (_format)
                    _sb.Append("\r\n" + Indent + Indent + "ON ");
                else
                    _sb.Append(" ON ");
                AppendTableColumnRef(context.OnLeft);
                _sb.Append("=");
                AppendTableColumnRef(context.OnRight);
            }
        }

        private void WalkWhereClause(WhereClause context)
        {
            if (_format)
                _sb.Append("WHERE\r\n" + Indent);
            else
                _sb.Append("WHERE ");

            var predicates = context.Predicates;
            var logicalOps = context.Operators;

            for (int i = 0; i < predicates.Count; i++)
            {
                if (i > 0 && i - 1 < logicalOps.Count)
                {
                    _sb.Append(" ");
                    _sb.Append(logicalOps[i - 1]);
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

                // A predicate that emits nothing (an expression made of an AS type only, when simplifying)
                // falls back to its original text, like the ANTLR visitor
                int posBefore = _sb.Length;
                AppendFilterPredicate(predicates[i]);
                if (_sb.Length == posBefore)
                {
                    EmitOriginalText(predicates[i].Start, predicates[i].End);
                }
            }
        }

        private void AppendFilterPredicate(FilterPredicate ctx)
        {
            switch (ctx.Kind)
            {
                case FilterKind.Parenthesized:
                    _sb.Append("( ");
                    AppendFilterPredicate(ctx.Inner);
                    _sb.Append(" )");
                    return;

                case FilterKind.Coalesce:
                    AppendCoalesceFilter(ctx.Coalesce);
                    return;

                case FilterKind.Expression:
                    AppendExpression(ctx.Expression);
                    return;

                case FilterKind.Unparsed:
                    EmitOriginalText(ctx.Start, ctx.End);
                    return;
            }

            var tcRefs = ctx.Columns;

            // Determine if the first column is a date column (for value annotation)
            bool isDate = _convertDates && IsDateColumn(tcRefs[0]);

            // Tuple IN filter: (col1, col2) IN {(v1,v2), ...}
            if (ctx.Kind == FilterKind.TupleIn)
            {
                _sb.Append("( ");
                for (int i = 0; i < tcRefs.Count; i++)
                {
                    if (i > 0) _sb.Append(", ");
                    AppendTableColumnRef(tcRefs[i]);
                }
                _sb.Append(" ) IN { ");
                AppendTupleList(ctx.Tuples);
                _sb.Append(" }");
                return;
            }

            AppendTableColumnRef(tcRefs[0]);

            switch (ctx.Kind)
            {
                case FilterKind.Comparison:
                    _sb.Append(" ");
                    Append(ctx.Operator);
                    _sb.Append(" ");
                    AppendFilterValue(ctx.Value, isDate);
                    return;
                case FilterKind.In:
                    _sb.Append(" IN ( ");
                    AppendValueList(ctx.Values, isDate);
                    _sb.Append(" )");
                    return;
                case FilterKind.NotIn:
                    _sb.Append(" NIN ( ");
                    AppendValueList(ctx.Values, isDate);
                    _sb.Append(" )");
                    return;
                case FilterKind.Between:
                    _sb.Append(" BETWEEN ");
                    AppendFilterValue(ctx.Value, isDate);
                    _sb.Append(" AND ");
                    AppendFilterValue(ctx.Value2, isDate);
                    return;
                case FilterKind.InIndex:
                    _sb.Append(" ININDEX ");
                    AppendTableColumnRef(tcRefs[1]);
                    return;
                case FilterKind.CallbackDataId:
                    _sb.Append(" ");
                    Append(ctx.CallbackDataId);
                    return;
            }
        }

        private void AppendCoalesceFilter(CoalesceFilter ctx)
        {
            Append(ctx.Function);
            _sb.Append(" ( ");

            string tableName = CleanTableName(ctx.Table.Text);
            string rawColName = ctx.Column.ToString();
            string columnName = CleanColumnName(rawColName);

            _sb.Append("'").Append(tableName).Append("'");
            _sb.Append("[").Append(columnName).Append("]");

            // AS type
            if (ctx.CastType.IsPresent() && !_simplify)
            {
                _sb.Append(" AS ");
                Append(ctx.CastType);
            }

            _sb.Append(" ) ");
            Append(ctx.Operator);
            _sb.Append(" ");

            // Determine if the filtered column is a date column
            bool isDate = false;
            if (_convertDates)
            {
                var name = rawColName;
                if (name.Length >= 2 && name[0] == '[' && name[name.Length - 1] == ']')
                    name = name.Substring(1, name.Length - 2);
                isDate = IsDateColumnByName(name);
            }

            if (ctx.HasValueCoalesce)
            {
                _sb.Append("COALESCE ( ");
            }

            AppendFilterValue(ctx.Value, isDate);

            if (ctx.HasValueCoalesce)
            {
                _sb.Append(" )");
            }
        }

        private void AppendFilterValue(TextSpan value, bool isDateColumn = false)
        {
            if (value.IsMissing()) return;

            if (!_simplify && !(_convertDates && isDateColumn))
            {
                Append(value);
                return;
            }

            var text = value.ToString();

            if (_simplify)
            {
                text = RemoveGuids(text);
                text = PremiumTagsPattern.Replace(text, "");
            }

            if (_convertDates && isDateColumn)
            {
                text = XmSqlFormattingVisitor.TryConvertOADateToIso(text);
            }

            _sb.Append(text);
        }

        private void AppendTupleList(IReadOnlyList<ValueList> valueLists)
        {
            if (valueLists == null) return;
            for (int i = 0; i < valueLists.Count; i++)
            {
                if (i > 0) _sb.Append(", ");
                _sb.Append("( ");
                AppendValueList(valueLists[i]);
                _sb.Append(" )");
            }
        }

        private void AppendValueList(ValueList ctx, bool isDateColumn = false)
        {
            if (ctx == null) return;
            var values = ctx.Values;
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) _sb.Append(", ");
                AppendFilterValue(values[i], isDateColumn);
            }
            if (ctx.Truncation.IsPresent())
            {
                _sb.Append("..");
                var bracketedName = ctx.Truncation.ToString();
                if (bracketedName[0] != '[')
                {
                    // An estimated size the formatter already wrote
                    _sb.Append(bracketedName);
                }
                // Check if this is an estimated size annotation
                else if (!TryExtractEstimatedSize(bracketedName))
                {
                    // Format total values if present
                    var inner = CleanColumnName(bracketedName);
                    _sb.Append("[").Append(FormatTotalValues(inner)).Append("]");
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

        private void AppendExpression(Expression ctx)
        {
            if (ctx == null) return;
            var atoms = ctx.Atoms;
            var ops = ctx.Operators;
            for (int i = 0; i < atoms.Count; i++)
            {
                if (i > 0 && i - 1 < ops.Count)
                {
                    _sb.Append(" ");
                    Append(ops[i - 1]);
                    _sb.Append(" ");
                }
                AppendExpressionAtom(atoms[i]);
            }
        }

        private void AppendExpressionAtom(ExpressionAtom ctx)
        {
            if (ctx.Column != null) { AppendTableColumnRef(ctx.Column); return; }

            if (ctx.ExprAtRef.IsPresent()) { Append(ctx.ExprAtRef); return; }

            if (ctx.ExprRef.IsPresent()) { Append(ctx.ExprRef); return; }

            if (ctx.Function != null) { AppendFunctionCall(ctx.Function); return; }

            if (ctx.Parenthesized != null)
            {
                _sb.Append("( ");
                AppendExpression(ctx.Parenthesized);
                _sb.Append(" )");
                return;
            }

            if (ctx.Value.IsPresent()) { AppendFilterValue(ctx.Value); return; }

            // AS IDENTIFIER
            if (ctx.AsType.IsPresent())
            {
                if (!_simplify)
                {
                    _sb.Append("AS ");
                    Append(ctx.AsType);
                }
            }
        }

        private void AppendFunctionCall(FunctionCall ctx)
        {
            if (ctx.NameKind == FunctionNameKind.PfCast)
            {
                _sb.Append("PFCAST ( ");
                AppendExpression(ctx.Arguments[0]);
                if (!_simplify && ctx.CastType.IsPresent())
                {
                    _sb.Append(" AS ");
                    Append(ctx.CastType);
                }
                _sb.Append(" )");
                return;
            }

            switch (ctx.NameKind)
            {
                case FunctionNameKind.Quoted:
                    // Function name is a QUOTED_TABLE_NAME (e.g. 'LogAbsValueCallback') — strip quotes
                    _sb.Append(ctx.Name.ToString().Trim('\''));
                    break;
                case FunctionNameKind.Bracketed:
                    // BRACKETED_NAME — strip brackets, but preserve them around callbacks:
                    // the bracketed name can contain an entire DAX expression.
                    var name = ctx.Name;
                    var innerName = UnescapeBrackets(name.Buffer.Substring(name.Offset + 1, name.Length - 2));
                    bool isCallback = CallbackNamePattern.IsMatch(innerName);
                    if (isCallback) _sb.Append("[");
                    _sb.Append(innerName);
                    if (isCallback) _sb.Append("]");
                    break;
                case FunctionNameKind.InlineCallback:
                    Append(ctx.Name);
                    _sb.Append(" ");
                    Append(ctx.CallbackExpression);
                    break;
                default:
                    Append(ctx.Name);
                    break;
            }

            _sb.Append(" ( ");
            var exprs = ctx.Arguments;
            if (exprs != null)
            {
                for (int i = 0; i < exprs.Count; i++)
                {
                    if (i > 0) _sb.Append(", ");
                    AppendExpression(exprs[i]);
                }
            }
            _sb.Append(" )");
        }
    }
}
