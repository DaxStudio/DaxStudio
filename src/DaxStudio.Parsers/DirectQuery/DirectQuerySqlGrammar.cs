using Parlot;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using static Parlot.Fluent.Parsers;

namespace DaxStudio.Parsers.DirectQuery
{
    /// <summary>
    /// Parlot grammar for the DirectQuery SQL subset, equivalent to Grammars/DirectQuerySql.g4.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rules follow the ANTLR grammar. ANTLR picks, at each decision, the first alternative with which the
    /// whole input still parses, while a PEG commits to the first alternative that matches locally. The two only
    /// disagree where a rule can be followed by a token that an inner rule could also consume:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// An expression is read as a flat chain of operands and operators, then turned into a tree with the
    /// precedences of the ANTLR left-recursive rule (* / % 8, + - 7, comparisons 6, AND 5, OR 4, IS NULL 3, and
    /// NOT applying to everything after it). In <c>joinCondition : expression EQUALS expression</c>, the first
    /// expression would otherwise take the '=' and the second expression any following <c>AND joinCondition</c>;
    /// ANTLR's greedy expression loops make the first expression end before the last '=' of the chain, so the
    /// chain is split there (and <c>(AND joinCondition)*</c> is never entered).
    /// </item>
    /// <item>
    /// The optional parentheses of <c>tableReference</c> and <c>onJoinClause</c>: the opening one is taken when
    /// the rest parses with it, and the closing one when the opening one was taken, or outside of a subquery,
    /// where nothing else can read it.
    /// </item>
    /// </list>
    /// <para>The parser is thread safe: it is built once and keeps no state between parses.</para>
    /// </remarks>
    internal static class DirectQuerySqlGrammar
    {
        private static readonly Parser<DqSelectStatement> Query;
        private static readonly Parser<TextSpan> Trivia = new TriviaParser();

        private static readonly IReadOnlyList<DqJoinClause> NoJoins = Array.Empty<DqJoinClause>();

