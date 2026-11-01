using Parlot;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using static Parlot.Fluent.Parsers;

namespace DaxStudio.Parsers.XmSql
{
    /// <summary>
    /// Parlot grammar for xmSQL, equivalent to Grammars/xmSQL.g4.
    /// </summary>
    /// <remarks>
    /// The rules and their alternatives follow the ANTLR grammar in the same order. ANTLR chooses between
    /// alternatives with unbounded lookahead while a PEG takes the first one that matches, so where an earlier
    /// alternative matches a prefix of a later one (a column vs. an expression starting with that column) the
    /// earlier one is only taken when the text does not continue as the later one.
    /// The parser is thread safe: it is built once and keeps no state between parses.
    /// </remarks>
    internal static class XmSqlGrammar
    {
        private static readonly Parser<IReadOnlyList<XmSqlStatement>> Query;

        /// <summary>Stands for a skipped token in the statement list, until the list is cleaned up.</summary>
        private static readonly SkippedText Skipped = new SkippedText();

        private sealed class SkippedText : XmSqlStatement
        {
        }

        private static readonly IReadOnlyList<TextSpan> NoSpans = Array.Empty<TextSpan>();
        private static readonly IReadOnlyList<JoinClause> NoJoins = Array.Empty<JoinClause>();

        static XmSqlGrammar()
        {
            // ==================== TOKENS ====================

            var bracketedName = Terms.Scan(XmSqlLexer.BracketedName, "[".AsSpan());
            var quotedTableName = Terms.Scan(XmSqlLexer.QuotedTableName, "'".AsSpan());
            var quotedString = Terms.Scan(XmSqlLexer.QuotedString, "\"".AsSpan());
            var number = Terms.Scan(XmSqlLexer.Number, "-0123456789".AsSpan());
            var identifier = Terms.Scan(XmSqlLexer.Identifier, XmSqlLexer.IdentifierStart.AsSpan());
            var exprRef = Terms.Scan(XmSqlLexer.ExprRef, "$".AsSpan());
            var exprAtRef = Terms.Scan(XmSqlLexer.ExprAtRef, "@".AsSpan());

            var lParen = Terms.Char('(');
            var rParen = Terms.Char(')');
            var lBrace = Terms.Char('{');
            var rBrace = Terms.Char('}');
            var comma = Terms.Char(',');
            var semicolon = Terms.Char(';');
            var equals = Terms.Char('=');
            var assign = Terms.Text(":=");
            var dotDot = Terms.Text("..");
            var dot = Terms.Char('.').WhenNotFollowedBy(Literals.Char('.'));

            var comparisonOperator = Literals.Text("<>").Or(Literals.Text("!=")).Or(Literals.Text(">=")).Or(Literals.Text("<="))
                .Or(Literals.Text("=")).Or(Literals.Text(">")).Or(Literals.Text("<"));
            var comparisonOp = SkipWhiteSpace(Capture(comparisonOperator));

            // '-' followed by a digit is a negative NUMBER for the lexer, never a MINUS.
            var minus = Literals.Char('-').WhenNotFollowedBy(Literals.AnyOf("0123456789", maxSize: 1));
            var expressionOp = SkipWhiteSpace(Capture(Literals.Char('+').Or(minus).Or(Literals.Char('*')).Or(Literals.Char('/'))
                .Then(_ => "").Or(comparisonOperator)));

            // ==================== RULES ====================

            var tableRef = quotedTableName.Then(t => new TableRef { Text = t, IsQuoted = true })
                .Or(bracketedName.Then(t => new TableRef { Text = t }));

            // tableRef BRACKETED_NAME | tableRef DOT BRACKETED_NAME
            var tableColumnRef = tableRef.And(bracketedName.Then(c => (false, c)).Or(dot.SkipAnd(bracketedName).Then(c => (true, c))))
                .Then(x => new TableColumnRef { Table = x.Item1, HasDot = x.Item2.Item1, Column = x.Item2.Item2 });

            var filterValue = quotedString.Or(quotedTableName).Or(number).Or(identifier);

            var expression = Deferred<Expression>();

            var functionName = identifier.Then(n => (FunctionNameKind.Identifier, n))
                .Or(quotedTableName.Then(n => (FunctionNameKind.Quoted, n)))
                .Or(bracketedName.Then(n => (FunctionNameKind.Bracketed, n)))
                .Or(Keyword("COALESCE").Then(n => (FunctionNameKind.Coalesce, n)))
                .Or(Keyword("PFCASTCOALESCE").Then(n => (FunctionNameKind.PfCastCoalesce, n)));

            var functionCall =
                // The formatter drops "AS type" when it simplifies; accept its output too.
                Keyword("PFCAST").AndSkip(lParen).And(expression).And(Kw("AS").SkipAnd(identifier).Optional()).AndSkip(rParen)
                    .Then(x => new FunctionCall { NameKind = FunctionNameKind.PfCast, Name = x.Item1, Arguments = new[] { x.Item2 }, CastType = x.Item3.HasValue ? x.Item3.Value : default })
                .Or(Keyword("CALLBACKDATAID").Or(Keyword("ENCODECALLBACK")).And(Terms.Scan(XmSqlLexer.ParenthesizedText, "(".AsSpan()))
                    .AndSkip(lParen).And(Separated(comma, expression).Optional()).AndSkip(rParen)
                    .Then(x => new FunctionCall { NameKind = FunctionNameKind.InlineCallback, Name = x.Item1, CallbackExpression = x.Item2, Arguments = x.Item3.HasValue ? x.Item3.Value : null }))
                .Or(functionName.AndSkip(lParen).And(Separated(comma, expression).Optional()).AndSkip(rParen)
                    .Then(x => new FunctionCall { NameKind = x.Item1.Item1, Name = x.Item1.Item2, Arguments = x.Item2.HasValue ? x.Item2.Value : null }));

            var expressionAtom = tableColumnRef.Then(c => new ExpressionAtom { Column = c })
                .Or(exprAtRef.Then(t => new ExpressionAtom { ExprAtRef = t }))
                .Or(exprRef.Then(t => new ExpressionAtom { ExprRef = t }))
                .Or(functionCall.Then(f => new ExpressionAtom { Function = f }))
                .Or(Between(lParen, expression, rParen).Then(e => new ExpressionAtom { Parenthesized = e }))
                .Or(filterValue.Then(v => new ExpressionAtom { Value = v }))
                .Or(Kw("AS").SkipAnd(identifier).Then(t => new ExpressionAtom { AsType = t }));

            expression.Parser = expressionAtom.And(ZeroOrMany(expressionOp.And(expressionAtom)))
                .Then(BuildExpression);

            // An earlier alternative is not taken when an expression would continue after it.
            Parser<T> NotContinuedAsExpression<T>(Parser<T> parser) => parser.When((context, _) => !ContinuesAsExpression(context));

            // ---------- select list ----------

            var alias = Keyword("AS").And(bracketedName.Or(quotedTableName))
                .Then(x => new Alias { As = x.Item1, Name = x.Item2 });

            var aggFunction = Keyword("SUM").Or(Keyword("COUNT")).Or(Keyword("DCOUNT")).Or(Keyword("MIN")).Or(Keyword("MAX"))
                .Or(Keyword("AVG")).Or(Keyword("SUMSQR")).Or(Keyword("COUNT_BIG"));

            var aggregationExpr = aggFunction.AndSkip(lParen).And(expression.Optional()).AndSkip(rParen)
                .Then(x => new Aggregation { Function = x.Item1, Expression = x.Item2.HasValue ? x.Item2.Value : null });

            var callbackExpr =
                Keyword("ENCODECALLBACK").AndSkip(lParen).And(tableColumnRef).AndSkip(rParen)
                    .Then(x => new CallbackExpr { Kind = CallbackKind.EncodeCallback, Keyword = x.Item1, Column = x.Item2 })
                .Or(Keyword("CALLBACKDATAID").AndSkip(lParen).And(tableColumnRef).AndSkip(rParen)
                    .Then(x => new CallbackExpr { Kind = CallbackKind.CallbackDataId, Keyword = x.Item1, Column = x.Item2 }))
                .Or(Keyword("CALLBACK").And(expression).And(tableColumnRef)
                    .Then(x => new CallbackExpr { Kind = CallbackKind.Callback, Keyword = x.Item1, Expression = x.Item2, Column = x.Item3 }))
                .Or(Keyword("CALLBACK").And(tableColumnRef)
                    .Then(x => new CallbackExpr { Kind = CallbackKind.Callback, Keyword = x.Item1, Column = x.Item2 }));

            var optionalAlias = alias.Optional();

            var selectItem =
                aggregationExpr.And(optionalAlias).Then(x => new SelectItem { Aggregation = x.Item1, Alias = OrNull(x.Item2) })
                .Or(callbackExpr.And(optionalAlias).Then(x => new SelectItem { Callback = x.Item1, Alias = OrNull(x.Item2) }))
                .Or(NotContinuedAsExpression(tableColumnRef.And(Keyword("CALLBACKDATAID").Optional()).And(optionalAlias))
                    .Then(x => new SelectItem { Column = x.Item1, CallbackDataId = x.Item2.HasValue ? x.Item2.Value : default, Alias = OrNull(x.Item3) }))
                .Or(exprAtRef.WhenNotFollowedBy(Kw("AS")).When((context, _) => !ContinuesAsExpression(context))
                    .Then(t => new SelectItem { ExprAtRef = t }))
                .Or(expression.And(optionalAlias).Then(x => new SelectItem { Expression = x.Item1, Alias = OrNull(x.Item2) }));

            // ---------- FROM / JOIN ----------

            var onColumns = tableColumnRef.AndSkip(equals).And(tableColumnRef);

            var joinType = Kw("LEFT").SkipAnd(Kw("OUTER")).SkipAnd(Kw("JOIN")).Then(JoinKind.LeftOuter)
                .Or(Kw("INNER").SkipAnd(Kw("JOIN")).Then(JoinKind.Inner));

            var joinClause =
                Kw("REVERSE").SkipAnd(Kw("BITMAP")).SkipAnd(Kw("JOIN")).SkipAnd(tableRef).AndSkip(Kw("ON")).And(onColumns)
                    .Then(x => new JoinClause { Kind = JoinKind.ReverseBitmap, Table = x.Item1, OnLeft = x.Item2.Item1, OnRight = x.Item2.Item2 })
                .Or(joinType.And(tableColumnRef.Then(c => (default(TableRef), c)).Or(tableRef.Then(t => (t, default(TableColumnRef)))))
                    .And(Kw("ON").SkipAnd(onColumns).Optional())
                    .Then(x => new JoinClause
                    {
                        Kind = x.Item1,
                        Table = x.Item2.Item1,
                        TableColumn = x.Item2.Item2,
                        OnLeft = x.Item3.HasValue ? x.Item3.Value.Item1 : null,
                        OnRight = x.Item3.HasValue ? x.Item3.Value.Item2 : null,
                    }));

            // ---------- WHERE ----------

            // The formatter writes the estimated size of a truncated list without brackets: '..Estimated size: rows = 1  bytes = 2 )'
            var truncationIndicator = dotDot.SkipAnd(dot.Optional()).SkipAnd(bracketedName.Or(Terms.Scan(XmSqlLexer.FormattedEstimatedSize, "E".AsSpan())));

            var valueList = Separated(comma, filterValue).And(truncationIndicator.Optional())
                .Then(x => new ValueList { Values = x.Item1, Truncation = x.Item2.HasValue ? x.Item2.Value : default });

            var tupleList = Separated(comma, Between(lParen, valueList, rParen));

            var filterPredicate = Deferred<FilterPredicate>();

            // The alternatives that start with a column, after the column.
            var columnFilterTail =
                NotContinuedAsComparisonValue(comparisonOp.And(filterValue))
                    .Then(x => new FilterPredicate { Kind = FilterKind.Comparison, Operator = x.Item1, Value = x.Item2 })
                .Or(Kw("IN").SkipAnd(lParen).SkipAnd(valueList).AndSkip(rParen)
                    .Then(v => new FilterPredicate { Kind = FilterKind.In, Values = v }))
                .Or(Kw("NIN").SkipAnd(lParen).SkipAnd(valueList).AndSkip(rParen)
                    .Then(v => new FilterPredicate { Kind = FilterKind.NotIn, Values = v }))
                .Or(Kw("BETWEEN").SkipAnd(filterValue).AndSkip(Kw("AND")).And(filterValue)
                    .Then(x => new FilterPredicate { Kind = FilterKind.Between, Value = x.Item1, Value2 = x.Item2 }))
                .Or(Kw("ININDEX").SkipAnd(tableColumnRef)
                    .Then(c => new FilterPredicate { Kind = FilterKind.InIndex, Columns = new[] { null, c } }))
                .Or(Keyword("CALLBACKDATAID")
                    .Then(t => new FilterPredicate { Kind = FilterKind.CallbackDataId, CallbackDataId = t }));

            var columnFilter = tableColumnRef.And(columnFilterTail).Then(x =>
            {
                var predicate = x.Item2;
                if (predicate.Kind == FilterKind.InIndex)
                {
                    ((TableColumnRef[])predicate.Columns)[0] = x.Item1;
                }
                else
                {
                    predicate.Columns = new[] { x.Item1 };
                }

                return predicate;
            });

            var coalesceValue = Kw("COALESCE").SkipAnd(lParen).SkipAnd(filterValue).AndSkip(rParen).Then(v => (true, v))
                .Or(filterValue.Then(v => (false, v)));

            var coalesceFilter =
                Keyword("PFCASTCOALESCE").Then(t => (true, t)).Or(Keyword("COALESCE").Then(t => (false, t)))
                .AndSkip(lParen)
                .And(tableRef)
                .And(bracketedName.Then(c => (false, c)).Or(dot.SkipAnd(bracketedName).Then(c => (true, c))))
                .And(Kw("AS").SkipAnd(identifier).Optional())
                .AndSkip(rParen)
                .And(comparisonOp)
                .And(coalesceValue)
                .Then(x => new CoalesceFilter
                {
                    IsPfCastCoalesce = x.Item1.Item1,
                    Function = x.Item1.Item2,
                    Table = x.Item2,
                    HasDot = x.Item3.Item1,
                    Column = x.Item3.Item2,
                    CastType = x.Item4.HasValue ? x.Item4.Value : default,
                    Operator = x.Item5,
                    HasValueCoalesce = x.Item6.Item1,
                    Value = x.Item6.Item2,
                });

            var tupleFilter = lParen.SkipAnd(Separated(comma, tableColumnRef)).AndSkip(rParen).AndSkip(Kw("IN"))
                .AndSkip(lBrace).And(tupleList).AndSkip(rBrace)
                .Then(x => new FilterPredicate { Kind = FilterKind.TupleIn, Columns = x.Item1, Tuples = x.Item2 });

            filterPredicate.Parser =
                columnFilter
                .Or(NotContinuedAsExpression(coalesceFilter).Then(c => new FilterPredicate { Kind = FilterKind.Coalesce, Coalesce = c }))
                .Or(tupleFilter)
                .Or(NotContinuedAsExpression(Between(lParen, filterPredicate, rParen)).Then(p => new FilterPredicate { Kind = FilterKind.Parenthesized, Inner = p }))
                .Or(expression.Then(e => new FilterPredicate { Kind = FilterKind.Expression, Expression = e }))
                // Recovers within the WHERE clause, so that the other predicates of the statement are kept
                .Or(Terms.Scan(XmSqlLexer.UnparsedPredicate).Warning("Skipped a filter predicate that does not parse")
                    .Then(_ => new FilterPredicate { Kind = FilterKind.Unparsed }))
                .Then((context, start, end, p) =>
                {
                    p.Start = start;
                    p.End = end;
                    return p;
                });

            var logicalOp = Kw("VAND").Or(Kw("VOR")).Or(comma.Then(","));

            var whereClause = Kw("WHERE").SkipAnd(filterPredicate).And(ZeroOrMany(logicalOp.And(filterPredicate)))
                .Then(x => BuildWhere(x.Item1, x.Item2));

            // ---------- statements ----------

            var selectBody = Kw("SELECT").SkipAnd(Separated(comma, selectItem))
                .AndSkip(Kw("FROM")).And(tableRef)
                .And(ZeroOrMany(joinClause))
                .And(whereClause.Optional())
                .Then(x => new SelectBody
                {
                    Items = x.Item1,
                    From = x.Item2,
                    Joins = x.Item3.Count == 0 ? NoJoins : x.Item3,
                    Where = x.Item4.HasValue ? x.Item4.Value : null,
                });

            var terminator = comma.Or(semicolon).Optional();

            var setDirective = Kw("SET").SkipAnd(identifier).AndSkip(equals).And(quotedString).AndSkip(semicolon)
                .Then(x => (XmSqlStatement)new SetDirective { Name = x.Item1, Value = x.Item2 });

            var defineTable = Kw("DEFINE").SkipAnd(Kw("TABLE")).SkipAnd(tableRef).AndSkip(assign).And(selectBody).AndSkip(terminator)
                .Then(x => (XmSqlStatement)new TempTableStatement { Table = x.Item1, Body = x.Item2 });

            var reducedBy = Kw("REDUCED").SkipAnd(Kw("BY")).SkipAnd(tableRef).AndSkip(assign).And(selectBody).AndSkip(terminator)
                .Then(x => (XmSqlStatement)new TempTableStatement { IsReducedBy = true, Table = x.Item1, Body = x.Item2 });

            var relationModifier = Keyword("MANYTOMANY").Or(Keyword("BOTH"));

            var createShallowRelation = Kw("CREATE").SkipAnd(Kw("SHALLOW")).SkipAnd(Kw("RELATION")).SkipAnd(tableRef)
                .And(ZeroOrMany(relationModifier))
                .AndSkip(Kw("FROM")).And(tableColumnRef)
                .AndSkip(Kw("TO")).And(tableColumnRef)
                .AndSkip(terminator)
                .Then(x => (XmSqlStatement)new CreateShallowRelation
                {
                    Name = x.Item1,
                    Modifiers = x.Item2.Count == 0 ? NoSpans : x.Item2,
                    From = x.Item3,
                    To = x.Item4,
                });

            var exprDefinition = exprRef.AndSkip(assign).AndSkip(lParen).And(expression).AndSkip(rParen)
                .Then(x => new ExprDefinition { Name = x.Item1, Expression = x.Item2 });

            var withClause = Kw("WITH").SkipAnd(OneOrMany(exprDefinition));

            var selectQuery = withClause.Optional().And(selectBody).And(comma.Or(semicolon).Optional())
                .Then(x => (XmSqlStatement)new SelectQuery
                {
                    With = x.Item1.HasValue ? x.Item1.Value : null,
                    Body = x.Item2,
                    Terminator = x.Item3.HasValue ? x.Item3.Value : '\0',
                });

            var statement = defineTable.Or(createShallowRelation).Or(reducedBy).Or(selectQuery).Or(setDirective)
                .Then((context, start, end, s) =>
                {
                    s.Start = start;
                    s.End = end;
                    return s;
                });

            // statement+ EOF, recovering like ANTLR: a token that doesn't start a statement is skipped and reported.
            // The estimated size footer of the trace or of the formatter, and the ", " the formatter writes between
            // statements when it doesn't break lines, are not errors.
            var estimatedSizeFooter = Terms.Scan(XmSqlLexer.EstimatedSizeFooter, "E[".AsSpan()).Then(default(XmSqlStatement));
            var separator = comma.Then(default(XmSqlStatement));
            var skippedToken = Terms.Scan(XmSqlLexer.AnyToken).Warning("Skipped text that does not start a statement")
                .Then((XmSqlStatement)Skipped);

            Query = ZeroOrMany(statement.Or(estimatedSizeFooter).Or(separator).Or(skippedToken));
        }

