using Parlot;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Text;
using static Parlot.Fluent.Parsers;

namespace DaxStudio.Parsers.StorageEngine.DirectQuery
{
    /// <summary>
    /// The tokens of Grammars/DirectQuerySql.g4, without the white space before them. The text the lexer skips includes the
    /// tokens it fails to read, so it is scanned by hand with the two tokens that can fail after reading some text.
    /// </summary>
    internal static class DirectQuerySqlLexer
    {
        /// <summary>The chars that continue an IDENTIFIER, so that a keyword is not followed by one.</summary>
        public const string IdentifierPart = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_@#$";

        public const string IdentifierStart = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_@#";

        public const string DollarTable = "[$Table]";

        private static readonly KeywordSet Keywords = new KeywordSet(
            "SELECT", "FROM", "WHERE", "AS", "ON", "AND", "OR", "NOT", "IN", "IS", "NULL", "INNER", "LEFT", "RIGHT",
            "FULL", "OUTER", "CROSS", "JOIN", "GROUP", "BY", "HAVING", "ORDER", "ASC", "DESC", "DISTINCT", "BETWEEN",
            "LIKE", "CASE", "WHEN", "THEN", "ELSE", "END", "TOP", "UNION", "ALL", "SUM", "COUNT", "MIN", "MAX", "AVG",
            "COUNT_BIG", "ISNULL", "CAST", "CONVERT", "COALESCE");

        /// <summary><c>[a-zA-Z_@#] [a-zA-Z0-9_@#$]*</c>, keywords included.</summary>
        public static readonly Parser<TextSpan> Word = Capture(Literals.AnyOf(IdentifierStart, 1, 1).And(Literals.AnyOf(IdentifierPart, 0)));

        /// <summary>IDENTIFIER: a word that is not a keyword.</summary>
        public static readonly Parser<TextSpan> Identifier = Word.When((context, word) => !IsKeyword(word.Span));

        /// <summary>NUMBER: <c>'-'? [0-9]+ ('.' [0-9]+)? ([eE] [+-]? [0-9]+)?</c></summary>
        public static readonly Parser<TextSpan> Number = XmSql.XmSqlLexer.Number;

        /// <summary>BRACKETED_NAME, see <see cref="BracketedName(ReadOnlySpan{char})"/>.</summary>
        public static readonly Parser<TextSpan> BracketedNameToken = Literals.Scan(BracketedName, "[".AsSpan());

        /// <summary>STRING_LITERAL, see <see cref="StringLiteral(ReadOnlySpan{char})"/>.</summary>
        public static readonly Parser<TextSpan> StringLiteralToken = Literals.Scan(StringLiteral, "'".AsSpan());

        /// <summary>The text the lexer skips, see <see cref="Trivia(ReadOnlySpan{char})"/>.</summary>
        public static readonly Parser<TextSpan> TriviaToken = Literals.Scan(Trivia);

        // The tokens up to the end of the text, or one char where there is none.
        private static readonly Parser<IReadOnlyList<TextSpan>> Tokens = ZeroOrMany(SkipWhiteSpace(
            Capture(Literals.Text(DollarTable)).Or(BracketedNameToken).Or(StringLiteralToken).Or(Number)
            .Or(Capture(Literals.Text("<>").Or(Literals.Text("<=")).Or(Literals.Text(">=")).Or(Literals.Text("!="))))
            .Or(Word)
            .Or(Literals.Pattern(c => true, 1, 1))))
            .WithWhiteSpaceParser(TriviaToken);

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        private static bool IsIdentifierStart(char c) => IdentifierStart.IndexOf(c) >= 0;

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

        /// <summary>
        /// The text of the tokens between two offsets, without the skipped text in between, like ANTLR's
        /// <c>ParserRuleContext.GetText()</c>.
        /// </summary>
        public static string TokenText(string text, int start, int end)
        {
            var builder = new StringBuilder(end - start);
            foreach (var token in Tokens.Parse(text.Substring(start, end - start)))
            {
                builder.Append(token.Buffer, token.Offset, token.Length);
            }

            return builder.ToString();
        }

        /// <summary>True when the word is one of the grammar's keywords, which the ANTLR lexer never reads as an IDENTIFIER.</summary>
        public static bool IsKeyword(ReadOnlySpan<char> word) => Keywords.Contains(word);
    }
}
