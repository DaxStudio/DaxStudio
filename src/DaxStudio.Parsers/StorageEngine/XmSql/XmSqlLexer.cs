using Parlot;
using Parlot.Fluent;
using System;
using static Parlot.Fluent.Parsers;

namespace DaxStudio.Parsers.StorageEngine.XmSql
{
    /// <summary>
    /// The tokens of Grammars/xmSQL.g4, without the white space before them. BRACKETED_NAME is the only one scanned by
    /// hand, as its longest match over an ambiguous rule doesn't fit a parser that takes the first match.
    /// </summary>
    internal static class XmSqlLexer
    {
        /// <summary>The chars that continue an identifier, so that a keyword is not followed by one.</summary>
        public const string IdentifierPart = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";

        public const string IdentifierStart = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_";

        public const string Digits = "0123456789";

        private const string FormattedSizeStart = "Estimated size:";

        private static readonly KeywordSet Keywords = new KeywordSet(
            "SET", "DEFINE", "TABLE", "CREATE", "SHALLOW", "RELATION", "MANYTOMANY", "BOTH", "REDUCED", "BY",
            "WITH", "SELECT", "FROM", "LEFT", "OUTER", "INNER", "JOIN", "ON", "WHERE", "IN", "NIN", "BETWEEN",
            "AND", "AS", "TO", "ININDEX", "REVERSE", "BITMAP", "SUM", "COUNT", "DCOUNT", "MIN", "MAX", "AVG",
            "SUMSQR", "COUNT_BIG", "CALLBACKDATAID", "ENCODECALLBACK", "CALLBACK", "PFCASTCOALESCE", "COALESCE",
            "PFCAST", "VAND", "VOR");

        private static readonly Parser<TextSpan> DigitRun = Literals.AnyOf(Digits);

        /// <summary><c>[a-zA-Z_] [a-zA-Z0-9_]*</c>, keywords included.</summary>
        public static readonly Parser<TextSpan> Word = Capture(Literals.AnyOf(IdentifierStart, 1, 1).And(Literals.AnyOf(IdentifierPart, 0)));

        /// <summary>IDENTIFIER: a word that is not a keyword.</summary>
        public static readonly Parser<TextSpan> Identifier = Word.When((context, word) => !IsKeyword(word.Span));

        /// <summary><c>'-'? [0-9]+ ('.' [0-9]+)? ([eE] [+-]? [0-9]+)?</c></summary>
        public static readonly Parser<TextSpan> Number = Literals.Scan(ScanNumber, "-0123456789".AsSpan());

        /// <summary>QUOTED_TABLE_NAME: <c>'\'' (~['\r\n])+ '\''</c></summary>
        public static readonly Parser<TextSpan> QuotedTableName = Capture(Literals.Char('\'').And(Literals.NoneOf("'\r\n")).And(Literals.Char('\'')));

        /// <summary>QUOTED_STRING: <c>'"' (~["\r\n])* '"'</c></summary>
        public static readonly Parser<TextSpan> QuotedString = Capture(Literals.Char('"').And(Literals.NoneOf("\"\r\n", 0)).And(Literals.Char('"')));

        /// <summary><c>'$Expr' [0-9]+</c></summary>
        public static readonly Parser<TextSpan> ExprRef = Capture(Literals.Text("$Expr").And(DigitRun));

        /// <summary><c>'@$Expr' [0-9]+</c></summary>
        public static readonly Parser<TextSpan> ExprAtRef = Capture(Literals.Text("@$Expr").And(DigitRun));

        /// <summary>BRACKETED_NAME, see <see cref="BracketedName(ReadOnlySpan{char})"/>.</summary>
        public static readonly Parser<TextSpan> BracketedNameToken = Literals.Scan(BracketedName, "[".AsSpan());

        /// <summary>
        /// Text between balanced parentheses, parentheses included, skipping quoted names and strings (a DAX expression).
        /// </summary>
        public static readonly Parser<TextSpan> ParenthesizedText = BuildParenthesizedText();