        /// <summary>A case-insensitive keyword, returning its canonical upper-case text.</summary>
        private static Parser<string> Kw(string keyword)
            => Terms.Keyword(keyword, XmSqlLexer.IdentifierPart, caseInsensitive: true);

        /// <summary>A case-insensitive keyword, returning the text as written.</summary>
        private static Parser<TextSpan> Keyword(string keyword)
            => SkipWhiteSpace(Capture(Literals.Keyword(keyword, XmSqlLexer.IdentifierPart, caseInsensitive: true)));

        private static Alias OrNull(Option<Alias> alias) => alias.HasValue ? alias.Value : null;

        private static Expression BuildExpression((ExpressionAtom, IReadOnlyList<(TextSpan, ExpressionAtom)>) x)
        {
            var rest = x.Item2;
            if (rest.Count == 0)
            {
                return new Expression { Atoms = new[] { x.Item1 }, Operators = NoSpans };
            }

            var atoms = new ExpressionAtom[rest.Count + 1];
            var operators = new TextSpan[rest.Count];
            atoms[0] = x.Item1;
            for (var i = 0; i < rest.Count; i++)
            {
                operators[i] = rest[i].Item1;
                atoms[i + 1] = rest[i].Item2;
            }

            return new Expression { Atoms = atoms, Operators = operators };
        }

