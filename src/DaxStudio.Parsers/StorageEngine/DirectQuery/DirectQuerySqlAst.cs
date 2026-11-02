using Parlot;
using System.Collections.Generic;

namespace DaxStudio.Parsers.StorageEngine.DirectQuery
{
    // Syntax tree produced by DirectQuerySqlGrammar (Parlot). Each node mirrors a rule of
    // Grammars/DirectQuerySql.g4 and exposes the parts of the ANTLR rule context that the analysis reads,
    // so DirectQuerySqlAnalysisWalker reads like the ANTLR visitor it replaces. Tokens are TextSpans over
    // the query text, with their brackets or quotes; a missing token is a default TextSpan
    // (see XmSql.XmSqlText.IsMissing).

    /// <summary>
    /// <c>SELECT selectList fromClause joinClause* whereClause? groupByClause? havingClause? orderByClause?</c>
    /// </summary>
    internal sealed class DqSelectStatement
    {
        public IReadOnlyList<DqSelectItem> SelectList;

        /// <summary>The FROM sources (<c>tableSource</c>), in order.</summary>
        public IReadOnlyList<DqSubselectBlock> From;
        public IReadOnlyList<DqJoinClause> Joins;

        /// <summary>The WHERE expression, or null.</summary>
        public DqExpression Where;

        /// <summary>The GROUP BY expressions, or null without a GROUP BY clause.</summary>
        public IReadOnlyList<DqExpression> GroupBy;

        /// <summary>The HAVING expression, or null.</summary>
        public DqExpression Having;

        /// <summary>The ORDER BY items, or null without an ORDER BY clause.</summary>
        public IReadOnlyList<DqOrderByItem> OrderBy;
    }

    /// <summary><c>expression (AS bracketedName)?</c></summary>
    internal sealed class DqSelectItem
    {
        public DqExpression Expression;
        public TextSpan Alias;
    }

    /// <summary>
    /// <c>( (? SELECT subselectColumnList FROM schemaTable AS [$Table] )? ) AS bracketedName</c>, or a
    /// <c>tableReference</c>. Either <see cref="SchemaTable"/> or <see cref="TableReference"/> is set.
    /// </summary>
    /// <remarks>
    /// <c>tableSource</c> and <c>tableJoinSource</c> list a plain <c>tableReference</c> as a second alternative,
    /// which ANTLR never takes since <c>subselectBlock</c> includes it and comes first.
    /// </remarks>
    internal sealed class DqSubselectBlock
    {
        public DqSchemaTable SchemaTable;
        public IReadOnlyList<DqSubselectColumn> Columns;
        public TextSpan Alias;
        public DqTableReference TableReference;
    }

    /// <summary><c>[$Table] . bracketedName AS bracketedName</c></summary>
    internal sealed class DqSubselectColumn
    {
        public TextSpan Source;
        public TextSpan Alias;
    }

    /// <summary><c>(? schemaTable )? (AS bracketedName)?</c></summary>
    internal sealed class DqTableReference
    {
        public DqSchemaTable SchemaTable;
        public TextSpan Alias;
    }

    /// <summary><c>bracketedName . bracketedName</c></summary>
    internal sealed class DqSchemaTable
    {
        public TextSpan Schema;
        public TextSpan Table;
    }

    internal enum DqJoinType
    {
        Inner,
        Left,
        Right,
        Full,
        Cross,
    }

    /// <summary><c>joinType tableJoinSource onJoinClause?</c></summary>
    internal sealed class DqJoinClause
    {
        public DqJoinType Type;
        public DqSubselectBlock Source;

        /// <summary>The ON conditions, or null without an ON clause.</summary>
        public IReadOnlyList<DqJoinCondition> On;
    }

    /// <summary><c>expression = expression</c></summary>
    internal sealed class DqJoinCondition
    {
        public DqExpression Left;
        public DqExpression Right;
    }

    /// <summary><c>expression (ASC | DESC)?</c></summary>
    internal sealed class DqOrderByItem
    {
        public DqExpression Expression;
        public TextSpan Direction;
    }

    internal enum DqExpressionKind
    {
        /// <summary><c>unaryExpression</c></summary>
        Unary,

        /// <summary><c>expression op expression</c></summary>
        Binary,

        /// <summary><c>expression IS NOT? NULL</c></summary>
        IsNull,

        /// <summary><c>( expression )</c></summary>
        Parenthesized,

        /// <summary><c>NOT expression</c></summary>
        Not,
    }

    internal enum DqOperatorKind
    {
        None,

        /// <summary><c>* / %</c>, precedence 8.</summary>
        Multiplicative,

        /// <summary><c>+ -</c>, precedence 7.</summary>
        Additive,

        /// <summary><c>comparisonOp</c>, precedence 6.</summary>
        Comparison,

        /// <summary><c>AND</c>, precedence 5.</summary>
        And,

        /// <summary><c>OR</c>, precedence 4.</summary>
        Or,
    }

    /// <summary>The <c>expression</c> rule, one node per alternative.</summary>
    internal sealed class DqExpression
    {
        public DqExpressionKind Kind;

        /// <summary>Set for <see cref="DqExpressionKind.Unary"/>.</summary>
        public DqUnaryExpression Unary;

        /// <summary>The left operand of a binary expression, or the operand of the other kinds.</summary>
        public DqExpression Left;

        /// <summary>The right operand of a binary expression.</summary>
        public DqExpression Right;

        public DqOperatorKind Operator;

        /// <summary>The operator token of a binary expression.</summary>
        public TextSpan OperatorText;

        /// <summary>The NOT of <c>IS NOT NULL</c>.</summary>
        public bool IsNot;

        /// <summary>Like ANTLR's <c>ExpressionContext.expression()</c>: the child expressions.</summary>
        public int ChildCount
        {
            get
            {
                switch (Kind)
                {
                    case DqExpressionKind.Unary: return 0;
                    case DqExpressionKind.Binary: return 2;
                    default: return 1;
                }
            }
        }
    }

    internal enum DqUnaryKind
    {
        FunctionCall,
        QualifiedColumnRef,
        BracketedName,
        Literal,
        Star,
        Subquery,
    }

    internal enum DqLiteralKind
    {
        Number,
        String,
        Null,
    }

    /// <summary>The <c>unaryExpression</c> rule.</summary>
    internal sealed class DqUnaryExpression
    {
        public DqUnaryKind Kind;

        public DqFunctionCall Function;

        /// <summary>The first name of a qualified column reference, or the bracketed name.</summary>
        public TextSpan Name;

        /// <summary>The second name of a qualified column reference.</summary>
        public TextSpan Column;

        public DqLiteralKind LiteralKind;
        public TextSpan Literal;

        public DqSelectStatement Subquery;
    }

    /// <summary><c>functionName ( (DISTINCT? expressionList | *)? )</c></summary>
    internal sealed class DqFunctionCall
    {
        public TextSpan Name;
        public bool IsDistinct;

        /// <summary>The arguments, or null for <c>F()</c>.</summary>
        public IReadOnlyList<DqExpression> Arguments;

        /// <summary>Offsets of the call in the query text, for <see cref="DirectQuerySqlLexer.TokenText"/>.</summary>
        public int Start;
        public int End;
    }
}