        /// <summary>The estimated size the formatter writes in a truncated list: <c>Estimated size: rows = 1  bytes = 2</c>, up to the closing parenthesis.</summary>
        public static readonly Parser<TextSpan> FormattedEstimatedSize = Capture(Literals.Text(FormattedSizeStart)
            .And(ZeroOrMany(Literals.AnyOf(" ", 0).And(Literals.NoneOf(" )\r\n")))));

        /// <summary>
        /// The estimated size footer after the query: <c>Estimated size: rows = 1  bytes = 2</c> on its line as formatted,
        /// or <c>[Estimated size (volume, marshalling bytes): 1, 2]</c> at the end of the text as traced.
        /// </summary>
        public static readonly Parser<TextSpan> EstimatedSizeFooter =
            Capture(Literals.Text(FormattedSizeStart).And(Literals.NoneOf("\r\n", 0)))
            .Or(BracketedNameToken
                .When((context, name) => name.Span.StartsWith("[Estimated size".AsSpan(), StringComparison.OrdinalIgnoreCase))
                .AndSkip(SkipWhiteSpace(Eof())));

        /// <summary>The next token, as the ANTLR lexer would read it, or one char when there is none.</summary>
        public static readonly Parser<TextSpan> AnyToken =
            BracketedNameToken.Or(QuotedTableName).Or(QuotedString).Or(ExprRef).Or(ExprAtRef).Or(Number)
            .Or(Capture(Literals.Text(":=").Or(Literals.Text("..")).Or(Literals.Text("<>")).Or(Literals.Text("<="))
                .Or(Literals.Text(">=")).Or(Literals.Text("!="))))
            .Or(Word)
            .Or(Literals.Pattern(c => true, 1, 1));

        /// <summary>
        /// Tokens that don't form a filter predicate, up to (and without) the next VAND, VOR, ',', ';', a ')' closing an
        /// enclosing parenthesis, or a statement keyword. Those are part of a parenthesized or braced group, and a group that
        /// doesn't close is not one: its opening char is skipped like any other, so the statements after it are kept.
        /// </summary>
        public static readonly Parser<TextSpan> UnparsedPredicate = BuildUnparsedPredicate();

        private static int ScanNumber(ReadOnlySpan<char> s)
        {
            var i = 0;
            if (i < s.Length && s[i] == '-') i++;
            var digits = i;
            while (i < s.Length && IsDigit(s[i])) i++;
            if (i == digits) return 0;

            if (i + 1 < s.Length && s[i] == '.' && IsDigit(s[i + 1]))
            {
                i += 2;
                while (i < s.Length && IsDigit(s[i])) i++;
            }

            if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
            {
                var j = i + 1;
                if (j < s.Length && (s[j] == '+' || s[j] == '-')) j++;
                if (j < s.Length && IsDigit(s[j]))
                {
                    j++;
                    while (j < s.Length && IsDigit(s[j])) j++;
                    i = j;
                }
            }

            return i;
        }

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        private static Parser<TextSpan> BuildParenthesizedText()
        {
            var nested = Deferred<TextSpan>();
            var item = Capture(Literals.Char('\'').And(Literals.NoneOf("'", 0)).And(Literals.Char('\'')))
                .Or(Capture(Literals.Char('"').And(Literals.NoneOf("\"", 0)).And(Literals.Char('"'))))
                .Or(nested)
                .Or(Literals.NoneOf("()'\""));

            nested.Parser = Capture(Literals.Char('(').And(ZeroOrMany(item)).And(Literals.Char(')')));
            return nested;
        }

        private static Parser<TextSpan> BuildUnparsedPredicate()
        {
            var group = Deferred<TextSpan>();

            // A group char that is not part of a group fails the enclosing groups instead of being read as a token,
            // otherwise each one would be tried both ways.
            var groupItem = SkipWhiteSpace(group.Or(AnyToken.When((context, token) => !IsGroupChar(token.Span))));

            group.Parser = Capture(Literals.Char('(').And(ZeroOrMany(groupItem)).And(SkipWhiteSpace(Literals.Char(')'))))
                .Or(Capture(Literals.Char('{').And(ZeroOrMany(groupItem)).And(SkipWhiteSpace(Literals.Char('}')))));

            var item = SkipWhiteSpace(group.Or(AnyToken.When((context, token) => !EndsPredicate(token.Span))));

            return Capture(OneOrMany(item));
        }