        private static WhereClause BuildWhere(FilterPredicate first, IReadOnlyList<(string, FilterPredicate)> rest)
        {
            var predicates = new FilterPredicate[rest.Count + 1];
            var operators = new string[rest.Count];
            predicates[0] = first;
            for (var i = 0; i < rest.Count; i++)
            {
                operators[i] = rest[i].Item1;
                predicates[i + 1] = rest[i].Item2;
            }

            return new WhereClause { Predicates = predicates, Operators = operators };
        }

        /// <summary>
        /// A comparison value that is followed by text making it part of a longer expression atom
        /// (<c>'T'[C] = 'U'[D]</c>, <c>'T'[C] = F(1)</c>) or of an expression is not a comparison.
        /// </summary>
        private static Parser<(TextSpan, TextSpan)> NotContinuedAsComparisonValue(Parser<(TextSpan, TextSpan)> parser)
            => parser.When((context, x) =>
            {
                var next = PeekNonWhiteSpace(context, out var nextNext);
                var value = x.Item2.Span;
                if (value.Length > 0 && (value[0] == '\'' || XmSqlLexer.Identifier(value) == value.Length) && next == '(') return false;
                if (value.Length > 0 && value[0] == '\'' && (next == '[' || next == '.' && nextNext != '.')) return false;
                return !IsExpressionOperator(next, nextNext);
            });

