using System;
using System.Text;

namespace DaxStudio.Parsers.StorageEngine.DirectQuery
{
    /// <summary>
    /// Token scanners that reproduce the lexer rules of Grammars/DirectQuerySql.g4. Each returns the length of the
    /// token at the start of the span, or 0 when there is none.
    /// </summary>
    internal static class DirectQuerySqlLexer
    {
        /// <summary>The chars that continue an IDENTIFIER, so that a keyword is not followed by one.</summary>
        public const string IdentifierPart = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_@#$";

        public const string IdentifierStart = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_@#";

        public const string DollarTable = "[$Table]";

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        private static bool IsLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        private static bool IsIdentifierStart(char c) => IsLetter(c) || c == '_' || c == '@' || c == '#';

        private static bool IsIdentifierPart(char c) => IsIdentifierStart(c) || IsDigit(c) || c == '$';

        /// <summary><c>[a-zA-Z_@#] [a-zA-Z0-9_@#$]*</c>, keywords included.</summary>
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

        /// <summary>NUMBER: <c>'-'? [0-9]+ ('.' [0-9]+)? ([eE] [+-]? [0-9]+)?</c></summary>
        public static int Number(ReadOnlySpan<char> s) => XmSql.XmSqlLexer.Number(s);

        /// <summary>
        /// BRACKETED_NAME: <c>'[' ~[\]\r\n]+ ']'</c>, except <c>[$Table]</c> which the lexer reads as DOLLAR_TABLE,
        /// the rule defined first.
        /// </summary>
        public static int BracketedName(ReadOnlySpan<char> s)
        {
            if (s.Length < 3 || s[0] != '[') return 0;
            for (var i = 1; i < s.Length; i++)
            {
                var c = s[i];
                if (c == ']')
                {
                    if (i == 1) return 0;
                    return i == DollarTable.Length - 1 && s.StartsWith(DollarTable.AsSpan(), StringComparison.Ordinal) ? 0 : i + 1;
                }

                if (c == '\r' || c == '\n') return 0;
            }

            return 0;
        }

        /// <summary>STRING_LITERAL: <c>'\'' (~['\r\n] | '\'\'')* '\''</c>, the longest match.</summary>
        public static int StringLiteral(ReadOnlySpan<char> s)
        {
            if (s.Length < 2 || s[0] != '\'') return 0;
            var longest = 0;
            var i = 1;
            while (i < s.Length)
            {
                var c = s[i];
                if (c == '\'')
                {
                    // Either the closing quote or the first of an escaped quote.
                    longest = i + 1;
                    if (i + 1 < s.Length && s[i + 1] == '\'')
                    {
                        i += 2;
                        continue;
                    }

                    return longest;
                }

                if (c == '\r' || c == '\n') break;
                i++;
            }

            return longest;
        }

        /// <summary>
        /// The text the lexer skips: white space, comments, and the chars it cannot read as a token. The ANTLR lexer
        /// reports those and drops them, which the silent error listener hides.
        /// </summary>
        public static int Trivia(ReadOnlySpan<char> s)
        {
            var i = 0;
            while (i < s.Length)
            {
                var c = s[i];
                switch (c)
                {
                    case ' ':
                    case '\t':
                    case '\r':
                    case '\n':
                        i++;
                        continue;
                    case '-':
                        if (i + 1 < s.Length && s[i + 1] == '-')
                        {
                            i += 2;
                            while (i < s.Length && s[i] != '\r' && s[i] != '\n') i++;
                            continue;
                        }

                        return i;
                    case '/':
                        if (i + 1 < s.Length && s[i + 1] == '*')
                        {
                            var close = s.Slice(i + 2).IndexOf("*/".AsSpan());
                            if (close >= 0)
                            {
                                i += close + 4;
                                continue;
                            }
                        }

                        return i;
                    case '[':
                        if (BracketedName(s.Slice(i)) > 0 || s.Slice(i).StartsWith(DollarTable.AsSpan(), StringComparison.Ordinal)) return i;
                        i = SkipFailedToken(s, i, ']');
                        continue;
                    case '\'':
                        if (StringLiteral(s.Slice(i)) > 0) return i;
                        i = SkipFailedToken(s, i, '\0');
                        continue;
                    case '!':
                        if (i + 1 < s.Length && s[i + 1] == '=') return i;
                        // The lexer read '!' expecting '=' and drops the char that is not one with it.
                        i = Math.Min(s.Length, i + 2);
                        continue;
                    default:
                        if (StartsToken(c)) return i;
                        i++;
                        continue;
                }
            }

            return i;
        }

        // An unterminated '[' or '\'' token: the lexer drops what it read and the char it failed on.
        private static int SkipFailedToken(ReadOnlySpan<char> s, int i, char terminator)
        {
            i++;
            while (i < s.Length)
            {
                var c = s[i];
                if (c == '\r' || c == '\n' || c == terminator) return i + 1;
                i++;
            }

            return i;
        }

        private static bool StartsToken(char c)
        {
            if (IsIdentifierStart(c) || IsDigit(c)) return true;
            switch (c)
            {
                case '(':
                case ')':
                case ',':
                case '.':
                case ';':
                case '+':
                case '*':
                case '%':
                case '=':
                case '<':
                case '>':
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Returns the length of the token at the start of the span, which must not start with trivia.</summary>
        public static int AnyToken(ReadOnlySpan<char> s)
        {
            if (s.Length == 0) return 0;
            int length;
            switch (s[0])
            {
                case '[':
                    length = s.StartsWith(DollarTable.AsSpan(), StringComparison.Ordinal) ? DollarTable.Length : BracketedName(s);
                    break;
                case '\'': length = StringLiteral(s); break;
                case '-': length = Number(s); break;
                case '<': length = s.Length > 1 && (s[1] == '>' || s[1] == '=') ? 2 : 0; break;
                case '>':
                case '!': length = s.Length > 1 && s[1] == '=' ? 2 : 0; break;
                default:
                    length = IsDigit(s[0]) ? Number(s) : Word(s);
                    break;
            }

            return length > 0 ? length : 1;
        }

        /// <summary>
        /// The text of the tokens between two offsets, without the skipped text in between, like ANTLR's
        /// <c>ParserRuleContext.GetText()</c>.
        /// </summary>
        public static string TokenText(string text, int start, int end)
        {
            var builder = new StringBuilder(end - start);
            var i = start;
            while (true)
            {
                i += Trivia(text.AsSpan(i, end - i));
                if (i >= end) break;
                var length = Math.Min(AnyToken(text.AsSpan(i, end - i)), end - i);
                builder.Append(text, i, length);
                i += length;
            }

            return builder.ToString();
        }

        private static readonly string[][] KeywordsByLength = BuildKeywordTable(
            "SELECT", "FROM", "WHERE", "AS", "ON", "AND", "OR", "NOT", "IN", "IS", "NULL", "INNER", "LEFT", "RIGHT",
            "FULL", "OUTER", "CROSS", "JOIN", "GROUP", "BY", "HAVING", "ORDER", "ASC", "DESC", "DISTINCT", "BETWEEN",
            "LIKE", "CASE", "WHEN", "THEN", "ELSE", "END", "TOP", "UNION", "ALL", "SUM", "COUNT", "MIN", "MAX", "AVG",
            "COUNT_BIG", "ISNULL", "CAST", "CONVERT", "COALESCE");

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
