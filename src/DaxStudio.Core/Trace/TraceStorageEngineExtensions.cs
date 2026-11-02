using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using DaxStudio.Parsers;
using DaxStudio.Parsers.StorageEngine;

namespace DaxStudio.Core.Trace
{
    public static class TraceStorageEngineExtensions {
        const string searchGuid = @"([_-]\{?([0-9a-fA-F]){8}-([0-9a-fA-F]){4}-([0-9a-fA-F]){4}-([0-9a-fA-F]){4}-([0-9a-fA-F]){12}\}?)";
        const string searchXmSqlFormatStep1 = @"\r\nSELECT([\w\W]+?)\r\nFROM";
        const string searchXmSqlFormatStep2 = @"(LEFT OUTER JOIN|INNER JOIN)\s+(.+?)\s+ON";
        const string searchXmSqlFormatStep3 = @"\,\r\n(DEFINE TABLE|CREATE)";
        const string searchXmSqlFormatStep4 = @"(\] MANYTOMANY FROM ).*( TO )";
        const string searchXmSqlFormatStep5 = @"(?<=,) *?(?=MIN|MAX|SUM|COUNT|DCOUNT)";
        const string searchXmSqlFormatStep6 = @"'(LogAbsValueCallback|RoundValueCallback|MinMaxColumnPositionCallback|Cond)'";
        const string searchXmSqlCallbackStart = @"\[\'?((CallbackDataID)|(EncodeCallback)|(LogAbsValueCallback)|(RoundValueCallback)|(MinMaxColumnPositionCallback)|(Cond))\'?\(?";
        const string searchXmSqlCallbackEnd = @"[\S\s]*?(?<!\]\])\)\]";
        const string searchXmSqlCallbackDax = @"(?<=\[CallbackDataID|EncodeCallback)(?<DAX>[\w\W]*?\))(?=\s?\]\s?\()";
        const string searchXmSqlSquareBracketsWithSpace = searchXmSqlCallbackStart + searchXmSqlCallbackEnd + @"|(?<![\.0-9a-zA-Z'])\[([^\[])*\]";
        const string searchXmSqlKeywords = searchXmSqlCallbackDax + @"|" + searchXmSqlCallbackStart + searchXmSqlCallbackEnd
                    + @"|\bTESTCALLBACKIDENT\b|\bTESTCALLBACKSUM\b|\bPFCASTCOALESCE\b|\bDATAID2STRING\b|\bSEQUENCEINDEX\b|\bNEXTROWINDEX\b|\bSIMPLEINDEXN\b|\bSIMPLEINDEXV\b|\bDESERIALIZE\b|\bFLUSHCACHES\b|\bSIMPLEINDEX"
                    + @"\b|\bDICTIONARY\b|\bDISCRETIZE\b|\bMANYTOMANY\b|\bNOVERTIPAQ\b|\bPARTITIONS\b|\bPFCOALESCE\b|\bDIMENSION\b|\bHIERARCHY\b|\bMANYTOONE\b|\bNOTIMEOUT\b|\bROWFILTER\b|\bSEPARATOR\b|\bSERIALIZE\b|\bTOKENIZED\b|\bVERTICALC"
                    + @"\b|\bANYTOKEN\b|\bASDATAID\b|\bCOALESCE\b|\bCONTAINS\b|\bENDMATCH\b|\bNVARCHAR\b|\bPFDATAID\b|\bRELATION\b|\bFOREIGN\b|\bGENERAL\b|\bININDEX\b|\bNATURAL\b|\bNOSPLIT\b|\bORDERBY\b|\bPRIMARY\b|\bREDUCED\b|\bREVERSE\b|\bSEGMENT\b|\bSHALLOW\b|\bVARIANT"
                    + @"\b|\bAPPEND\b|\bBITMAP\b|\bCOLUMN\b|\bCREATE\b|\bDCOUNT\b|\bDEFINE\b|\bPFCAST\b|\bPREFIX\b|\bSearch\b|\bSELECT\b|\bSTRING\b|\bSUMSQR\b|\bSYSTEM\b|\bCOUNT\b|\bINDEX\b|\bINNER\b|\bNORLE\b|\bOUTER\b|\bPAGED\b|\bRJOIN\b|\bTABLE\b|\bUSING\b|\bVALUE\b|\bVANDR\b|\bWHERE"
                    + @"\b|\bDC_KIND\b|\bDENSE\b"
                    + @"\b|\bAUTO\b|\bBLOB\b|\bC123\b|\bCAST\b|\bDESC\b|\bDROP\b|\bDUMP\b|\bEXEC\b|\bFACT\b|\bFROM\b|\bHASH\b|\bIN32\b|\bIN64\b|\bJOIN\b|\bLEFT\b|\bLOAD\b|\bNINB\b|\bNINH\b|\bNULL\b|\bREAL\b|\bROWS\b|\bSIZE\b|\bSKIP\b|\bWITH"
                    + @"\b|\bAND\b|\bASC\b|\bC64|\bIN0\b|\bINB\b|\bINH\b|\bINT\b|\bINX\b|\bKEY\b|\bMAX\b|\bMIN\b|\bNIN\b|\bNOT\b|\bSET\b|\bSUM\b|\bTOP\b|\bVAND"
                    + @"\b|\bAS\b|\bBY\b|\bIN\b|\bIS\b|\bON\b|\bOR\b|\bPF\b|\bTO\b|\bTW\b|\bUH";
        const string searchXmSqlDotSeparator = @"\.\[";
        const string searchXmSqlParenthesis = @"\ *[\(\)]\ *";
        const string searchXmSqlRemoveDoubleSpaces = @"(?<![\r\n ])(?<whitespace> {2,})";
        const string searchXmSqlAlias = @" AS[\r\n\t\s]?\'[^\']*\'";
        const string searchXmSqlLineageBracket = @" \( [0-9]+ \) \]";
        const string searchXmSqlLineageQuoted = @" \( [0-9]+ \) \'";
        const string searchXmSqlLineageDollar = @" \( [0-9]+ \) \$";
        const string searchXmSqlEmptyArguments = @" \(\s*\) ";
        const string searchXmSqlRowNumberGuidBracket = @"\[RowNumber [0-9A-F ]*\]";
        const string searchXmSqlRowNumberGuidQuoted = @"\$RowNumber [0-9A-F ]*\'";
        const string seachXmSqlPremiumTags = @"<pii>|</pii>|<ccon>|</ccon>";