        /// <summary>True when the text after the cursor starts with an expressionOp.</summary>
        private static bool ContinuesAsExpression(ParseContext context)
        {
            var next = PeekNonWhiteSpace(context, out var nextNext);
            return IsExpressionOperator(next, nextNext);
        }

        private static bool IsExpressionOperator(char c, char next)
        {
            switch (c)
            {
                case '+':
                case '*':
                case '/':
                case '=':
                case '<':
                case '>':
                    return true;
                case '-':
                    return next < '0' || next > '9';
                case '!':
                    return next == '=';
                default:
                    return false;
            }
        }

        private static char PeekNonWhiteSpace(ParseContext context, out char next)
        {
            var span = context.Scanner.Cursor.Span;
            var i = 0;
            while (i < span.Length && char.IsWhiteSpace(span[i])) i++;
            next = i + 1 < span.Length ? span[i + 1] : '\0';
            return i < span.Length ? span[i] : '\0';
        }

        /// <summary>
        /// Parses an xmSQL query. Like the ANTLR parser with its default error strategy, text that is not
        /// a statement is skipped one token at a time and the statements around it are kept; the result's
        /// <see cref="XmSqlQuery.Diagnostics"/> tell what was skipped.
        /// </summary>
        public static XmSqlQuery Parse(string text)
        {
            // Collecting diagnostics has a cost in every choice, while they are only needed when text was skipped.
            // Valid queries are parsed once without them, the others are parsed again to report what was skipped.
            var statements = Parse(text, collectDiagnostics: false, out var cleanup, out var diagnostics);
            if (cleanup != Cleanup.Skipped)
            {
                return new XmSqlQuery(text, cleanup == Cleanup.None ? statements : WithoutSeparators(statements), diagnostics);
            }

            statements = Parse(text, collectDiagnostics: true, out _, out diagnostics);
            return new XmSqlQuery(text, WithoutSeparators(statements), diagnostics);
        }