        static DirectQuerySqlGrammar()
        {
            // ==================== TOKENS ====================

            var bracketedName = Terms.Scan(DirectQuerySqlLexer.BracketedName, "[".AsSpan());
            var dollarTable = Terms.Text(DirectQuerySqlLexer.DollarTable);
            var stringLiteral = Terms.Scan(DirectQuerySqlLexer.StringLiteral, "'".AsSpan());
            var number = Terms.Scan(DirectQuerySqlLexer.Number, "-0123456789".AsSpan());
            var identifier = Terms.Scan(DirectQuerySqlLexer.Identifier, DirectQuerySqlLexer.IdentifierStart.AsSpan());

            var lParen = Terms.Char('(');
            var rParen = Terms.Char(')');
            var comma = Terms.Char(',');
            var dot = Terms.Char('.');
            var star = Terms.Char('*');

            // '-' followed by a digit is a negative NUMBER for the lexer, never a MINUS.
            var minus = Literals.Char('-').WhenNotFollowedBy(Literals.AnyOf("0123456789", maxSize: 1));

            var comparisonOp = SkipWhiteSpace(Capture(Literals.Text("<>").Or(Literals.Text("!=")).Or(Literals.Text(">="))
                .Or(Literals.Text("<=")).Or(Literals.Text("=")).Or(Literals.Text(">")).Or(Literals.Text("<"))));

            var binaryOperator =
                SkipWhiteSpace(Capture(Literals.Char('*').Or(Literals.Char('/')).Or(Literals.Char('%'))))
                    .Then(t => (DqOperatorKind.Multiplicative, t))
                .Or(SkipWhiteSpace(Capture(Literals.Char('+').Or(minus))).Then(t => (DqOperatorKind.Additive, t)))
                .Or(comparisonOp.Then(t => (DqOperatorKind.Comparison, t)))
                .Or(Keyword("AND").Then(t => (DqOperatorKind.And, t)))
                .Or(Keyword("OR").Then(t => (DqOperatorKind.Or, t)));

            var isNull = Kw("IS").SkipAnd(Kw("NOT").Optional()).AndSkip(Kw("NULL")).Then(n => n.HasValue);

            // ==================== EXPRESSIONS ====================

            var nestedSelect = Deferred<DqSelectStatement>();
            var primary = Deferred<DqExpression>();
            var chain = new ExpressionChainParser(primary, binaryOperator, isNull, Kw("NOT"));
            var expression = chain.Then(c => BuildExpression(c, 0, c.Count));

            var functionName = identifier
                .Or(Keyword("SUM")).Or(Keyword("COUNT_BIG")).Or(Keyword("COUNT")).Or(Keyword("MIN")).Or(Keyword("MAX")).Or(Keyword("AVG"))
                .Or(Keyword("ISNULL")).Or(Keyword("CAST")).Or(Keyword("CONVERT")).Or(Keyword("COALESCE"));

            // functionName ( (DISTINCT? expressionList | STAR)? ): F(*) reads as an expressionList with a STAR
            // expression, ANTLR's first alternative, so the STAR alternative is never needed.
            var functionCall = functionName.AndSkip(lParen)
                .And(Kw("DISTINCT").Optional().And(Separated(comma, expression)).Optional())
                .AndSkip(rParen)
                .Then((context, start, end, x) => new DqFunctionCall
                {
                    Name = x.Item1,
                    IsDistinct = x.Item2.HasValue && x.Item2.Value.Item1.HasValue,
                    Arguments = x.Item2.HasValue ? x.Item2.Value.Item2 : null,
                    Start = start,
                    End = end,
                });

            var literal = number.Then(t => (DqLiteralKind.Number, t))
                .Or(stringLiteral.Then(t => (DqLiteralKind.String, t)))
                .Or(Keyword("NULL").Then(t => (DqLiteralKind.Null, t)));

            var unaryExpression =
                functionCall.Then(f => new DqUnaryExpression { Kind = DqUnaryKind.FunctionCall, Function = f })
                .Or(bracketedName.AndSkip(dot).And(bracketedName)
                    .Then(x => new DqUnaryExpression { Kind = DqUnaryKind.QualifiedColumnRef, Name = x.Item1, Column = x.Item2 }))
                .Or(bracketedName.Then(t => new DqUnaryExpression { Kind = DqUnaryKind.BracketedName, Name = t }))
                .Or(literal.Then(x => new DqUnaryExpression { Kind = DqUnaryKind.Literal, LiteralKind = x.Item1, Literal = x.Item2 }))
                .Or(star.Then(_ => new DqUnaryExpression { Kind = DqUnaryKind.Star }))
                .Or(Between(lParen, nestedSelect, rParen).Then(s => new DqUnaryExpression { Kind = DqUnaryKind.Subquery, Subquery = s }));

            primary.Parser =
                unaryExpression.Then(u => new DqExpression { Kind = DqExpressionKind.Unary, Unary = u })
                .Or(Between(lParen, expression, rParen).Then(e => new DqExpression { Kind = DqExpressionKind.Parenthesized, Left = e }));

            // ==================== STATEMENTS ====================

            nestedSelect.Parser = SelectStatement(nested: true, expression, chain, bracketedName, dollarTable, lParen, rParen, comma, dot);

            Query = SelectStatement(nested: false, expression, chain, bracketedName, dollarTable, lParen, rParen, comma, dot);
        }