        const string searchXmSqlPatternSize = @"[\'\[]Estimated size .* : (?<rows>\d+), (?<bytes>\d+)[\'\]]";
        const string searchXmSqlTotalValues = @"(?<=\.\.\[).*?(?=\stotal\s)";

        const string searchFormatDelimiters = @"\|\~.~\|";

        static Regex guidRemoval = new Regex(searchGuid, RegexOptions.Compiled);
        static Regex xmSqlFormatStep1 = new Regex(searchXmSqlFormatStep1, RegexOptions.Compiled);
        static Regex xmSqlFormatStep2 = new Regex(searchXmSqlFormatStep2, RegexOptions.Compiled);
        static Regex xmSqlFormatStep3 = new Regex(searchXmSqlFormatStep3, RegexOptions.Compiled);
        static Regex xmSqlFormatStep4 = new Regex(searchXmSqlFormatStep4, RegexOptions.Compiled);
        static Regex xmSqlFormatStep5 = new Regex(searchXmSqlFormatStep5, RegexOptions.Compiled);
        static Regex xmSqlFormatStep6 = new Regex(searchXmSqlFormatStep6, RegexOptions.Compiled|RegexOptions.IgnoreCase);
        static Regex xmSqlRemoveDoubleSpaces = new Regex(searchXmSqlRemoveDoubleSpaces, RegexOptions.Compiled);
        static Regex xmSqlCallbackStart = new Regex(searchXmSqlCallbackStart, RegexOptions.Compiled);
        static Regex xmSqlTotalValues = new Regex(searchXmSqlTotalValues, RegexOptions.Compiled);
        static Regex xmSqlSquareBracketsWithSpaceRemoval = new Regex(searchXmSqlSquareBracketsWithSpace, RegexOptions.Compiled);
        static Regex xmSqlKeywords = new Regex(searchXmSqlKeywords, RegexOptions.Compiled);
        static Regex xmSqlDotSeparator = new Regex(searchXmSqlDotSeparator, RegexOptions.Compiled);
        static Regex xmSqlParenthesis = new Regex(searchXmSqlParenthesis, RegexOptions.Compiled);
        static Regex xmSqlAliasRemoval = new Regex(searchXmSqlAlias, RegexOptions.Compiled);
        static Regex xmSqlLineageBracketRemoval = new Regex(searchXmSqlLineageBracket, RegexOptions.Compiled);
        static Regex xmSqlLineageQuotedRemoval = new Regex(searchXmSqlLineageQuoted, RegexOptions.Compiled);
        static Regex xmSqlLineageDollarRemoval = new Regex(searchXmSqlLineageDollar, RegexOptions.Compiled);
        static Regex xmSqlEmptyArguments = new Regex(searchXmSqlEmptyArguments, RegexOptions.Compiled);
        static Regex xmSqlRowNumberGuidBracketRemoval = new Regex(searchXmSqlRowNumberGuidBracket, RegexOptions.Compiled);
        static Regex xmSqlRowNumberGuidQuotedRemoval = new Regex(searchXmSqlRowNumberGuidQuoted, RegexOptions.Compiled);
        static Regex xmSqlPremiumTagsRemoval = new Regex(seachXmSqlPremiumTags, RegexOptions.Compiled);

