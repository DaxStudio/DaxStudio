using Parlot;
using System;
using System.Collections.Generic;

namespace DaxStudio.Parsers.StorageEngine.XmSql
{
    // Syntax tree produced by XmSqlGrammar (Parlot). Each node mirrors a rule of Grammars/xmSQL.g4 and
    // exposes the same optional parts as the ANTLR rule context, so the tree walkers read like the
    // ANTLR visitors they replace. Tokens are TextSpans over the query text: nothing is copied while
    // parsing, and a missing token is a default TextSpan (see XmSqlText.IsMissing).

    /// <summary>A complete xmSQL query: <c>statement+ EOF</c>.</summary>
    internal sealed class XmSqlQuery
    {
        public XmSqlQuery(string text, IReadOnlyList<XmSqlStatement> statements, IReadOnlyList<ParseDiagnostic> diagnostics)
        {
            Text = text;
            Statements = statements;
            Diagnostics = diagnostics;
        }

        /// <summary>The parsed text.</summary>
        public string Text { get; }

        public IReadOnlyList<XmSqlStatement> Statements { get; }

        /// <summary>What was skipped to recover from syntax errors, in text order.</summary>
        public IReadOnlyList<ParseDiagnostic> Diagnostics { get; }

        /// <summary>True when parts of the text did not parse and were skipped, or there is no statement.</summary>
        public bool HasErrors => Diagnostics.Count > 0 || Statements.Count == 0;

        /// <summary>Offset of the first text that did not parse, or -1.</summary>
        public int ErrorOffset => Diagnostics.Count > 0 ? Diagnostics[0].Position.Offset : -1;
    }

    internal abstract class XmSqlStatement
    {
        /// <summary>Offset of the statement's first char in the query text.</summary>
        public int Start;

        /// <summary>Offset after the statement's last char in the query text.</summary>
        public int End;
    }

    /// <summary><c>SET IDENTIFIER = QUOTED_STRING ;</c></summary>
    internal sealed class SetDirective : XmSqlStatement
    {
        public TextSpan Name;
        public TextSpan Value;
    }

    /// <summary><c>DEFINE TABLE tableRef := selectBody</c> or <c>REDUCED BY tableRef := selectBody</c>.</summary>
    internal sealed class TempTableStatement : XmSqlStatement
    {
        public bool IsReducedBy;
        public TableRef Table;
        public SelectBody Body;
    }

    /// <summary><c>CREATE SHALLOW RELATION tableRef relationModifier* FROM tableColumnRef TO tableColumnRef</c></summary>
    internal sealed class CreateShallowRelation : XmSqlStatement
    {
        public TableRef Name;

        /// <summary>MANYTOMANY / BOTH, as written.</summary>
        public IReadOnlyList<TextSpan> Modifiers;
        public TableColumnRef From;
        public TableColumnRef To;
    }

    /// <summary><c>withClause? selectBody (COMMA | SEMICOLON)?</c></summary>
    internal sealed class SelectQuery : XmSqlStatement
    {
        /// <summary>The WITH definitions, or null without a WITH clause.</summary>
        public IReadOnlyList<ExprDefinition> With;
        public SelectBody Body;

        /// <summary>',' or ';' when the statement ends with one, otherwise '\0'.</summary>
        public char Terminator;
    }

    /// <summary><c>EXPR_REF := ( expression )</c></summary>
    internal sealed class ExprDefinition
    {
        public TextSpan Name;
        public Expression Expression;
    }

    /// <summary><c>SELECT selectList fromClause joinClause* whereClause?</c></summary>
    internal sealed class SelectBody
    {
        public IReadOnlyList<SelectItem> Items;
        public TableRef From;
        public IReadOnlyList<JoinClause> Joins;
        public WhereClause Where;
    }

    /// <summary>One item of a select list. Exactly one of Aggregation, Callback, Column, ExprAtRef or Expression is set.</summary>
    internal sealed class SelectItem
    {
        public Aggregation Aggregation;
        public CallbackExpr Callback;
        public TableColumnRef Column;

        /// <summary>The CALLBACKDATAID keyword that can follow <see cref="Column"/>.</summary>
        public TextSpan CallbackDataId;
        public TextSpan ExprAtRef;
        public Expression Expression;
        public Alias Alias;
    }