        private static bool IsGroupChar(ReadOnlySpan<char> token)
            => token.Length == 1 && (token[0] == '(' || token[0] == ')' || token[0] == '{' || token[0] == '}');

        private static bool EndsPredicate(ReadOnlySpan<char> token)
        {
            if (token.Length == 1 && (token[0] == ',' || token[0] == ';' || token[0] == ')')) return true;

            foreach (var stop in StopWords)
            {
                if (token.Equals(stop.AsSpan(), StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private static readonly string[] StopWords = { "VAND", "VOR", "DEFINE", "CREATE", "REDUCED", "SELECT", "WITH", "SET" };

        /// <summary>
        /// BRACKETED_NAME:
        /// <c>'[' ( ']' ']' | PAREN_GROUP | ~('[' | ']' | '\r' | '\n') | '[' ~(']' | '\r' | '\n')* ']' )+ ']'</c>
        /// with <c>PAREN_GROUP : '(' ( PAREN_GROUP | ~('(' | ')' | '\r' | '\n') )* ')'</c>.
        /// </summary>
        /// <remarks>
        /// Like the ANTLR lexer, this returns the longest text the rule matches. The rule is ambiguous (a '(' can
        /// open a group or be a plain char, a ']' can end the name or start a ']]' escape), so the scanner tracks
        /// every way the text can be read at once: the main loop, a pending ']' escape, a nested '[...]', and the
        /// depths of the open parenthesis groups as a bit set.
        /// </remarks>
        public static int BracketedName(ReadOnlySpan<char> s)
        {
            if (s.Length < 3 || s[0] != '[') return 0;

            // Fast path: a plain name, [Name], without brackets or parentheses inside.
            for (var k = 1; k < s.Length; k++)
            {
                var c = s[k];
                if (c == ']')
                {
                    if (k == 1) break;
                    if (k + 1 < s.Length && s[k + 1] == ']') break;
                    return k + 1;
                }

                if (c == '[' || c == '(' || c == '\r' || c == '\n') break;
            }

            var main = true;          // in the main loop, after at least one element when 'started'
            var started = false;      // the main loop has matched at least one element
            var escape = false;       // a ']' was read that must be followed by another ']'
            var nested = false;       // inside a nested '[...]'
            ulong groups = 0;         // bit d set: inside a PAREN_GROUP at depth d + 1
            var longest = 0;

            for (var i = 1; i < s.Length; i++)
            {
                var c = s[i];
                if (c == '\r' || c == '\n') break;

                var nextMain = false;
                var nextEscape = false;
                var nextNested = false;
                ulong nextGroups = 0;

                if (main)
                {
                    switch (c)
                    {
                        case ']':
                            if (started && longest < i + 1) longest = i + 1;
                            nextEscape = true;
                            break;
                        case '[':
                            nextNested = true;
                            break;
                        case '(':
                            nextMain = true;     // a plain char
                            nextGroups |= 1;     // or a group at depth 1
                            break;
                        default:
                            nextMain = true;
                            break;
                    }
                }

                if (escape && c == ']')
                {
                    nextMain = true;
                }

                if (nested)
                {
                    if (c == ']') nextMain = true;
                    else nextNested = true;
                }

                if (groups != 0)
                {
                    switch (c)
                    {
                        case '(':
                            nextGroups |= groups << 1;
                            break;
                        case ')':
                            if ((groups & 1) != 0) nextMain = true;
                            nextGroups |= groups >> 1;
                            break;
                        default:
                            nextGroups |= groups;
                            break;
                    }
                }

                // Every transition into the main loop completes an element.
                if (nextMain) started = true;

                main = nextMain;
                escape = nextEscape;
                nested = nextNested;
                groups = nextGroups;

                if (!main && !escape && !nested && groups == 0) break;
            }

            return longest;
        }

        /// <summary>True when the word is one of the grammar's keywords, which the ANTLR lexer never reads as an IDENTIFIER.</summary>
        public static bool IsKeyword(ReadOnlySpan<char> word) => Keywords.Contains(word);
    }
}
