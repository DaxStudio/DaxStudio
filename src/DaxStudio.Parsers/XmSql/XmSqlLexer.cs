using System;

namespace DaxStudio.Parsers.XmSql
{
    /// <summary>
    /// Token scanners that reproduce the lexer rules of Grammars/xmSQL.g4. Each returns the length of the
    /// token at the start of the span, or 0 when there is none.
    /// </summary>
    internal static class XmSqlLexer
    {
        /// <summary>The chars that continue an identifier, so that a keyword is not followed by one.</summary>
        public const string IdentifierPart = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";

        public const string IdentifierStart = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_";

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        private static bool IsIdentifierStart(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';

        private static bool IsIdentifierPart(char c) => IsIdentifierStart(c) || IsDigit(c);

        /// <summary><c>[a-zA-Z_] [a-zA-Z0-9_]*</c>, keywords included.</summary>
        public static int Word(ReadOnlySpan<char> s)
        {
            if (s.Length == 0 || !IsIdentifierStart(s[0])) return 0;
            var i = 1;
            while (i < s.Length && IsIdentifierPart(s[i])) i++;
            return i;
        }

        /// <summary>IDENTIFIER: a word that is not a keyword.</summary>
        public static int Identifier(ReadOnlySpan<char> s)
        {
            var length = Word(s);
            return length > 0 && !IsKeyword(s.Slice(0, length)) ? length : 0;
        }

        /// <summary><c>'-'? [0-9]+ ('.' [0-9]+)? ([eE] [+-]? [0-9]+)?</c></summary>
        public static int Number(ReadOnlySpan<char> s)
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

        /// <summary>QUOTED_TABLE_NAME: <c>'\'' (~['\r\n])+ '\''</c></summary>
        public static int QuotedTableName(ReadOnlySpan<char> s) => Delimited(s, '\'', allowEmpty: false);

        /// <summary>QUOTED_STRING: <c>'"' (~["\r\n])* '"'</c></summary>
        public static int QuotedString(ReadOnlySpan<char> s) => Delimited(s, '"', allowEmpty: true);

        private static int Delimited(ReadOnlySpan<char> s, char quote, bool allowEmpty)
        {
            if (s.Length < 2 || s[0] != quote) return 0;
            var i = 1;
            while (i < s.Length)
            {
                var c = s[i];
                if (c == quote)
                {
                    return i > 1 || allowEmpty ? i + 1 : 0;
                }

                if (c == '\r' || c == '\n') return 0;
                i++;
            }

            return 0;
        }

        /// <summary><c>'$Expr' [0-9]+</c></summary>
        public static int ExprRef(ReadOnlySpan<char> s) => ExprReference(s, 0);

        /// <summary><c>'@$Expr' [0-9]+</c></summary>
        public static int ExprAtRef(ReadOnlySpan<char> s) => s.Length > 0 && s[0] == '@' ? ExprReference(s, 1) : 0;

        private static int ExprReference(ReadOnlySpan<char> s, int i)
        {
            if (!s.Slice(i).StartsWith("$Expr".AsSpan(), StringComparison.Ordinal)) return 0;
            i += 5;
            var digits = i;
            while (i < s.Length && IsDigit(s[i])) i++;
            return i > digits ? i : 0;
        }

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

        /// <summary>Returns the length of the next token, as the ANTLR lexer would read it, or 1 for an unknown char.</summary>
        public static int AnyToken(ReadOnlySpan<char> s)
        {
            if (s.Length == 0) return 0;
            int length;
            switch (s[0])
            {
                case '[': length = BracketedName(s); break;
                case '\'': length = QuotedTableName(s); break;
                case '"': length = QuotedString(s); break;
                case '$': length = ExprRef(s); break;
                case '@': length = ExprAtRef(s); break;
                case '-': length = Number(s); break;
                case ':': length = s.Length > 1 && s[1] == '=' ? 2 : 0; break;
                case '.': length = s.Length > 1 && s[1] == '.' ? 2 : 0; break;
                case '<': length = s.Length > 1 && (s[1] == '>' || s[1] == '=') ? 2 : 0; break;
                case '>': length = s.Length > 1 && s[1] == '=' ? 2 : 0; break;
                case '!': length = s.Length > 1 && s[1] == '=' ? 2 : 0; break;
                default:
                    length = IsDigit(s[0]) ? Number(s) : Word(s);
                    break;
            }

            return length > 0 ? length : 1;
        }

        private static readonly string[][] KeywordsByLength = BuildKeywordTable(
            "SET", "DEFINE", "TABLE", "CREATE", "SHALLOW", "RELATION", "MANYTOMANY", "BOTH", "REDUCED", "BY",
            "WITH", "SELECT", "FROM", "LEFT", "OUTER", "INNER", "JOIN", "ON", "WHERE", "IN", "NIN", "BETWEEN",
            "AND", "AS", "TO", "ININDEX", "REVERSE", "BITMAP", "SUM", "COUNT", "DCOUNT", "MIN", "MAX", "AVG",
            "SUMSQR", "COUNT_BIG", "CALLBACKDATAID", "ENCODECALLBACK", "CALLBACK", "PFCASTCOALESCE", "COALESCE",
            "PFCAST", "VAND", "VOR");

        private static string[][] BuildKeywordTable(params string[] keywords)
        {
            var max = 0;
            foreach (var k in keywords) max = Math.Max(max, k.Length);
            var table = new string[max + 1][];
            for (var length = 0; length <= max; length++)
            {
                table[length] = Array.FindAll(keywords, k => k.Length == length);
            }

            return table;
        }

        /// <summary>True when the word is one of the grammar's keywords, which the ANTLR lexer never reads as an IDENTIFIER.</summary>
        public static bool IsKeyword(ReadOnlySpan<char> word)
        {
            if (word.Length >= KeywordsByLength.Length) return false;
            foreach (var keyword in KeywordsByLength[word.Length])
            {
                if (word.Equals(keyword.AsSpan(), StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
    }
}