    /// <summary><c>AS (BRACKETED_NAME | QUOTED_TABLE_NAME)</c></summary>
    internal sealed class Alias
    {
        public TextSpan As;
        public TextSpan Name;
    }

    /// <summary><c>aggFunction ( expression? )</c></summary>
    internal sealed class Aggregation
    {
        /// <summary>The function keyword, as written.</summary>
        public TextSpan Function;

        /// <summary>The argument, or null for <c>COUNT ()</c>.</summary>
        public Expression Expression;
    }

    internal enum CallbackKind
    {
        EncodeCallback,
        CallbackDataId,
        Callback,
    }

    /// <summary><c>ENCODECALLBACK ( tableColumnRef )</c>, <c>CALLBACKDATAID ( tableColumnRef )</c> or <c>CALLBACK expression? tableColumnRef</c>.</summary>
    internal sealed class CallbackExpr
    {
        public CallbackKind Kind;
        public TextSpan Keyword;
        public Expression Expression;
        public TableColumnRef Column;
    }

    /// <summary><c>QUOTED_TABLE_NAME</c> or <c>BRACKETED_NAME</c>, with its quotes or brackets.</summary>
    internal sealed class TableRef
    {
        public TextSpan Text;
        public bool IsQuoted;
    }

    /// <summary><c>tableRef BRACKETED_NAME</c> or <c>tableRef . BRACKETED_NAME</c>.</summary>
    internal sealed class TableColumnRef
    {
        public TableRef Table;
        public bool HasDot;

        /// <summary>The column with its brackets.</summary>
        public TextSpan Column;
    }

    internal enum JoinKind
    {
        LeftOuter,
        Inner,
        ReverseBitmap,
    }

    /// <summary>
    /// <c>joinType tableRef onClause?</c>, <c>joinType tableColumnRef onClause?</c> or
    /// <c>REVERSE BITMAP JOIN tableRef ON tableColumnRef = tableColumnRef</c>.
    /// </summary>
    internal sealed class JoinClause
    {
        public JoinKind Kind;

        /// <summary>The joined table, null when the join names a column (<see cref="TableColumn"/>).</summary>
        public TableRef Table;
        public TableColumnRef TableColumn;

        /// <summary>The ON columns, null without an ON clause.</summary>
        public TableColumnRef OnLeft;
        public TableColumnRef OnRight;
    }

    /// <summary><c>WHERE filterPredicate (logicalOp filterPredicate)*</c></summary>
    internal sealed class WhereClause
    {
        public IReadOnlyList<FilterPredicate> Predicates;

        /// <summary>The operators between predicates: VAND, VOR or ",", upper-cased.</summary>
        public IReadOnlyList<string> Operators;
    }

    internal enum FilterKind
    {
        /// <summary><c>tableColumnRef comparisonOp filterValue</c></summary>
        Comparison,
        /// <summary><c>tableColumnRef IN ( valueList )</c></summary>
        In,
        /// <summary><c>tableColumnRef NIN ( valueList )</c></summary>
        NotIn,
        /// <summary><c>tableColumnRef BETWEEN filterValue AND filterValue</c></summary>
        Between,
        /// <summary><c>tableColumnRef ININDEX tableColumnRef</c></summary>
        InIndex,
        /// <summary><c>coalesceFilter</c></summary>
        Coalesce,
        /// <summary><c>tableColumnRef CALLBACKDATAID</c></summary>
        CallbackDataId,
        /// <summary><c>( tableColumnRef, ... ) IN { tupleList }</c></summary>
        TupleIn,
        /// <summary><c>( filterPredicate )</c></summary>
        Parenthesized,
        /// <summary><c>expression</c></summary>
        Expression,
        /// <summary>Text that is not a predicate, skipped up to the next VAND or VOR to recover.</summary>
        Unparsed,
    }

    internal sealed class FilterPredicate
    {
        public FilterKind Kind;

        /// <summary>The filtered columns: one, two for ININDEX, or the tuple's columns.</summary>
        public IReadOnlyList<TableColumnRef> Columns;

        /// <summary>The comparison operator, as written.</summary>
        public TextSpan Operator;

        /// <summary>The compared value, or the BETWEEN lower bound.</summary>
        public TextSpan Value;

        /// <summary>The BETWEEN upper bound.</summary>
        public TextSpan Value2;