        static Regex xmSqlPatternSize = new Regex(searchXmSqlPatternSize, RegexOptions.Compiled);

        static Regex formatDelimiters = new Regex(searchFormatDelimiters, RegexOptions.Compiled);

        public static string RemoveDaxGuids(this string daxQuery) {
            return guidRemoval.Replace(daxQuery, "");
        }
        private static string RemoveSquareBracketsWithSpace(Match match) {
            if (xmSqlCallbackStart.IsMatch(match.Value))
            {
                // If required, modify the content of a CallbackDataID
                // We currently transform ]] in ] for measure references
                return match.Value.Replace("]]", "]");
            }
            else
            {
                // Specific case for Search function - we might want to classify it as a more generic cas
                // if xmSQL will add other similar functions
                if (match.Value.StartsWith("[Search(", false, CultureInfo.InvariantCulture))
                {
                    return match.Value.Substring(1,match.Value.Length - 2);
                }
                else
                {
                    // Apply the square bracket transformation outside of callbacks
                    return match.Value.Replace("[", "'").Replace("]", "'");
                }
            }
        }
        private static string RemoveSquareBracketsNoSpace(Match match) {
            return match.Value.Replace("[", "").Replace("]", "");
        }
        private static string FixSpaceParenthesis(Match match) {
            string parenthesis = match.Value.Trim();
            return " " + parenthesis + " ";
        }
        private static string RemoveDoubleSpaces(Match match)
        {
            return " ";
        }
        public static string RemoveAlias(this string xmSqlQuery) {
            return xmSqlAliasRemoval.Replace(xmSqlQuery, "");
        }
        public static string RemoveLineage(this string xmSqlQuery) {
            string s = xmSqlLineageBracketRemoval.Replace(xmSqlQuery, "]");
            s = xmSqlLineageQuotedRemoval.Replace(s, "'");
            s = xmSqlLineageDollarRemoval.Replace(s, "$");
            return s;
        }
        public static string FixEmptyArguments(this string xmSqlQuery) {
            return xmSqlEmptyArguments.Replace(xmSqlQuery, " () ");
        }
        public static string RemoveRowNumberGuid(this string xmSqlQuery) {
            string s = xmSqlRowNumberGuidBracketRemoval.Replace(xmSqlQuery, "[RowNumber]");
            s = xmSqlRowNumberGuidQuotedRemoval.Replace(s, "$RowNumber'");
            return s;
        }
        public static string RemovePremiumTags(this string xmSqlQuery)
        {
            return xmSqlPremiumTagsRemoval.Replace(xmSqlQuery, "");
        }
        public static string RemoveDoubleBracketsInCallbacks(this string xmSqlQuery)
        {
            return xmSqlQuery.Replace("]]", "]");
        }
        public static string RemoveXmSqlSquareBrackets(this string xmSqlQuery) {
            string daxQueryNoBrackets = xmSqlSquareBracketsWithSpaceRemoval.Replace(
                            xmSqlQuery,
                            RemoveSquareBracketsWithSpace);
            string daxQueryNoDots = xmSqlDotSeparator.Replace(daxQueryNoBrackets, "[");
            string result = xmSqlParenthesis.Replace(daxQueryNoDots, FixSpaceParenthesis);
            return result;
        }
        public static string RemoveDoubleSpaces(this string xmSqlQuery)
        {
            return xmSqlRemoveDoubleSpaces.Replace(xmSqlQuery, RemoveDoubleSpaces);
        }
        public static string FormatIndexSize(this string xmSqlQuery)
        {
            return xmSqlTotalValues.Replace(xmSqlQuery, FormatNumber);
        }