        private enum Cleanup
        {
            None,
            Separators,
            Skipped,
        }

        private static IReadOnlyList<XmSqlStatement> Parse(string text, bool collectDiagnostics, out Cleanup cleanup, out IReadOnlyList<ParseDiagnostic> diagnostics)
        {
            var context = new ParseContext(new Scanner(text), disableLoopDetection: true) { CollectDiagnostics = collectDiagnostics };

            // Formatted queries carry "/* 2024-01-31 */" comments after dates, which xmSQL doesn't have.
            // Skipping them as white space only costs when there are some.
            if (text.IndexOf("/*", StringComparison.Ordinal) >= 0)
            {
                context.WhiteSpaceParser = WhiteSpaceAndComments;
            }

            var result = new ParseResult<IReadOnlyList<XmSqlStatement>>();
            Query.Parse(context, ref result);

            cleanup = Cleanup.None;
            foreach (var statement in result.Value)
            {
                if (statement == Skipped || HasUnparsedPredicate(statement))
                {
                    cleanup = Cleanup.Skipped;
                    break;
                }

                if (statement == null) cleanup = Cleanup.Separators;
            }

            diagnostics = collectDiagnostics ? context.Diagnostics : Array.Empty<ParseDiagnostic>();
            return result.Value;
        }

        private static bool HasUnparsedPredicate(XmSqlStatement statement)
        {
            SelectBody body;
            switch (statement)
            {
                case SelectQuery select: body = select.Body; break;
                case TempTableStatement temp: body = temp.Body; break;
                default: return false;
            }

            if (body.Where == null) return false;
            foreach (var predicate in body.Where.Predicates)
            {
                var p = predicate;
                while (p.Kind == FilterKind.Parenthesized) p = p.Inner;
                if (p.Kind == FilterKind.Unparsed) return true;
            }

            return false;
        }

        private static IReadOnlyList<XmSqlStatement> WithoutSeparators(IReadOnlyList<XmSqlStatement> statements)
        {
            var list = new List<XmSqlStatement>(statements.Count);
            foreach (var statement in statements)
            {
                if (statement != null && statement != Skipped) list.Add(statement);
            }

            return list;
        }

        private static readonly Parser<TextSpan> WhiteSpaceAndComments = Literals.Scan(XmSqlLexer.WhiteSpaceAndComments);
    }
}