        /// <summary>The CALLBACKDATAID keyword.</summary>
        public TextSpan CallbackDataId;
        public ValueList Values;
        public IReadOnlyList<ValueList> Tuples;
        public FilterPredicate Inner;
        public CoalesceFilter Coalesce;
        public Expression Expression;

        public int Start;
        public int End;
    }

    /// <summary>
    /// <c>(PFCASTCOALESCE | COALESCE) ( tableRef (BRACKETED_NAME | . BRACKETED_NAME) (AS IDENTIFIER)? ) comparisonOp (COALESCE ( )? filterValue )?</c>
    /// </summary>
    internal sealed class CoalesceFilter
    {
        /// <summary>PFCASTCOALESCE or COALESCE, as written.</summary>
        public TextSpan Function;
        public bool IsPfCastCoalesce;
        public TableRef Table;
        public bool HasDot;
        public TextSpan Column;

        /// <summary>The type after AS.</summary>
        public TextSpan CastType;
        public TextSpan Operator;

        /// <summary>True when the value is wrapped in <c>COALESCE ( )</c>.</summary>
        public bool HasValueCoalesce;
        public TextSpan Value;
    }

    /// <summary><c>filterValue (, filterValue)* truncationIndicator?</c></summary>
    internal sealed class ValueList
    {
        public IReadOnlyList<TextSpan> Values;

        /// <summary>The bracketed name of the truncation indicator (<c>..[N total values, not all displayed]</c>).</summary>
        public TextSpan Truncation;
    }

    /// <summary><c>expressionAtom (expressionOp expressionAtom)*</c></summary>
    internal sealed class Expression
    {
        public IReadOnlyList<ExpressionAtom> Atoms;

        /// <summary>The operators between atoms, as written.</summary>
        public IReadOnlyList<TextSpan> Operators;
    }

    /// <summary>One of the expressionAtom alternatives; exactly one member is set.</summary>
    internal sealed class ExpressionAtom
    {
        public TableColumnRef Column;
        public TextSpan ExprAtRef;
        public TextSpan ExprRef;
        public FunctionCall Function;
        public Expression Parenthesized;

        /// <summary>A filterValue: QUOTED_STRING, QUOTED_TABLE_NAME, NUMBER or IDENTIFIER.</summary>
        public TextSpan Value;

        /// <summary>The type of an <c>AS IDENTIFIER</c> atom.</summary>
        public TextSpan AsType;
    }

    internal enum FunctionNameKind
    {
        Identifier,
        Quoted,
        Bracketed,
        Coalesce,
        PfCastCoalesce,
        PfCast,
        /// <summary>
        /// A callback as the formatter writes it once simplified, <c>CallbackDataID ( DAX ) ( args )</c>:
        /// <see cref="FunctionCall.Name"/> is the keyword and <see cref="FunctionCall.CallbackExpression"/> the DAX.
        /// </summary>
        InlineCallback,
    }

    /// <summary>
    /// <c>(IDENTIFIER | QUOTED_TABLE_NAME | BRACKETED_NAME | COALESCE | PFCASTCOALESCE) ( expressionList? )</c>
    /// or <c>PFCAST ( expression AS IDENTIFIER )</c>.
    /// </summary>
    internal sealed class FunctionCall
    {
        public FunctionNameKind NameKind;

        /// <summary>The name, as written (with quotes or brackets).</summary>
        public TextSpan Name;

        /// <summary>The arguments, null when the parentheses are empty. PFCAST has one argument.</summary>
        public IReadOnlyList<Expression> Arguments;

        /// <summary>The PFCAST target type.</summary>
        public TextSpan CastType;

        /// <summary>The parenthesized DAX expression of an <see cref="FunctionNameKind.InlineCallback"/>.</summary>
        public TextSpan CallbackExpression;
    }

    internal static class XmSqlText
    {
        public static bool IsMissing(this TextSpan span) => span.Buffer == null;

        public static bool IsPresent(this TextSpan span) => span.Buffer != null;

        /// <summary>Returns the text, or null for a missing token (like ANTLR's <c>node?.GetText()</c>).</summary>
        public static string GetText(this TextSpan span) => span.Buffer == null ? null : span.ToString();

        public static bool EqualsIgnoreCase(this TextSpan span, string text)
            => span.Buffer != null && span.Span.Equals(text.AsSpan(), StringComparison.OrdinalIgnoreCase);
    }
}