        // Regex to find numeric literals inside COALESCE(...) in the fallback (non-ANTLR) path
        private static readonly Regex CoalesceDatePattern = new Regex(
            @"(?<=COALESCE\s*\(\s*)-?\d+(?:\.\d+)?(?=\s*\))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// In the regex-based fallback path, annotates numeric values inside COALESCE(number)
        /// with an ISO date comment if they fall within a plausible OLE Automation date range.
        /// </summary>
        public static string ConvertCoalesceDatesToIso(this string xmSqlQuery)
        {
            return CoalesceDatePattern.Replace(xmSqlQuery, m =>
                XmSqlFormattingVisitor.TryConvertOADateToIso(m.Value));
        }
        public static string HighlightXmSqlTokens(this string xmSqlQuery, MatchEvaluator evaluator )
        {
            return xmSqlKeywords.Replace(xmSqlQuery, evaluator);
        }
        public static string HighlightXmSqlTotalValues(this string xmSqlQuery, MatchEvaluator evaluator )
        {
            return xmSqlTotalValues.Replace(xmSqlQuery, evaluator);
        }
        private static string FormatStep1(Match match)
        {
            return match.Value
                .Replace(",\r\n", ",\r\n    ")
                .Replace("], [", "],\r\n    [")
                .Replace("SELECT\r\n", "SELECT\r\n    ");
        }
        private static string FormatStep2(Match match)
        {
            return match.Value.Substring(0,match.Value.Length-3) + "\r\n        ON";
        }
        private static string FormatStep3(Match match)
        {
            return match.Value.Replace(",",",\r\n");
        }
        private static string FormatStep4(Match match)
        {
            return match.Value.Replace(" MANYTOMANY FROM", "\r\n    MANYTOMANY\r\n    FROM").Replace(" TO ", "\r\n        TO ");
        }
        private static string FormatStep5(Match match)
        {
            return "\r\n    ";
        }

        public static string FormatXmSql(this string xmSqlQuery)
        {
            // New line after ' :=  (only table name)
            var stepTable = xmSqlQuery.Replace("] := ", "] :=\r\n");

            var step1 = xmSqlFormatStep1.Replace(stepTable, FormatStep1);
            var step2 = xmSqlFormatStep2.Replace(step1, FormatStep2);
            var step3 = xmSqlFormatStep3.Replace(step2, FormatStep3);
            var step4 = xmSqlFormatStep4.Replace(step3, FormatStep4);
            var step5 = xmSqlFormatStep5.Replace(step4, FormatStep5);
            var step6 = xmSqlFormatStep6.Replace(step5, @"$1");
            var stepFinal = step5;
            return stepFinal;
        }

        private static string FormatNumber(Match match)
        {
            bool validNumber = long.TryParse(match.Value, out long number);
            return validNumber ? number.ToString("#,#") : match.Value;
        }

        public static bool ExtractEstimatedSize(this string daxQuery, out long rows, out long bytes, out string daxQueryFormatted, bool formatTotalValues) {
            // Format the number if requested
            daxQuery = formatTotalValues ? xmSqlTotalValues.Replace(daxQuery, FormatNumber) : daxQuery;
            var m = xmSqlPatternSize.Match(daxQuery);
            string rowsString = m.Groups["rows"].Value;
            string bytesString = m.Groups["bytes"].Value;
            bool foundRows = long.TryParse(rowsString, out rows);
            bool foundBytes = long.TryParse(bytesString, out bytes);
            daxQueryFormatted = xmSqlPatternSize.Replace(daxQuery, $"Estimated size: rows = {(foundRows ? rows.ToString("#,0") : rowsString)}  bytes = {(foundBytes ? bytes.ToString("#,0") : bytesString)}");
            return foundRows && foundBytes;
        }

        public static string ReplaceTableOrColumnNames( this string xmSqlQuery, Dictionary<string,string> TablesOrColumnsMap )
        {
            // NOTE: the speed might be affected by the number of columns/tables
            // we could save time by reducing the mapping to calculated columns only, but it would not work with older versions of metadata (from XML instead of JSON)
            // it should always be applied to tables, though
            foreach ( var replaceName in TablesOrColumnsMap )
            {
                if (xmSqlQuery.Contains(replaceName.Key))
                {
                    xmSqlQuery = xmSqlQuery.Replace(replaceName.Key, replaceName.Value);
                }
            }
            return xmSqlQuery;
        }

        public static string StripFormatDelimiters( this string query )
        {
            return formatDelimiters.Replace(query, "");
        }

        /// <summary>
        /// Checks if the query contains any callback function names.
        /// </summary>
        public static bool ContainsCallback(this string query)
        {
            return query.Contains("CallbackDataID")
                || query.Contains("LogAbsValueCallback")
                || query.Contains("RoundValueCallback")
                || query.Contains("EncodeCallback")
                || query.Contains("MinMaxColumnPositionCallback")
                || query.Contains("Cond");
        }
    }
}