        private static Parser<DqSelectStatement> SelectStatement(
            bool nested,
            Parser<DqExpression> expression,
            Parser<List<ChainItem>> chain,
            Parser<TextSpan> bracketedName,
            Parser<string> dollarTable,
            Parser<char> lParen,
            Parser<char> rParen,
            Parser<char> comma,
            Parser<char> dot)
        {
            var alias = Kw("AS").SkipAnd(bracketedName);
            var optionalAlias = alias.Optional();

            // The closing parenthesis of an optional pair, see the remarks.
            var closingParen = nested ? (Parser<bool>)Always(false) : rParen.Optional().Then(c => c.HasValue);

            var selectItem = expression.And(optionalAlias)
                .Then(x => new DqSelectItem { Expression = x.Item1, Alias = x.Item2.HasValue ? x.Item2.Value : default });

            var schemaTable = bracketedName.AndSkip(dot).And(bracketedName)
                .Then(x => new DqSchemaTable { Schema = x.Item1, Table = x.Item2 });

            var tableReference =
                lParen.SkipAnd(schemaTable).AndSkip(rParen.Optional()).And(optionalAlias)
                .Or(schemaTable.AndSkip(closingParen).And(optionalAlias))
                .Then(x => new DqTableReference { SchemaTable = x.Item1, Alias = x.Item2.HasValue ? x.Item2.Value : default });

            var subselectColumn = dollarTable.SkipAnd(dot).SkipAnd(bracketedName).AndSkip(Kw("AS")).And(bracketedName)
                .Then(x => new DqSubselectColumn { Source = x.Item1, Alias = x.Item2 });

            // LPAREN LPAREN? SELECT subselectColumnList FROM schemaTable AS DOLLAR_TABLE RPAREN? RPAREN AS bracketedName
            var subselectBlock =
                lParen.SkipAnd(lParen.Optional()).SkipAnd(Kw("SELECT")).SkipAnd(Separated(comma, subselectColumn))
                    .AndSkip(Kw("FROM")).And(schemaTable).AndSkip(Kw("AS")).AndSkip(dollarTable)
                    .AndSkip(rParen).AndSkip(rParen.Optional()).AndSkip(Kw("AS")).And(bracketedName)
                    .Then(x => new DqSubselectBlock { Columns = x.Item1, SchemaTable = x.Item2, Alias = x.Item3 })
                .Or(tableReference.Then(t => new DqSubselectBlock { TableReference = t }));

            var joinType =
                Kw("INNER").SkipAnd(Kw("JOIN")).Then(DqJoinType.Inner)
                .Or(Kw("LEFT").SkipAnd(Kw("OUTER").Optional()).SkipAnd(Kw("JOIN")).Then(DqJoinType.Left))
                .Or(Kw("RIGHT").SkipAnd(Kw("OUTER").Optional()).SkipAnd(Kw("JOIN")).Then(DqJoinType.Right))
                .Or(Kw("FULL").SkipAnd(Kw("OUTER").Optional()).SkipAnd(Kw("JOIN")).Then(DqJoinType.Full))
                .Or(Kw("CROSS").SkipAnd(Kw("JOIN")).Then(DqJoinType.Cross));

            // joinCondition (AND joinCondition)*: the chain up to its last '=' and the chain after it.
            var joinConditions = chain.When((context, c) => LastEquals(c) > 0).Then(SplitJoinCondition);

            var onJoinClause = Kw("ON").SkipAnd(
                lParen.SkipAnd(joinConditions).AndSkip(rParen.Optional())
                .Or(joinConditions.AndSkip(closingParen)));

            var joinClause = joinType.And(subselectBlock).And(onJoinClause.Optional())
                .Then(x => new DqJoinClause { Type = x.Item1, Source = x.Item2, On = x.Item3.HasValue ? x.Item3.Value : null });

            var orderByItem = expression.And(Keyword("ASC").Or(Keyword("DESC")).Optional())
                .Then(x => new DqOrderByItem { Expression = x.Item1, Direction = x.Item2.HasValue ? x.Item2.Value : default });

            return Kw("SELECT").SkipAnd(Separated(comma, selectItem))
                .AndSkip(Kw("FROM")).And(Separated(comma, subselectBlock))
                .And(ZeroOrMany(joinClause))
                .And(Kw("WHERE").SkipAnd(expression).Optional())
                .And(Kw("GROUP").SkipAnd(Kw("BY")).SkipAnd(Separated(comma, expression)).Optional())
                .And(Kw("HAVING").SkipAnd(expression).Optional())
                .And(Kw("ORDER").SkipAnd(Kw("BY")).SkipAnd(Separated(comma, orderByItem)).Optional())
                .Then(x => new DqSelectStatement
                {
                    SelectList = x.Item1,
                    From = x.Item2,
                    Joins = x.Item3.Count == 0 ? NoJoins : x.Item3,
                    Where = x.Item4.HasValue ? x.Item4.Value : null,
                    GroupBy = x.Item5.HasValue ? x.Item5.Value : null,
                    Having = x.Item6.HasValue ? x.Item6.Value : null,
                    OrderBy = x.Item7.HasValue ? x.Item7.Value : null,
                });
        }

        /// <summary>A case-insensitive keyword, returning its canonical upper-case text.</summary>
        private static Parser<string> Kw(string keyword)
            => Terms.Keyword(keyword, DirectQuerySqlLexer.IdentifierPart, caseInsensitive: true);

        /// <summary>A case-insensitive keyword, returning the text as written.</summary>
        private static Parser<TextSpan> Keyword(string keyword)
            => SkipWhiteSpace(Capture(Literals.Keyword(keyword, DirectQuerySqlLexer.IdentifierPart, caseInsensitive: true)));

        // ==================== EXPRESSION CHAINS ====================

        internal enum ChainItemKind : byte
        {
            Not,
            Operand,
            Binary,
            IsNull,
        }

        /// <summary>An element of an expression read as a flat chain.</summary>
        internal struct ChainItem
        {
            public ChainItemKind Kind;
            public DqExpression Operand;
            public DqOperatorKind Operator;
            public TextSpan OperatorText;
            public bool IsNot;
        }

        private static int Precedence(DqOperatorKind kind)
        {
            switch (kind)
            {
                case DqOperatorKind.Multiplicative: return 8;
                case DqOperatorKind.Additive: return 7;
                case DqOperatorKind.Comparison: return 6;
                case DqOperatorKind.And: return 5;
                default: return 4;
            }
        }

        private const int IsNullPrecedence = 3;
        private const int NotOperandPrecedence = 1;

        /// <summary>Builds the tree ANTLR builds for the chain items in [start, end).</summary>
        private static DqExpression BuildExpression(List<ChainItem> items, int start, int end)
        {
            var position = start;
            return BuildExpression(items, ref position, end, 0);
        }

        // The ANTLR left-recursive rule: expression[_p] reads a primary, then each operator whose precedence is at least _p.
        private static DqExpression BuildExpression(List<ChainItem> items, ref int position, int end, int minPrecedence)
        {
            DqExpression left;
            var first = items[position++];
            if (first.Kind == ChainItemKind.Not)
            {
                left = new DqExpression { Kind = DqExpressionKind.Not, Left = BuildExpression(items, ref position, end, NotOperandPrecedence) };
            }
            else
            {
                left = first.Operand;
            }

            while (position < end)
            {
                var item = items[position];
                if (item.Kind == ChainItemKind.Binary)
                {
                    var precedence = Precedence(item.Operator);
                    if (precedence < minPrecedence) break;
                    position++;
                    var right = BuildExpression(items, ref position, end, precedence + 1);
                    left = new DqExpression { Kind = DqExpressionKind.Binary, Left = left, Right = right, Operator = item.Operator, OperatorText = item.OperatorText };
                }
                else if (item.Kind == ChainItemKind.IsNull)
                {
                    if (IsNullPrecedence < minPrecedence) break;
                    position++;
                    left = new DqExpression { Kind = DqExpressionKind.IsNull, Left = left, IsNot = item.IsNot };
                }
                else
                {
                    break;
                }
            }

            return left;
        }

        /// <summary>The index of the last '=' operator of a chain, or -1.</summary>
        private static int LastEquals(List<ChainItem> items)
        {
            for (var i = items.Count - 1; i > 0; i--)
            {
                var item = items[i];
                if (item.Kind == ChainItemKind.Binary && item.Operator == DqOperatorKind.Comparison
                    && item.OperatorText.Length == 1 && item.OperatorText.Span[0] == '=')
                {
                    return i;
                }
            }

            return -1;
        }

        private static IReadOnlyList<DqJoinCondition> SplitJoinCondition(List<ChainItem> items)
        {
            var split = LastEquals(items);
            return new[]
            {
                new DqJoinCondition { Left = BuildExpression(items, 0, split), Right = BuildExpression(items, split + 1, items.Count) },
            };
        }

        /// <summary>
        /// Reads <c>NOT* primary ((binaryOperator NOT* primary) | IS NOT? NULL)*</c>. An operator that is not
        /// followed by an operand ends the chain before it, as ANTLR leaves the expression loop when it
        /// cannot continue.
        /// </summary>
        private sealed class ExpressionChainParser : Parser<List<ChainItem>>
        {
            private readonly Parser<DqExpression> _primary;
            private readonly Parser<(DqOperatorKind, TextSpan)> _operator;
            private readonly Parser<bool> _isNull;
            private readonly Parser<string> _not;

            public ExpressionChainParser(Parser<DqExpression> primary, Parser<(DqOperatorKind, TextSpan)> op, Parser<bool> isNull, Parser<string> not)
            {
                _primary = primary;
                _operator = op;
                _isNull = isNull;
                _not = not;
                Name = "expression";
            }

            public override bool Parse(ParseContext context, ref ParseResult<List<ChainItem>> result)
            {
                context.EnterParser(this);

                var cursor = context.Scanner.Cursor;
                var start = cursor.Position;
                var items = new List<ChainItem>();

                if (!ReadOperand(context, items))
                {
                    cursor.ResetPosition(start);
                    context.ExitParser(this);
                    return false;
                }

                var notResult = new ParseResult<bool>();
                var operatorResult = new ParseResult<(DqOperatorKind, TextSpan)>();
                while (true)
                {
                    var beforeOperator = cursor.Position;
                    var count = items.Count;

                    if (_operator.Parse(context, ref operatorResult))
                    {
                        items.Add(new ChainItem { Kind = ChainItemKind.Binary, Operator = operatorResult.Value.Item1, OperatorText = operatorResult.Value.Item2 });
                        if (ReadOperand(context, items)) continue;

                        items.RemoveRange(count, items.Count - count);
                        cursor.ResetPosition(beforeOperator);
                        break;
                    }

                    if (_isNull.Parse(context, ref notResult))
                    {
                        items.Add(new ChainItem { Kind = ChainItemKind.IsNull, IsNot = notResult.Value });
                        continue;
                    }

                    break;
                }

                result.Set(start.Offset, cursor.Offset, items);
                context.ExitParser(this);
                return true;
            }

            private bool ReadOperand(ParseContext context, List<ChainItem> items)
            {
                var notResult = new ParseResult<string>();
                var primaryResult = new ParseResult<DqExpression>();
                while (_not.Parse(context, ref notResult))
                {
                    items.Add(new ChainItem { Kind = ChainItemKind.Not });
                }

                if (!_primary.Parse(context, ref primaryResult)) return false;
                items.Add(new ChainItem { Kind = ChainItemKind.Operand, Operand = primaryResult.Value });
                return true;
            }
        }

        /// <summary>Skips what the ANTLR lexer skips, see <see cref="DirectQuerySqlLexer.Trivia"/>.</summary>
        private sealed class TriviaParser : Parser<TextSpan>
        {
            public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
            {
                var cursor = context.Scanner.Cursor;
                var start = cursor.Offset;
                var length = DirectQuerySqlLexer.Trivia(cursor.Span);
                if (length == 0) return false;
                cursor.Advance(length);
                result.Set(start, start + length, new TextSpan(context.Scanner.Buffer, start, length));
                return true;
            }
        }

        /// <summary>
        /// Parses a DirectQuery SQL query, <c>selectStatement EOF</c>. Returns null when the text does not start with a
        /// select statement. When text that does not match the grammar follows the statement, where the ANTLR parser
        /// reports syntax errors, the statement read up to there is returned and <paramref name="hasErrors"/> is set.
        /// </summary>
        /// <remarks>
        /// The ANTLR parser recovers from syntax errors and its visitor still reads the clauses it parsed. Keeping the
        /// statement that precedes the error gives the same analysis when the error comes after the last clause the
        /// visitor reads, such as an unsupported predicate in the WHERE clause or a UNION.
        /// </remarks>
        public static DqSelectStatement Parse(string text, out bool hasErrors)
        {
            var context = new ParseContext(new Scanner(text), disableLoopDetection: true) { WhiteSpaceParser = Trivia };
            var result = new ParseResult<DqSelectStatement>();
            if (!Query.Parse(context, ref result))
            {
                hasErrors = true;
                return null;
            }

            context.SkipWhiteSpace();
            hasErrors = !context.Scanner.Cursor.Eof;
            return result.Value;
        }

        /// <summary>Parses a DirectQuery SQL query, returning null when it does not match the grammar.</summary>
        public static DqSelectStatement Parse(string text)
        {
            var statement = Parse(text, out var hasErrors);
            return hasErrors ? null : statement;
        }
    }
}
