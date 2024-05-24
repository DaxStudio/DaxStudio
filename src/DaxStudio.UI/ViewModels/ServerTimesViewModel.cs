using System.Collections.Generic;
using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using Newtonsoft.Json;
using System.Windows;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using DaxStudio.QueryTrace;
using DaxStudio.Interfaces;
using Serilog;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Windows.Media;
using DaxStudio.UI.JsonConverters;
using Newtonsoft.Json.Converters;
using System.IO.Packaging;
using System;
using System.Globalization;
using DaxStudio.Common;
using DaxStudio.UI.Utils;
using DaxStudio.Common.Enums;
using DaxStudio.UI.Extensions;
using System.Diagnostics;
using System.Windows.Markup;
using System.Threading.Tasks;
using System.Threading;
using System.Linq.Dynamic;
using DaxStudio.Interfaces.Enums;
using System.Windows.Documents;
using DaxStudio.UI.Views;
using DaxStudio.UI.Controls;
using PoorMansTSqlFormatterLib.Interfaces;
using PoorMansTSqlFormatterLib.ParseStructure;
using System.Data;
using DaxStudio.UI.Enums;

namespace DaxStudio.UI.ViewModels
{
    // Using SimplePropertyChangedBase as a base as it does not have a [DataContract] attribute
    // like the default Caliburn.Micro PropertyChangedBase which breaks the deserilization
    public class TraceStorageEngineEvent : SimplePropertyChangedBase {
        [JsonConverter(typeof(StringEnumConverter))]
        public DaxStudioTraceEventClass Class;
        [JsonConverter(typeof(StringEnumConverter))]
        public DaxStudioTraceEventSubclass Subclass;
        [JsonIgnore]
        public DaxStudioTraceEventClassSubclass ClassSubclass {
            get {
                return new DaxStudioTraceEventClassSubclass { Class = this.Class, Subclass = this.Subclass, QueryLanguage = this.GetQueryLanguage() };
            }
        }

        public bool ShowRawQuery { 
            get { return _showRawQuery; } 
            set { 
                _showRawQuery = value; 
                NotifyOfPropertyChange(nameof(ShowRawQuery));
                // Update also Query to reflect the change in Raw visualization
                NotifyOfPropertyChange(nameof(QueryRichText));
            }
        }
        private bool _showRawQuery = false; // do not show raw query by default

        public string Query { get; set; }
        public virtual string TextData { get; set; }

        private bool IsDaxDirectQuery(string query)
        {
            string sampleQueryStart = query.Substring(0,Math.Min(query.Length,100)).Replace(" ", "").Replace("\n", "").Replace("\r", "");
            return sampleQueryStart.StartsWith("DEFINE", StringComparison.InvariantCultureIgnoreCase)
                   || sampleQueryStart.StartsWith("EVALUATE", StringComparison.InvariantCultureIgnoreCase);
        }
        private DaxStudioTraceEventClassSubclass.Language GetQueryLanguage()
        {
            if (this.Class == DaxStudioTraceEventClass.DirectQueryBegin || this.Class == DaxStudioTraceEventClass.DirectQueryEnd)
            {
                if (IsDaxDirectQuery(Query))
                {
                    return DaxStudioTraceEventClassSubclass.Language.DAX;
                }
                else
                {
                    return DaxStudioTraceEventClassSubclass.Language.SQL;
                }
            }
            else if (this.Class == DaxStudioTraceEventClass.QueryBegin || this.Class == DaxStudioTraceEventClass.QueryEnd)
            {
                switch (this.Subclass)
                {
                    case DaxStudioTraceEventSubclass.DmxQuery:
                        return DaxStudioTraceEventClassSubclass.Language.DMX;
                    case DaxStudioTraceEventSubclass.MdxQuery:
                        return DaxStudioTraceEventClassSubclass.Language.MDX;
                    case DaxStudioTraceEventSubclass.SqlQuery:
                        return DaxStudioTraceEventClassSubclass.Language.SQL;
                    case DaxStudioTraceEventSubclass.DAXQuery:
                        return DaxStudioTraceEventClassSubclass.Language.DAX;
                    default:
                        return DaxStudioTraceEventClassSubclass.Language.Unknown;
                }
            }
            else if (this.Class == DaxStudioTraceEventClass.VertiPaqSEQueryBegin || this.Class == DaxStudioTraceEventClass.VertiPaqSEQueryCacheMatch || this.Class == DaxStudioTraceEventClass.VertiPaqSEQueryEnd)
            {
                return DaxStudioTraceEventClassSubclass.Language.xmSQL;
            }
            return DaxStudioTraceEventClassSubclass.Language.Unknown;
        }

        public long? Duration { get; set; }
        public long? NetParallelDuration { get; set; }
        public long? CpuTime { get; set; }
        public double? CpuFactor { get; set; }
        public int RowNumber { get; set; }
        public long? EstimatedRows { get; set; }
        public long? EstimatedKBytes { get; set; }
        public bool HighlightQuery { get; set; }
        public bool InternalBatchEvent { get; set; }
        public bool IsBatchEvent
        {
            get
            {
                return this.Subclass == DaxStudioTraceEventSubclass.BatchVertiPaqScan;
            }
        }

        public bool IsInternalEvent
        {
            get
            {
                return this.Subclass == DaxStudioTraceEventSubclass.VertiPaqScanInternal;
            }
        }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        // String that highlight important parts of the query
        // Currently implement only the strong (~E~/~S~) for the following functions:
        // - CallbackDataID
        // - EncodeCallback
        // - 'LogAbsValueCallback'
        // - 'RoundValueCallback'
        // - 'MinMaxColumnPositionCallback'
        // - 'Cond'
        private string _queryRichText = "";

        private static string HighlightXmSqlToken(Match match)
        {
            return string.IsNullOrEmpty(match.Groups["DAX"].Value) 
                ? HighlightXmSqlKeyword(match) 
                : HighlightXmSqlDaxCallback(match);
        }
        private static string HighlightXmSqlKeyword(Match match)
        {
            return $"|~K~|{match.Value}|~E~|";
        }
        private static string HighlightXmSqlDaxCallback(Match match)
        {
            return $"|~F~|{match.Value}|~E~|";
        }
        private static string HighlightXmSqlTotalValues(Match match)
        {
            return $"|~N~|{match.Value}|~E~|";
        }
        [JsonIgnore]
        public string QueryRichText {
            set {
                if (Options.HighlightXmSqlCallbacks && ClassSubclass.QueryLanguage == DaxStudioTraceEventClassSubclass.Language.xmSQL)
                {
                    string totalValuesHighlighted = value.HighlightXmSqlTotalValues(HighlightXmSqlTotalValues);
                    string keywordsHighlighted = totalValuesHighlighted.HighlightXmSqlTokens(HighlightXmSqlToken);

                    var sb = new StringBuilder(keywordsHighlighted);
                    // Remove existing highlighters, we want to make sure we apply the |~S~|...|~E~| delimiters only once
                    sb.Replace("|~S~|CallbackDataID|~E~|", "CallbackDataID");
                    sb.Replace("CallbackDataID", "|~S~|CallbackDataID|~E~|");
                    sb.Replace("'LogAbsValueCallback'", "|~S~|LogAbsValueCallback|~E~|");
                    sb.Replace("'RoundValueCallback'", "|~S~|RoundValueCallback|~E~|");
                    sb.Replace("EncodeCallback", "|~S~|EncodeCallback|~E~|");
                    sb.Replace("'MinMaxColumnPositionCallback'", "|~S~|MinMaxColumnPositionCallback|~E~|");
                    sb.Replace("'Cond'", "|~S~|Cond|~E~|");
                    _queryRichText = sb.ToString();
                }
                else
                {
                    _queryRichText = value;
                }
            }

            get => ShowRawQuery ? TextData : _queryRichText; 

        }

        private IGlobalOptions _options;
        [JsonIgnore]
        protected IGlobalOptions Options { get {
                if (_options == null) _options = IoC.Get<IGlobalOptions>();
                return _options;
            }
            private set { _options = value; }
        }

        public long? StartOffsetMs { get; set; }
        public long? TotalQueryDuration { get; set; } = 0;

        [JsonIgnore]
        public long? TimelineDuration => TotalQueryDuration + 1;

        [JsonIgnore]
        public long? DisplayDuration => Convert.ToInt64((EndTime - StartTime).TotalMilliseconds);

        public TraceStorageEngineEvent(DaxStudioTraceEventArgs ev, int rowNumber, IGlobalOptions options, Dictionary<string, string> remapColumns, Dictionary<string, string> remapTables)
        {
            Options = options;
            RowNumber = rowNumber;
            Class = ev.EventClass;
            Subclass = ev.EventSubclass;
            InternalBatchEvent = ev.InternalBatchEvent;
            StartTime = ev.StartTime;
            EndTime = ev.EndTime;
            TextData = ev.TextData;
            switch (Class)
            {
                case DaxStudioTraceEventClass.ExecutionMetrics:
                case DaxStudioTraceEventClass.AggregateTableRewriteQuery:
                    // the rewrite event does not have a query or storage engine timings
                    break;
                case DaxStudioTraceEventClass.DirectQueryBegin:
                case DaxStudioTraceEventClass.DirectQueryEnd:
                    // Format SQL code
                    // Apply bold to keywords
                    // Replace base queries with table alias (optional?)
                    if (!IsDaxDirectQuery(ev.TextData) && Options.FormatDirectQuerySql)
                    {
                        Query = SqlFormatter.FormatSql(ev.TextData);
                    }
                    else
                    {
                        Query = ev.TextData;
                    }
                    break;
                default:
                    string rawText = Options.SimplifyXmSqlSyntax ? ev.TextData.RemovePremiumTags() : ev.TextData;
                    // Format xmSQL
                    string queryFormatted = Options.FormatXmSql ? rawText.FormatXmSql() : rawText;
                    // Replace column names
                    string queryRemapped = Options.ReplaceXmSqlColumnNames ? queryFormatted.ReplaceTableOrColumnNames( remapColumns ) : queryFormatted;
                    // replace table names
                    queryRemapped = Options.ReplaceXmSqlTableNames ? queryRemapped.ReplaceTableOrColumnNames( remapTables ) : queryRemapped;

                    Query = Options.SimplifyXmSqlSyntax 
                                ? queryRemapped
                                    .RemoveDaxGuids()
                                    .RemoveXmSqlSquareBrackets()
                                    .RemoveAlias()
                                    .RemoveLineage()
                                    .FixEmptyArguments()
                                    .RemoveRowNumberGuid()
                                    .RemoveDoubleBracketsInCallbacks()
                                    .RemoveDoubleSpaces()
                                    .FormatIndexSize()
                                : queryRemapped;
                    break;
            }
            
            // Skip Duration/Cpu Time for Cache Match
            if (ClassSubclass.Subclass != DaxStudioTraceEventSubclass.VertiPaqCacheExactMatch)
            {
                Duration = ev.Duration;
                NetParallelDuration = ev.NetParallelDuration;
                if (ClassSubclass.Subclass != DaxStudioTraceEventSubclass.RewriteAttempted)
                {
                    CpuTime = ev.CpuTime;
                    CpuFactor = ev.CpuFactor;
                }
            }
            if (Query != null && Query?.Length > 0)
            {
                long rows, bytes;
                if (Query.ExtractEstimatedSize(out rows, out bytes, out string formattedQuery, true))
                {
                    if (Options.FormatXmSql)
                    {
                        Query = formattedQuery;
                    }

                    EstimatedRows = rows;
                    EstimatedKBytes = 1 + bytes / 1024;
                }

                QueryRichText = Query;
                // Clean highlight code (in case SQL has been formatted)
                if (Query.Contains("|~"))
                {
                    Query = Query.StripFormatDelimiters();
                }
                // Set flag in case any highlight is present
                HighlightQuery = QueryRichText.Contains("|~S~|");
            }
            else
            {
                QueryRichText = null;
                HighlightQuery = false;
            }
        }
        [JsonIgnore]
        public virtual bool ShowTimelineForRow => true;
        public TraceStorageEngineEvent() { }
    }

    
    public class ExecutionMetricsTraceEngineEvent: TraceStorageEngineEvent {
        public ExecutionMetricsTraceEngineEvent() { }

        public ExecutionMetricsTraceEngineEvent(DaxStudioTraceEventArgs ev, int rowNumber, IGlobalOptions options, Dictionary<string, string> remapColumns, Dictionary<string, string> remapTables)
            : base(ev, rowNumber, options, remapColumns, remapTables)
        {
            TextData = ev.TextData;
        }

        public override string TextData { get => base.TextData;
            set { base.TextData = value; 
                ParseTextData(value);
                Query = TextData;
            } 
        }

        [JsonIgnore]
        public DataTable Properties { get; set; } 

        private void ParseTextData(string json)
        {
            Properties = new DataTable();
            Properties.Columns.Add("Property", typeof(string));
            Properties.Columns.Add("Value", typeof(string));
            //Properties.Columns.Add("FormatString", typeof(string));
            var data = JObject.Parse(json);
            foreach (var prop in data.Properties())
            {
                var row = Properties.NewRow();
                row["Property"] = prop.Name;
                var formatString = GetFormatString(prop.Name);
                row["Value"] = ParsePropValue(prop.Name, prop.Value.ToString(), formatString);
                //row["FormatString"] = GetFormatString(prop.Name);
                Properties.Rows.Add(row);

            }
        }

        private string GetFormatString(string name)
        {
            if (name.EndsWith("Ms") 
                || name.EndsWith("KB")
                || name.EndsWith("Rows")) return "N0";
            return string.Empty;
        }

        private string ParsePropValue(string name, string value, string formatString)
        {
            switch (name)
            {
                case "commandType":
                    return value;
                case "queryDialect":
                    int i = -1;
                    int.TryParse(value, out i);
                    return  ((QueryEndSubClass)i).ToString();
                default:
                    if( int.TryParse(value, out var i2 ))
                    {  return i2.ToString(formatString); }
                    if (long.TryParse(value, out var lng))
                    { return lng.ToString(formatString); }
                    return value;
            }
        }

        public override bool ShowTimelineForRow => false;
    }
    
    public class RewriteTraceEngineEvent : TraceStorageEngineEvent
    {

        public RewriteTraceEngineEvent() { }

        public RewriteTraceEngineEvent(DaxStudioTraceEventArgs ev, int rowNumber, IGlobalOptions options, Dictionary<string, string> remapColumns, Dictionary<string, string> remapTables) 
            : base(ev, rowNumber, options, remapColumns, remapTables) {
            TextData = ev.TextData;
        }
        
        public string Table { get; set; }
        public string MatchingResult { get; set; }
        public string Mapping { get; set; }
        private string _textData;
        public override string TextData { get { return _textData; } set {
                _textData = value;
                if (_textData == null) return;
                JObject rewriteResult = JObject.Parse(_textData);
                Table = (string)rewriteResult["table"];
                MatchingResult = (string)rewriteResult["matchingResult"];
                var mapping = rewriteResult["mapping"];
                if (mapping != null) {
                    if (mapping.HasValues) {
                        Mapping = (string)rewriteResult["mapping"]["table"];
                    }
                }
                Query = $"<{MatchingResult}>";
            }
        }
        public new string Query { get; set; } = "";
        public bool MatchFound { get { return MatchingResult == "matchFound"; } }

        public override bool ShowTimelineForRow => false;
    }

    public static class TraceStorageEngineExtensions {
        const string searchGuid = @"([_-]\{?([0-9a-fA-F]){8}-([0-9a-fA-F]){4}-([0-9a-fA-F]){4}-([0-9a-fA-F]){4}-([0-9a-fA-F]){12}\}?)";
        const string searchXmSqlFormatStep1 = @"\r\nSELECT([\w\W]+?)\r\nFROM";
        const string searchXmSqlFormatStep2 = @"(LEFT OUTER JOIN|INNER JOIN)\s+(.+?)\s+ON";
        const string searchXmSqlFormatStep3 = @"\,\r\n(DEFINE TABLE|CREATE)";
        const string searchXmSqlFormatStep4 = @"(\] MANYTOMANY FROM ).*( TO )";
        const string searchXmSqlFormatStep5 = @"(?<=,) *?(?=MIN|MAX|SUM|COUNT|DCOUNT)";
        const string searchXmSqlCallbackStart = @"\[\'?((CallbackDataID)|(EncodeCallback)|(LogAbsValueCallback)|(RoundValueCallback)|(MinMaxColumnPositionCallback)|(Cond))\'?\(";
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
                .Replace(",\r\n", ",\r\n\t")
                .Replace("], [", "],\r\n\t[")
                .Replace("SELECT\r\n", "SELECT\r\n\t");
        }
        private static string FormatStep2(Match match)
        {
            return match.Value.Substring(0,match.Value.Length-3) + "\r\n\t\tON";
        }
        private static string FormatStep3(Match match)
        {
            return match.Value.Replace(",",",\r\n");
        }
        private static string FormatStep4(Match match)
        {
            return match.Value.Replace(" MANYTOMANY FROM", "\r\n\tMANYTOMANY\r\n\tFROM").Replace(" TO ", "\r\n\t\tTO ");
        }
        private static string FormatStep5(Match match)
        {
            return "\r\n\t";
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
            daxQueryFormatted = xmSqlPatternSize.Replace(daxQuery, $"Estimated size: rows = {(foundRows ? rows.ToString("#,#") : rowsString)}  bytes = {(foundBytes ? bytes.ToString("#,#") : bytesString)}");
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
    }


    public class ServerTimesViewModel
        : TraceWatcherBaseViewModel
            , ISaveState
            , IServerTimes
            , ITraceDiagnostics
            , IViewAware
            , IHandle<ThemeChangedEvent>
            , IHandle<CopySEQueryEvent>
            , IHandle<CopyPasteServerTimingsEvent>
            , IHaveData
    {

        private string _queryEndActivityId = string.Empty;

        private bool parallelStorageEngineEventsDetected;
        public bool ParallelStorageEngineEventsDetected
        { 
            get => parallelStorageEngineEventsDetected; 
            set { 
                parallelStorageEngineEventsDetected = value;
                NotifyOfPropertyChange(nameof(ParallelStorageEngineEventsDetected));
            }
        }

        private DaxStudioTraceEventArgs maxStorageEngineVertipaqEvent;
        private DaxStudioTraceEventArgs maxStorageEngineDirectQueryEvent;
        public IGlobalOptions Options { get; set; }
        public Dictionary<string, string> RemapColumnNames { get; set; }
        public Dictionary<string, string> RemapTableNames { get; set; }

        [ImportingConstructor]
        public ServerTimesViewModel(IEventAggregator eventAggregator, ServerTimingDetailsViewModel serverTimingDetails
            , IGlobalOptions options, IWindowManager windowManager) : base(eventAggregator, options,windowManager)
        {
            _storageEngineEvents = new BindableCollection<TraceStorageEngineEvent>();
            RemapColumnNames = new Dictionary<string, string>();
            RemapTableNames = new Dictionary<string, string>();
            Options = options;
            // Use global option as a default but doesn't change it at runtime
            StorageEventTimelineStyle = options.StorageEventHeatmapStyle;
            ServerTimingDetails = serverTimingDetails;
            this.ViewAttached += ServerTimesViewModel_ViewAttached;
        }

        private void ServerTimesViewModel_ViewAttached(object sender, ViewAttachedEventArgs e)
        {
            var view = e.View as ServerTimesView;
            if (view == null) return;

            DataObject.AddCopyingHandler(view.EventDetails, OnCopyEventDetails);
            
        }

        private void OnCopyEventDetails(object sender, DataObjectCopyingEventArgs e)
        {
            ClipboardManager.ReplaceLineBreaks(e.DataObject);
        }


        #region Tooltip properties
        public string TotalTooltip => "The total server side duration of the query";
        public string FETooltip => "Formula Engine (FE) Duration";
        public string SETooltip => "Storage Engine (SE) Duration";
        public string SENetParallelTooltip => "Storage Engine (SE) Net Duration - accounting for parallel operations";
        public string SECpuTooltip => "Storage Engine CPU Duration";
        public string SEQueriesTooltip => "The number of queries sent to the Storage Engine while processing this query";
        public string SECacheTooltip => "The number of queries sent to the Storage Engine that were answered from the SE Cache";
        #endregion

        protected override List<DaxStudioTraceEventClass> GetMonitoredEvents()
        {

            return new List<DaxStudioTraceEventClass>
                { DaxStudioTraceEventClass.QuerySubcube
                , DaxStudioTraceEventClass.VertiPaqSEQueryBegin
                , DaxStudioTraceEventClass.VertiPaqSEQueryEnd
                , DaxStudioTraceEventClass.VertiPaqSEQueryCacheMatch
                , DaxStudioTraceEventClass.AggregateTableRewriteQuery
                , DaxStudioTraceEventClass.ExecutionMetrics
                , DaxStudioTraceEventClass.DirectQueryEnd
                , DaxStudioTraceEventClass.QueryBegin
                , DaxStudioTraceEventClass.QueryEnd};
        }

        public bool HighlightXmSqlCallbacks => Options.HighlightXmSqlCallbacks;

        public bool SimplifyXmSqlSyntax => Options.SimplifyXmSqlSyntax;

        public bool ReplaceXmSqlColumnNames => Options.ReplaceXmSqlColumnNames;

        public bool ReplaceXmSqlTableNames => Options.ReplaceXmSqlTableNames;

        public bool ShowTotalDirectQueryDuration => Options.ShowTotalDirectQueryDuration;

        public bool ShowStorageEngineNetParallelDuration => Options.ShowStorageEngineNetParallelDuration;

        public override string TraceStatusText
        {
            get
            {
                return string.IsNullOrEmpty(ErrorMessage) ? base.TraceStatusText : ErrorMessage;
            }
        }

        public override string ErrorMessage
        {
            get => base.ErrorMessage;
            set
            {
                base.ErrorMessage = value;
                NotifyOfPropertyChange(() => TraceStatusText);
            }
        }

        protected override void OnUpdateGlobalOptions(UpdateGlobalOptions message)
        {
            base.OnUpdateGlobalOptions(message);
            NotifyOfPropertyChange(nameof(HighlightXmSqlCallbacks));
            NotifyOfPropertyChange(nameof(SimplifyXmSqlSyntax));
            NotifyOfPropertyChange(nameof(ReplaceXmSqlColumnNames));
            NotifyOfPropertyChange(nameof(StorageEventHeatmapHeight));
            NotifyOfPropertyChange(nameof(StorageEventTimelineStyle));
            NotifyOfPropertyChange(nameof(TimelineVerticalMargin));
        }

        protected override void ProcessSingleEvent(DaxStudioTraceEventArgs singleEvent)
        {
            base.ProcessSingleEvent(singleEvent);

            // These events are processed in "real-time" during the execution, just to show that something is moving in total time
            // We do not provide details of FE/SE until the execution is completed
            switch (singleEvent.EventClass)
            {
                case DaxStudioTraceEventClass.QueryBegin:
                    // Reset duration when query begins
                    QueryStartDateTime = singleEvent.StartTime;
                    TotalDuration = 0;
                    break;
                case DaxStudioTraceEventClass.ExecutionMetrics:
                    // we need to grab the ExecutionMetrics here since it arrives after the QueryEnd event
                    if (singleEvent.ActivityId == _queryEndActivityId && !string.IsNullOrEmpty(_queryEndActivityId))
                    {
                        AllStorageEngineEvents.Add(new ExecutionMetricsTraceEngineEvent(singleEvent, AllStorageEngineEvents.Count + 1, Options, RemapColumnNames, RemapTableNames));
                        NotifyOfPropertyChange(nameof(StorageEngineEvents));
                    }
                    break;
                default:
                    // Updates the Total for each following event
                    TotalDuration = (long)(singleEvent.CurrentTime - QueryStartDateTime).TotalMilliseconds;
                    break;
            }

            if (singleEvent.EventClass == DaxStudioTraceEventClass.QueryEnd) { _queryEndActivityId = singleEvent.ActivityId; }
        }

        protected struct SortableEvent : IComparable<SortableEvent>
        {
            public DateTime TimeStamp;
            public bool IsStart;
            public DaxStudioTraceEventArgs Event;

            int IComparable<SortableEvent>.CompareTo(SortableEvent y)
            {
                return this.CompareTo(y);
            }
            public override int GetHashCode()
            {
                int hash = 23;
                hash = hash * 31 + TimeStamp.GetHashCode();
                hash = hash * 31 + Event.GetHashCode();
                return hash;
            }

            int CompareTo(SortableEvent y)
            {
                // Start is always before end
                if (this.IsStart != y.IsStart)
                {
                    return this.IsStart ? -1 : 1;
                }

                // Compare TimeStamp meaningful only for comparable events
                // (QueryEnd has another priority)
                var compareTimeStamp = this.TimeStamp.CompareTo(y.TimeStamp);

                // If Start, QueryBegin before SE and DirectQuery
                if (this.IsStart)
                {
                    if (this.Event.EventClass == DaxStudioTraceEventClass.QueryEnd
                        && y.Event.EventClass != DaxStudioTraceEventClass.QueryEnd)
                    {
                        // this is QueryStart, y is not QueryStart, this before y
                        return -1;
                    }
                    else if (this.Event.EventClass != DaxStudioTraceEventClass.QueryEnd
                        && y.Event.EventClass == DaxStudioTraceEventClass.QueryEnd)
                    {
                        // this is not QueryStart, y is QueryStart, this after y
                        return 1;
                    }
                    else return compareTimeStamp;
                }
                else
                {
                    // If End, QueryEnd after SE and DirectQuery
                    if (this.Event.EventClass == DaxStudioTraceEventClass.QueryEnd
                        && y.Event.EventClass != DaxStudioTraceEventClass.QueryEnd)
                    {
                        // this is QueryEnd, y is not QueryEnd, this after y
                        return 1;
                    }
                    else if (this.Event.EventClass != DaxStudioTraceEventClass.QueryEnd
                        && y.Event.EventClass == DaxStudioTraceEventClass.QueryEnd)
                    {
                        // this is not QueryEnd, y is QueryEnd, this before y
                        return -1;
                    }
                    else return compareTimeStamp;
                }
            }
            int CompareTo(object obj)
            {
                if (!(obj is SortableEvent)) throw new Exception($"Invalid argument obj type {obj.GetType().Name} in SortableEvent.CompareTo");
                return this.CompareTo((SortableEvent)obj);
            }
            public override bool Equals(object obj)
            {
                if (!(obj is SortableEvent)) return false; //avoid double casting
                return CompareTo((SortableEvent)obj) == 0;
            }
            public static bool operator ==(SortableEvent left, SortableEvent right)
            {
                return left.Equals(right);
            }
            public static bool operator !=(SortableEvent left, SortableEvent right)
            {
                return !(left == right);
            }
            public static bool operator <(SortableEvent left, SortableEvent right)
            {
                return left.CompareTo(right) < 0;
            }
            public static bool operator >(SortableEvent left, SortableEvent right)
            {
                return left.CompareTo(right) > 0;
            }
            public static bool operator <=(SortableEvent left, SortableEvent right)
            {
                return left.CompareTo(right) <= 0;
            }
            public static bool operator >=(SortableEvent left, SortableEvent right)
            {
                return left.CompareTo(right) >= 0;
            }
        }


        // This method is called after the WaitForEvent is seen (usually the QueryEnd event)
        // This is where you can do any processing of the events before displaying them to the UI
        protected override void ProcessResults()
        {
            if (AllStorageEngineEvents?.Count > 0) return;
            // results have not been cleared so this is probably an end event from some other
            // action like a tooltip populating

            ClearAll();

            int batchScan = 0;
            long batchStorageEngineDuration = 0;
            long batchStorageEngineCpu = 0;
            long batchStorageEngineQueryCount = 0;

            maxStorageEngineVertipaqEvent = null;
            maxStorageEngineDirectQueryEvent = null;
            bool eventsProcessed = false;

            if (Events != null)
            {
                // exit early if this is an internal query
                if (IsDaxStudioInternalQuery())
                {
                    Events.Clear();
                    return;
                };

                bool IsEnd(DaxStudioTraceEventClass eventClass)
                {
                    return eventClass == DaxStudioTraceEventClass.VertiPaqSEQueryEnd
                        || eventClass == DaxStudioTraceEventClass.DirectQueryEnd
                        || eventClass == DaxStudioTraceEventClass.QueryEnd;
                }
                
                // TODO CHECK 
                // Fix duration and end time
                foreach (var traceEvent in Events.Where(e => IsEnd(e.EventClass)))
                {
                    // Fix EndTime if required - after analysis of parallel events
                    if (traceEvent.EndTime == traceEvent.StartTime && traceEvent.Duration > 0)
                    {
                        traceEvent.EndTime = traceEvent.StartTime.AddMilliseconds((double)traceEvent.Duration);
                        Log.Debug($">> fix EndTime row Duration={traceEvent.Duration} StartTime={traceEvent.StartTime.Millisecond} EndTime={traceEvent.EndTime.Millisecond} Duration={traceEvent.Duration} NetParallelDuration={traceEvent.NetParallelDuration} Cpu={traceEvent.CpuTime}");
                    }
                    else if (traceEvent.EndTime >= traceEvent.StartTime && (traceEvent.EndTime - traceEvent.StartTime).TotalMilliseconds > traceEvent.Duration)
                    {
                        traceEvent.Duration = Convert.ToInt64((traceEvent.EndTime - traceEvent.StartTime).TotalMilliseconds);
                        Log.Debug($">> fix Duration row Duration={traceEvent.Duration} StartTime={traceEvent.StartTime.Millisecond} EndTime={traceEvent.EndTime.Millisecond} Duration={traceEvent.Duration} NetParallelDuration={traceEvent.NetParallelDuration} Cpu={traceEvent.CpuTime}");
                    }
                    else
                    {
                        Log.Debug($">> NOT row Duration={traceEvent.Duration} StartTime={traceEvent.StartTime.Millisecond} EndTime={traceEvent.EndTime.Millisecond} Duration={traceEvent.Duration} NetParallelDuration={traceEvent.NetParallelDuration} Cpu={traceEvent.CpuTime}");
                    }
                    // Align NetParallelDuration to Duration
                    traceEvent.NetParallelDuration = traceEvent.Duration;
                }
                // END TODO CHECK 
                
                // Copy all SE events for new FE calculation
                var seEvents =
                    (
                        from e in Events
                        where IsEnd(e.EventClass)
                        select new SortableEvent { TimeStamp = e.StartTime, IsStart = true, Event = e }
                    ).Union(
                        from e in Events
                        where IsEnd(e.EventClass)
                        select new SortableEvent { TimeStamp = e.EndTime, IsStart = false, Event = e }
                    ).OrderBy(e => e).ToList();

                // Scan events sequentially computing SE time when there are no SE events active
                int seLevel = 0;
                double new_FormulaEngineDuration = 0;
                DateTime currentScanTime = DateTime.MinValue;
                foreach (var e in seEvents)
                {
                    Log.Debug($"** lev={seLevel} Event {e.Event.EventClass} Time={e.TimeStamp.TimeOfDay}");
                    switch (e.Event.EventClass)
                    {
                        case DaxStudioTraceEventClass.QueryEnd:
                            Log.Debug($"QueryEnd StartTime={e.Event.StartTime.Millisecond} EndTime={e.Event.EndTime.Millisecond}");

                            if (e.IsStart)
                            {
                                // currentScanTime = e.TimeStamp;
                                currentScanTime = e.Event.StartTime;
                                // Placeholder: START FE event
                            }
                            else
                            {
                                Debug.Assert(currentScanTime > DateTime.MinValue, "Missing QueryBegin event, invalid FE calculation");
                                Debug.Assert(seLevel == 0, "Invalid storage engine level at QueryEnd event, invalid FE calculation");
                                if (seLevel == 0)
                                {
                                    var delta = (e.TimeStamp - currentScanTime).TotalMilliseconds;
                                    Log.Debug($"FE += {delta}ms QueryEnd currentScanTime={currentScanTime.Millisecond} TimeStamp={e.TimeStamp.Millisecond}");
                                    new_FormulaEngineDuration += delta;
                                    // Placeholder: END FE event
                                }
                            }
                            break;
                        case DaxStudioTraceEventClass.VertiPaqSEQueryEnd:
                        case DaxStudioTraceEventClass.DirectQueryEnd:
                            Log.Debug($"VertiPaqSEQueryEnd {e.Event.EventSubclassName} StartTime={e.Event.StartTime.Millisecond} EndTime={e.Event.EndTime.Millisecond} Offset={(e.Event.StartTime - currentScanTime).TotalMilliseconds}");
                            if (e.IsStart)
                            {
                                if (seLevel == 0)
                                {
                                    var delta = (e.Event.StartTime - currentScanTime).TotalMilliseconds;
                                    // delta = (delta < e.Event.Duration && e.Event.Duration > 0) ? e.Event.Duration : delta;
                                    Log.Debug($"FE += {delta}ms VertiPaqSEQueryEnd currentScanTime={currentScanTime.Millisecond} TimeStamp={e.TimeStamp.Millisecond}");
                                    new_FormulaEngineDuration += delta;
                                    // Placeholder: END FE event
                                }
                                seLevel++;
                            }
                            else
                            {
                                seLevel--;
                                if (seLevel == 0)
                                {
                                    currentScanTime = e.Event.EndTime;
                                    // Placeholder: START FE event
                                }
                            }
                            break;
                    }
                    Debug.Assert(seLevel >= 0, "Invalid storage engine level, invalid FE calculation");

                }

                eventsProcessed = !Events.IsEmpty;
                while (!Events.IsEmpty)
                {

                    Events.TryDequeue(out var traceEvent);
                    switch (traceEvent.EventClass)
                    {
                        case DaxStudioTraceEventClass.VertiPaqSEQueryBegin:

                            // At the start of a batch, we just activate the flag BatchScan
                            if (traceEvent.EventSubclass == DaxStudioTraceEventSubclass.BatchVertiPaqScan)
                            {
                                batchScan++;
                                // The value should never be greater than 1. If it happens, we should log it and investigate as possible bug.
                                System.Diagnostics.Debug.Assert(batchScan == 1, "Nested VertiScan batches detected or missed SE QueryEnd events!");

                                // Reset counters for internal batch cost
                                batchStorageEngineDuration = 0;
                                batchStorageEngineCpu = 0;
                                batchStorageEngineQueryCount = 0;
                            }

                            break;
                        case DaxStudioTraceEventClass.VertiPaqSEQueryEnd:
                            Log.Debug($"VertiPaqSEQueryEnd {traceEvent.EventSubclass} Duration={traceEvent.Duration} NetParallelDuration={traceEvent.NetParallelDuration} Cpu={traceEvent.CpuTime}" );
                            // At the end of a batch, we compute the cost for the batch and assign the cost to the complete query
                            if (traceEvent.EventSubclass == DaxStudioTraceEventSubclass.BatchVertiPaqScan)
                            {
                                batchScan--;
                                // The value should never be greater than 1. If it happens, we should log it and investigate as possible bug.
                                System.Diagnostics.Debug.Assert(batchScan == 0, "Nested VertiScan batches detected or missed SE QueryBegin events!");

                                Log.Debug($"FIX EndScan traceEvent.Duration={traceEvent.Duration}ms batchStorageEngineDuration={batchStorageEngineDuration}");
                                // Fix 
                                // Subtract from the batch event the total computed for the scan events within the batch
                                traceEvent.Duration = Math.Max((long)(traceEvent.Duration - batchStorageEngineDuration), 0);
                                traceEvent.NetParallelDuration = traceEvent.Duration;
                                traceEvent.CpuTime = Math.Max((long)(traceEvent.CpuTime - batchStorageEngineCpu), 0 );

                                StorageEngineDuration += traceEvent.Duration;
                                StorageEngineNetParallelDuration += traceEvent.Duration;
                                Log.Debug($"StorageEngineDuration)={StorageEngineDuration}");
                                StorageEngineCpu += traceEvent.CpuTime;
                                // Currently, we do not compute a storage engine query for the batch event - we might uncomment this if we decide to show the Batch event by default
                                StorageEngineQueryCount++;

                            }
                            else if (traceEvent.EventSubclass == DaxStudioTraceEventSubclass.VertiPaqScan)
                            {
                                if (batchScan > 0)
                                {
                                    traceEvent.InternalBatchEvent = true;
                                    batchStorageEngineDuration += traceEvent.NetParallelDuration;
                                    batchStorageEngineCpu += traceEvent.CpuTime;
                                    batchStorageEngineQueryCount++;
                                }
                                else
                                {
                                    // Ignore internal batch events to update parallel operation
                                    UpdateForParallelOperations(ref maxStorageEngineVertipaqEvent, traceEvent);
                                    StorageEngineDuration += traceEvent.Duration;
                                }
                                StorageEngineNetParallelDuration += traceEvent.NetParallelDuration;
                                StorageEngineCpu += traceEvent.CpuTime;
                                StorageEngineQueryCount++;

                            }
                            UpdateTimelineTotalDuration(traceEvent);
                            AllStorageEngineEvents.Add(new TraceStorageEngineEvent(traceEvent, AllStorageEngineEvents.Count + 1, Options, RemapColumnNames, RemapTableNames));

                            break;
                        case DaxStudioTraceEventClass.DirectQueryEnd:
                            UpdateForParallelOperations(ref maxStorageEngineDirectQueryEvent, traceEvent);
                            TotalDirectQueryDuration += traceEvent.Duration;
                            StorageEngineDuration += traceEvent.Duration;
                            StorageEngineNetParallelDuration += traceEvent.NetParallelDuration;
                            StorageEngineCpu += traceEvent.CpuTime;
                            StorageEngineQueryCount++;
                            UpdateTimelineTotalDuration(traceEvent);
                            AllStorageEngineEvents.Add(new TraceStorageEngineEvent(traceEvent, AllStorageEngineEvents.Count + 1, Options, RemapColumnNames, RemapTableNames));
                            break;

                        case DaxStudioTraceEventClass.AggregateTableRewriteQuery:
                            AllStorageEngineEvents.Add(new RewriteTraceEngineEvent(traceEvent, AllStorageEngineEvents.Count + 1, Options, RemapColumnNames, RemapTableNames));
                            break;
                        case DaxStudioTraceEventClass.ExecutionMetrics:
                            //AllStorageEngineEvents.Add(new ExecutionMetricsTraceEngineEvent(traceEvent, AllStorageEngineEvents.Count + 1, Options, RemapColumnNames, RemapTableNames));
                            break;
                        case DaxStudioTraceEventClass.QueryEnd:

                            TotalDuration = traceEvent.Duration;
                            TotalCpuDuration = traceEvent.CpuTime;
                            QueryEndDateTime = traceEvent.EndTime;
                            QueryStartDateTime = traceEvent.StartTime;
                            ActivityID = traceEvent.ActivityId;
                            UpdateTimelineTotalDuration(traceEvent);
                            break;
                        case DaxStudioTraceEventClass.QueryBegin:
                            Parameters = traceEvent.RequestParameters;
                            CommandText = traceEvent.TextData;
                            break;
                        case DaxStudioTraceEventClass.VertiPaqSEQueryCacheMatch:

                            VertipaqCacheMatches++;
                            UpdateTimelineTotalDuration(traceEvent);
                            AllStorageEngineEvents.Add(new TraceStorageEngineEvent(traceEvent, AllStorageEngineEvents.Count + 1, Options, RemapColumnNames, RemapTableNames));
                            break;
                    }
                }

                // TODO - do we want to add a total row at the end?
                /*
                var totalSEDuration = AllStorageEngineEvents
                                        .Where(e => 
                                            (e.Class == DaxStudioTraceEventClass.VertiPaqSEQueryEnd && (e.Subclass == DaxStudioTraceEventSubclass.VertiPaqScan || e.Subclass == DaxStudioTraceEventSubclass.BatchVertiPaqScan)) 
                                            || e.Class == DaxStudioTraceEventClass.DirectQueryEnd).Sum(e => e.Duration)??0;

                AllStorageEngineEvents.Add(new TraceStorageEngineEvent(new DaxStudioTraceEventArgs("Total", "NotAvailable", totalSEDuration, 0, string.Empty, string.Empty, QueryStartDateTime), AllStorageEngineEvents.Count + 1, Options, RemapColumnNames, RemapTableNames));
                */

                // New calculation for parallel SE queries (2022-10-03) Marco Russo
                // 
                // Old calculation commented: the FE is the difference between Total Duration and SE Duration
                // long old_FormulaEngineDuration = TotalDuration - StorageEngineNetParallelDuration;

                // New calculation: SE is Query Duration - FE Duration
                //                  FE Duration is computed as time when there are no SE queries running
                Log.Debug($"FormulaEngineDuration={FormulaEngineDuration}ms new={new_FormulaEngineDuration}");
                FormulaEngineDuration = (long)new_FormulaEngineDuration;
                TotalDuration = FormulaEngineDuration > TotalDuration ? FormulaEngineDuration : TotalDuration;
                double computed_Duration = StorageEngineNetParallelDuration + FormulaEngineDuration;
                if (computed_Duration < TotalDuration)
                {
                    StorageEngineDuration = StorageEngineNetParallelDuration;
                    FormulaEngineDuration = TotalDuration - StorageEngineDuration;
                }
                else
                {
                    StorageEngineDuration = TotalDuration - FormulaEngineDuration;
                }
                // End of new calculation for parallel SE queries

                if (QueryHistoryEvent != null)
                {
                    QueryHistoryEvent.FEDurationMs = FormulaEngineDuration;
                    QueryHistoryEvent.SEDurationMs = StorageEngineDuration;
                    QueryHistoryEvent.ServerDurationMs = TotalDuration;

                    _eventAggregator.PublishOnUIThreadAsync(QueryHistoryEvent);
                }
                if (eventsProcessed) _eventAggregator.PublishOnUIThreadAsync(new ServerTimingsEvent(this));

                Events.Clear();
                UpdateTimelineDurations(QueryStartDateTime, QueryEndDateTime, TimelineTotalDuration);
                //NotifyOfPropertyChange(nameof(CanExport));
                //NotifyOfPropertyChange(nameof(CanCopyResults));
                //NotifyOfPropertyChange(nameof(CanShowTraceDiagnostics));
                //NotifyOfPropertyChange(nameof(StorageEventHeatmap));
                Refresh(); // update all data bindings
            }
        }

        private void UpdateTimelineTotalDuration(DaxStudioTraceEventArgs traceEvent)
        {
            var maxDuration = (traceEvent.StartTime.AddMilliseconds(traceEvent.Duration == 0 ? 1 : traceEvent.Duration) - QueryStartDateTime).TotalMilliseconds;
            if (maxDuration > TimelineTotalDuration)
                TimelineTotalDuration = Convert.ToInt64(maxDuration);
        }

        private void UpdateTimelineDurations(DateTime queryStartDateTime, DateTime queryEndDateTime, long totalDuration)
        {
            foreach (var traceEvent in AllStorageEngineEvents)
            {
                traceEvent.StartOffsetMs = Convert.ToInt64((traceEvent.StartTime - queryStartDateTime).TotalMilliseconds );
                // WARNING: we recalculate the duration based on the start/end time
                // traceEvent.Duration = Convert.ToInt64((traceEvent.EndTime - traceEvent.StartTime).TotalMilliseconds);
                traceEvent.TotalQueryDuration = totalDuration;
            }

            NotifyOfPropertyChange(nameof(StorageEngineEvents));
        }

        private bool IsDaxStudioInternalQuery()
        {
            var endEvent = Events.FirstOrDefault(e => e.EventClass == DaxStudioTraceEventClass.QueryEnd);
            return endEvent != null && endEvent.TextData.Contains(Constants.InternalQueryHeader);
        }

        // This function assumes that the events arrive in StartTime order, then we check if
        // the start/end times of the current event overlap with the end time of the previous
        // event with the latest end time.
        private void UpdateForParallelOperations(ref DaxStudioTraceEventArgs maxEvent,  DaxStudioTraceEventArgs traceEvent)
        {
            if (maxEvent == null)
            {
                maxEvent = traceEvent;
                return;
            }

            // Any difference below 3 ms is ignored because it could be caused by the 
            // time fix for events that have 0ms of duration but present an effective time (EndTime-StartTime)
            var overlapEventsMs = (maxEvent.EndTime - traceEvent.StartTime).TotalMilliseconds;
            if (overlapEventsMs > 0)
            {
                // Display warning only when the overlap is greater than 10 ms
                ParallelStorageEngineEventsDetected = (overlapEventsMs > 10);

                if (maxEvent.EndTime > traceEvent.EndTime)
                {
                    // fully overlapped
                    traceEvent.NetParallelDuration = 0;
                }
                else
                {
                    traceEvent.NetParallelDuration = (long)(traceEvent.EndTime - maxEvent.EndTime).TotalMilliseconds;
                    maxEvent = traceEvent;
                }
            }
            else
            {
                maxEvent = traceEvent;
            }
        }

        public DateTime QueryEndDateTime { get; set; }
        public DateTime QueryStartDateTime { get; set; }

        private long _totalCpuDuration;
        public long TotalCpuDuration
        {
            get { return _totalCpuDuration; }
            set
            {
                _totalCpuDuration = value;
                NotifyOfPropertyChange(() => TotalCpuDuration);
                NotifyOfPropertyChange(() => TotalCpuFactor);
            }
        }

        private long _totalDirectQueryDuration;
        public long TotalDirectQueryDuration
        {
            get { return _totalDirectQueryDuration; }
            set
            {
                _totalDirectQueryDuration = value;
                NotifyOfPropertyChange(() => TotalDirectQueryDuration);
            }
        }


        public double TotalCpuFactor
        {
            get { return (double)_totalCpuDuration / (double)_totalDuration; }
        }

        public double StorageEngineCpuFactor
        {
            get { return _storageEngineDuration == 0 ? 0 : (double)_storageEngineCpu / (double)_storageEngineDuration; }
        }
        public double StorageEngineDurationPercentage
        {
            get
            {
                return TotalDuration == 0 ? 0 : (double)StorageEngineNetParallelDuration / (double)TotalDuration;
            }
        }
        public double FormulaEngineDurationPercentage
        {
            // marcorusso: we use the formula engine total provided by Query End event in CPU Time
            // get { return TotalDuration == 0 ? 0:(TotalDuration-StorageEngineDuration)/TotalDuration;}
            get
            {
                return TotalDuration == 0 ? 0 : (double)FormulaEngineDuration / (double)TotalDuration;
            }
        }
        public double VertipaqCacheMatchesPercentage
        {
            get
            {
                return StorageEngineQueryCount == 0 ? 0 : (double)VertipaqCacheMatches / (double)StorageEngineQueryCount;
            }
        }
        private long _totalDuration;
        public long TotalDuration
        {
            get { return _totalDuration; }
            private set
            {
                _totalDuration = value;
                //NotifyOfPropertyChange(() => TotalDuration);
                //NotifyOfPropertyChange(() => StorageEngineDurationPercentage);
                //NotifyOfPropertyChange(() => FormulaEngineDurationPercentage);
                //NotifyOfPropertyChange(() => VertipaqCacheMatchesPercentage);
                //NotifyOfPropertyChange(() => TotalCpuFactor);
            }
        }
        private long _formulaEngineDuration;
        public long FormulaEngineDuration
        {
            get { return _formulaEngineDuration; }
            private set
            {
                _formulaEngineDuration = value;
                //NotifyOfPropertyChange(() => FormulaEngineDuration);
                //NotifyOfPropertyChange(() => FormulaEngineDurationPercentage);
            }
        }
        private long _storageEngineDuration;
        public long StorageEngineDuration
        {
            get { return _storageEngineDuration; }
            private set
            {
                _storageEngineDuration = value;
                //NotifyOfPropertyChange(() => StorageEngineDuration);
                //NotifyOfPropertyChange(() => StorageEngineDurationPercentage);
                //NotifyOfPropertyChange(() => StorageEngineCpuFactor);
            }
        }

        private long _storageEngineNetParallelDuration;
        public long StorageEngineNetParallelDuration
        {
            get { return _storageEngineNetParallelDuration; }
            private set
            {
                _storageEngineNetParallelDuration = value;
                NotifyOfPropertyChange(() => StorageEngineNetParallelDuration);
            }
        }

        private long _storageEngineCpu;
        public long StorageEngineCpu
        {
            get { return _storageEngineCpu; }
            private set
            {
                _storageEngineCpu = value;
                //NotifyOfPropertyChange(() => StorageEngineCpu);
                //NotifyOfPropertyChange(() => StorageEngineCpuFactor);
            }
        }
        private long _storageEngineQueryCount;
        public long StorageEngineQueryCount
        {
            get { return _storageEngineQueryCount; }
            private set
            {
                _storageEngineQueryCount = value;
                //NotifyOfPropertyChange(() => StorageEngineQueryCount);
            }
        }

        private int _vertipaqCacheMatches;
        public int VertipaqCacheMatches
        {
            get { return _vertipaqCacheMatches; }
            set
            {
                _vertipaqCacheMatches = value;
                //NotifyOfPropertyChange(() => VertipaqCacheMatches);
                //NotifyOfPropertyChange(() => VertipaqCacheMatchesPercentage);
            }
        }

        /// <summary>
        /// List of all the storage engine events 
        /// Reserved for internal use, access should be limited to initialization only
        /// </summary>
        private readonly BindableCollection<TraceStorageEngineEvent> _storageEngineEvents;

        /// <summary>
        /// Access all the storage engine events without any filter
        /// </summary>
        protected IObservableCollection<TraceStorageEngineEvent> AllStorageEngineEvents
        {
            get { return _storageEngineEvents; }
        }

        /// <summary>
        /// Access the storage engine events that are visible according to the filters applied to the visualization
        /// </summary>
        public IObservableCollection<TraceStorageEngineEvent> StorageEngineEvents
        {
            get
            {
                var fse = from e in AllStorageEngineEvents
                          where
                              (e.ClassSubclass.Subclass == DaxStudioTraceEventSubclass.VertiPaqScanInternal && ServerTimingDetails.ShowInternal)
                              ||
                              (e.ClassSubclass.Subclass == DaxStudioTraceEventSubclass.BatchVertiPaqScan && ServerTimingDetails.ShowBatch)
                              ||
                              (e.ClassSubclass.Subclass == DaxStudioTraceEventSubclass.VertiPaqCacheExactMatch && ServerTimingDetails.ShowCache)
                              ||
                              ((e.ClassSubclass.Subclass != DaxStudioTraceEventSubclass.VertiPaqCacheExactMatch
                                  && e.ClassSubclass.Subclass != DaxStudioTraceEventSubclass.VertiPaqScanInternal
                                  && e.ClassSubclass.Subclass != DaxStudioTraceEventSubclass.BatchVertiPaqScan
                                  && e.ClassSubclass.Subclass != DaxStudioTraceEventSubclass.NotAvailable
                               ) && ServerTimingDetails.ShowScan)
                              ||
                              (e.ClassSubclass.Subclass == DaxStudioTraceEventSubclass.RewriteAttempted && ServerTimingDetails.ShowRewriteAttempts)
                              || 
                              (e.ClassSubclass.Class == DaxStudioTraceEventClass.Total)
                              ||
                              (e.ClassSubclass.Class == DaxStudioTraceEventClass.ExecutionMetrics && ServerTimingDetails.ShowMetrics)
                          select e;
                return new BindableCollection<TraceStorageEngineEvent>(fse);
            }
        }

        public IEnumerable<TraceStorageEngineEvent> CollapseEvents(IEnumerable<TraceStorageEngineEvent> events)
        {
            var listItems = events.ToList();
            var listRemove = new List<TraceStorageEngineEvent>();
            bool restartLoop = true;
            while (listItems.Count > 0 && restartLoop)
            {
                restartLoop = false;
                for (int itemIndex = 0; itemIndex < listItems.Count; itemIndex++)
                {
                    var item = listItems.ElementAt(itemIndex);
                    listRemove.Clear();
                    // Search contiguous
                    for (int i = 0; i < listItems.Count; i++)
                    {
                        var candidate = listItems.ElementAt(i);
                        if (candidate != item)
                        {
                            if (candidate.StartTime >= item.StartTime && candidate.StartTime <= item.EndTime)
                            {
                                item.EndTime = candidate.EndTime > item.EndTime ? candidate.EndTime : item.EndTime;
                                listRemove.Add(candidate);
                            }
                        }
                    }
                    if (listRemove.Count > 0)
                    {
                        listRemove.ForEach(r => listItems.Remove(r));
                        restartLoop = true;
                        break;
                    }
                }
            }
            return listItems;
        }

        private TraceStorageEngineEvent _selectedEvent;
        public TraceStorageEngineEvent SelectedEvent
        {
            get
            {
                return _selectedEvent;
            }
            set
            {
                _selectedEvent = value;
                IsSEQuery = !(_selectedEvent is RewriteTraceEngineEvent || _selectedEvent is ExecutionMetricsTraceEngineEvent);
                NotifyOfPropertyChange(() => SelectedEvent);
            }
        }

        private bool _isSEQuery;
        public bool IsSEQuery { get => _isSEQuery;
            set { 
                _isSEQuery = value;
                NotifyOfPropertyChange();
            } 
        }

        // IToolWindow interface
        public override string Title => "Server Timings";
        public override string ContentId => "server-timings-trace";
        public override string TraceSuffix => "timings";
        public override string ImageResource => "server_timingsDrawingImage";
        public override string KeyTip => "ST";
        public override int SortOrder => 30;

        public override string ToolTipText => "Runs a server trace to record detailed timing information for performance profiling";

        public override void OnReset()
        {
            IsBusy = false;
            ToggleScrollLeft();
            ClearAll();
            Events.Clear();
        //    ProcessResults();
        }

        #region ISaveState methods
        void ISaveState.Save(string filename)
        {
            string json = GetJson();
            File.WriteAllText(filename + ".serverTimings", json);

        }

        public bool ScrollLeft { get; set; }

        public void ToggleScrollLeft()
        {
            ScrollLeft = !ScrollLeft;
            NotifyOfPropertyChange(nameof(ScrollLeft));
        }

        public void SavePackage(Package package)
        {

            Uri uriTom = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.ServerTimings, UriKind.Relative));
            using (TextWriter tw = new StreamWriter(package.CreatePart(uriTom, "application/json", CompressionOption.Maximum).GetStream(), Encoding.UTF8))
            {
                tw.Write(GetJson());
                tw.Close();
            }
        }

        public void LoadPackage(Package package)
        {
            var uri = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.ServerTimings, UriKind.Relative));
            if (!package.PartExists(uri)) return;
            _eventAggregator.PublishOnUIThreadAsync(new ShowTraceWindowEvent(this));
            var part = package.GetPart(uri);
            using (TextReader tr = new StreamReader(part.GetStream()))
            {
                string data = tr.ReadToEnd();
                LoadJson(data);
            }

        }

        public string GetJson()
        {
            var m = new ServerTimesModel()
            {
                FormulaEngineDuration = this.FormulaEngineDuration,
                StorageEngineDuration = this.StorageEngineDuration,
                StorageEngineNetParallelDuration = this.StorageEngineNetParallelDuration,
                StorageEngineCpu = this.StorageEngineCpu,
                TotalDuration = this.TotalDuration,
                VertipaqCacheMatches = this.VertipaqCacheMatches,
                StorageEngineQueryCount = this.StorageEngineQueryCount,
                StorageEngineEvents = this._storageEngineEvents,
                TotalCpuDuration = this.TotalCpuDuration,
                TotalDirectQueryDuration = this.TotalDirectQueryDuration,
                QueryEndDateTime = this.QueryEndDateTime,
                QueryStartDateTime = this.QueryStartDateTime,
                Parameters = this.Parameters,
                CommandText = this.CommandText,
                ParallelStorageEngineEventsDetected = this.ParallelStorageEngineEventsDetected,
                ActivityID = this.ActivityID,
                TimelineTotalDuration = this.TimelineTotalDuration
            };
            var json = JsonConvert.SerializeObject(m, Formatting.None, new JsonSerializerSettings() { DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate});
            return json;
        }

        void ISaveState.Load(string filename)
        {
            filename = filename + ".serverTimings";
            if (!File.Exists(filename)) return;

            _eventAggregator.PublishOnUIThreadAsync(new ShowTraceWindowEvent(this));
            string data = File.ReadAllText(filename);

            LoadJson(data);
        }

        public void LoadJson(string data)
        {
            var eventConverter = new ServerTimingConverter();
            var deseralizeSettings = new JsonSerializerSettings();
            deseralizeSettings.Converters.Add(eventConverter);
            deseralizeSettings.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple;
            deseralizeSettings.TypeNameHandling = TypeNameHandling.Auto;

            ServerTimesModel m = JsonConvert.DeserializeObject<ServerTimesModel>(data, deseralizeSettings);

            ActivityID = m.ActivityID;
            FormulaEngineDuration = m.FormulaEngineDuration;
            StorageEngineDuration = m.StorageEngineDuration;
            StorageEngineNetParallelDuration = m.StorageEngineNetParallelDuration;
            StorageEngineCpu = m.StorageEngineCpu;
            TotalDuration = m.TotalDuration;
            VertipaqCacheMatches = m.VertipaqCacheMatches;
            StorageEngineQueryCount = m.StorageEngineQueryCount;
            TotalCpuDuration = m.TotalCpuDuration;
            TotalDirectQueryDuration = m.TotalDirectQueryDuration;
            QueryEndDateTime = m.QueryEndDateTime;
            QueryStartDateTime = m.QueryStartDateTime;
            Parameters = m.Parameters;
            CommandText = m.CommandText;
            ParallelStorageEngineEventsDetected = m.ParallelStorageEngineEventsDetected;
            TimelineTotalDuration = m.TimelineTotalDuration;
            AllStorageEngineEvents.Clear();
            if (m.StoreageEngineEvents != null)
                AllStorageEngineEvents.AddRange(m.StoreageEngineEvents);
            else
                AllStorageEngineEvents.AddRange(m.StorageEngineEvents);

            AllStorageEngineEvents.Apply(se => { 
                se.HighlightQuery = se.QueryRichText?.Contains("|~S~|") ?? false;
                if (se.Class == DaxStudioTraceEventClass.DirectQueryEnd) { se.QueryRichText = SqlFormatter.FormatSql(se.TextData); }
            });
            // update timeline total Duration if this is an older file format
            if (m.FileFormatVersion <= 4) {
                AllStorageEngineEvents.Apply(se => UpdateTimelineTotalDuration(new DaxStudioTraceEventArgs(se.Class.ToString(), se.Subclass.ToString(), se.Duration ?? 0, se.CpuTime ?? 0, se.Query, string.Empty, se.StartTime)));
                UpdateTimelineDurations(QueryStartDateTime, QueryEndDateTime, TimelineTotalDuration);
            }
        }


        #endregion


        #region Properties to handle layout changes

        public int TextGridRow { get { return ServerTimingDetails?.LayoutBottom ?? false ? 4 : 2; } }
        public int TextGridRowSpan { get { return ServerTimingDetails?.LayoutBottom ?? false ? 1 : 3; } }
        public int TextGridColumnSpan { get { return ServerTimingDetails?.LayoutBottom ?? false ? 3 : 1; } }
        public int TextGridColumn { get { return ServerTimingDetails?.LayoutBottom ?? false ? 2 : 4; } }

        public GridLength TextColumnWidth { get { return ServerTimingDetails?.LayoutBottom ?? false ? new GridLength(0, GridUnitType.Pixel) : new GridLength(1, GridUnitType.Star); } }

        private ServerTimingDetailsViewModel _serverTimingDetails;
        public ServerTimingDetailsViewModel ServerTimingDetails
        {
            get { return _serverTimingDetails; }
            set
            {
                if (_serverTimingDetails != null) { _serverTimingDetails.PropertyChanged -= ServerTimingDetails_PropertyChanged; }
                _serverTimingDetails = value;
                _serverTimingDetails.PropertyChanged += ServerTimingDetails_PropertyChanged;
                NotifyOfPropertyChange(() => ServerTimingDetails);
            }
        }

        public override bool FilterForCurrentSession
        {
            get
            {
                return true;
            }
        }

        protected override bool IsFinalEvent(DaxStudioTraceEventArgs traceEvent)
        {
            return traceEvent.EventClass == DaxStudioTraceEventClass.QueryEnd ||
                   traceEvent.EventClass == DaxStudioTraceEventClass.Error;
        }
        private void ServerTimingDetails_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "LayoutBottom":
                case "LayoutRight":
                    NotifyOfPropertyChange(() => TextGridColumn);
                    NotifyOfPropertyChange(() => TextGridRow);
                    NotifyOfPropertyChange(() => TextGridRowSpan);
                    NotifyOfPropertyChange(() => TextGridColumnSpan);
                    NotifyOfPropertyChange(() => TextColumnWidth);
                    break;
                case "ShowScan":
                case "ShowBatch":
                case "ShowCache":
                case "ShowInternal":
                case "ShowMetrics":
                    NotifyOfPropertyChange(nameof(StorageEngineEvents));
                    break;
            }
        }

        #endregion

        #region Title Bar Button Methods

        

        public override void ClearAll()
        {
            FormulaEngineDuration = 0;
            StorageEngineDuration = 0;
            StorageEngineNetParallelDuration = 0;
            TotalDirectQueryDuration = 0;
            StorageEngineCpu = 0;
            StorageEngineQueryCount = 0;
            VertipaqCacheMatches = 0;
            TotalDuration = 0;
            TimelineTotalDuration= 0;
            ParallelStorageEngineEventsDetected = false;
            StorageEventHeatmap = null;
            AllStorageEngineEvents.Clear();
            NotifyOfPropertyChange(nameof(AllStorageEngineEvents));
            NotifyOfPropertyChange(nameof(StorageEngineEvents));
            NotifyOfPropertyChange(nameof(CanExport));
            NotifyOfPropertyChange(nameof(CanShowTraceDiagnostics));
        }
        
        public void Copy()
        {
            Log.Warning("Copy not implemented for ServerTimesViewModel");
        }
        public override void CopyEventContent()
        {
            Log.Warning("CopyEventContent not implemented for ServerTimesViewModel");
        }
        public override void CopyAll()
        {
            Log.Warning("CopyAll Method not implemented for ServerTimesViewModel");
        }
        #endregion

        public bool IsCopyResultsForCommentsVisible => Options.ShowCopyMetricsComments;
        public bool IsCopyResultsForCommentsDataVisible => Options.ShowCopyMetricsComments;

        public override bool CanCopyResults => CanExport;
        public override bool IsCopyResultsVisible => true;
        public override void CopyResults()
        {
            CopyResultsData(true);
        }

        public void CopyResultsData()
        {
            CopyResultsData(false);
        }
        public string CopyResultsForComments()
        {
            return CopyResultsData(includeHeader:true, formatTextForComment: true);
        }
        public string CopyResultsForCommentsData()
        {
            return CopyResultsData(includeHeader:false, formatTextForComment: true);
        }
        public string CopyResultsData(bool includeHeader, bool formatTextForComment=false)
        {
            var dataObject = new DataObject();
            var headers = string.Empty;
            if (includeHeader) headers = "Query End\tTotal\tFE\tSE\tSE CPU\tSE Par.\tSE Queries\tSE Cache\n";
            var values = $"{QueryEndDateTime.ToString(Constants.IsoDateFormatPaste)}\t{TotalDuration}\t{FormulaEngineDuration}\t{StorageEngineDuration}\t{StorageEngineCpu}\t{StorageEngineCpuFactor}\t{StorageEngineQueryCount}\t{VertipaqCacheMatches}";
            string result = $"{headers}{values}";
            dataObject.SetData(DataFormats.StringFormat, result);
            dataObject.SetData(DataFormats.CommaSeparatedValue, $"{headers.Replace("\t", CultureInfo.CurrentCulture.TextInfo.ListSeparator)}\n{values.Replace("\t", CultureInfo.CurrentCulture.TextInfo.ListSeparator)}");
            if (formatTextForComment)
            {
                var textHeader = includeHeader ? PasteServerTimingsEvent.SERVERTIMINGS_HEADER : string.Empty;
                var textValues = $"-- {TotalDuration,9:#,0}  {FormulaEngineDuration,9:#,0}  {StorageEngineDuration,9:#,0}  {StorageEngineCpu,10:#,0}  x{StorageEngineCpuFactor,4:0.0}";
                result = $"{textHeader}{(string.IsNullOrEmpty(textHeader) ? string.Empty : "\r\n")}{textValues}\r\n";
                dataObject.SetData(DataFormats.Text, result);
            }
            Clipboard.SetDataObject(dataObject);
            return result;
        }

        public override bool CanExport => AllStorageEngineEvents.Count > 0;
        public override void ExportTraceDetails(string filePath)
        {
            File.WriteAllText(filePath, GetJson());
        }

        public void ExportDetails()
        {
            if (Options.ExportServerTimingDetailsToFolder)
            {

                var dialog = new System.Windows.Forms.FolderBrowserDialog();
                dialog.Description = "A file per storage event will be created in the selected folder.";

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) ExportxmSqlFiles(dialog.SelectedPath);
            }
            else
            {
                Export();
            }
        }

        public void ExportxmSqlFiles(string folderPath)
        {

            foreach (var evt in StorageEngineEvents)
            {
                if (evt == null) continue;
                if (evt is TraceStorageEngineEvent tse)
                {
                    var fileName = $"{tse.RowNumber:0000}_{tse.StartTime:yyyyMMddThhmmss-ffff}_{tse.Subclass}.{tse.ClassSubclass.QueryLanguage.ToString().ToLower(System.Globalization.CultureInfo.InvariantCulture)}";
                    var filePath = Path.Combine(folderPath, fileName);
                    File.WriteAllText(filePath, StripHighlighCodes(tse.QueryRichText));
                }
            }
        }

        private Regex regexStripHighlightCodes = new Regex("\\|~\\w~\\|", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);
        private string StripHighlighCodes(string queryRichText)
        {
            return regexStripHighlightCodes.Replace(queryRichText, string.Empty);
        }

        public bool CanShowTraceDiagnostics => AllStorageEngineEvents.Count > 0;

        private string _activityId = string.Empty;
        public string ActivityID { get => _activityId;
            set
            {
                _activityId = value;
                NotifyOfPropertyChange();
            }
        }

        public DateTime StartDatetime { get => QueryStartDateTime; }
        public string CommandText { get; set; }
        public string Parameters { get; set; }
        public long TimelineTotalDuration { get; private set; }

        public void CopySEQuery()
        {
            if (SelectedEvent == null)
            {
                Log.Debug("SelectedEvent is null on CopySEQuery");
                // TODO we should provide a visual notification that copy did not work because of missing selection
                return;
            }
            try
            {
                var view = GetView() as ServerTimesView;
                var details = view.EventDetails;
                var rt = details.FindChild("QueryRichText", typeof(BindableRichTextBox)) as BindableRichTextBox;

                if (rt == null)
                {
                    // if we don't have a rich text control in the current view then 
                    // just grab the TextData
                    Clipboard.SetText(SelectedEvent.TextData);
                    return;
                }

                // Remove initial SET DC_KIND line
                TextPointer secondLine = rt.Document.ContentStart.GetNextInsertionPosition(LogicalDirection.Forward).GetLineStartPosition(1);
                string firstLineContent = (secondLine != null) ? new TextRange(rt.Document.ContentStart, secondLine).Text : null;

                TextPointer startSelection = (firstLineContent != null) && firstLineContent.StartsWith("SET DC_KIND", StringComparison.InvariantCulture)
                    ? secondLine
                    : rt.Document.ContentStart;

                // Remove last empty lines and Estimated size
                TextPointer endSelection;
                TextPointer lastLine = null;
                string lastLineContent;
                int index = 0;
                do
                {
                    endSelection = lastLine ?? rt.Document.ContentEnd;
                    lastLine = rt.Document.ContentEnd.GetNextInsertionPosition(LogicalDirection.Backward).GetLineStartPosition(--index);
                    lastLineContent = (lastLine != null) ? new TextRange(lastLine, endSelection).Text : null;
                } while (lastLineContent != null
                            && (lastLineContent.StartsWith("Estimated", StringComparison.InvariantCulture) || lastLineContent == "\r\n"));
                Console.WriteLine(lastLineContent);

                // Remove the last CRLF from selection
                if (lastLineContent.EndsWith("\r\n", StringComparison.InvariantCulture))
                {
                    endSelection = endSelection.GetPositionAtOffset(-2) ?? endSelection;
                }

                rt.Selection.Select(startSelection, endSelection);
                rt.Copy();
                rt.Selection.Select(rt.Document.ContentStart.GetLineStartPosition(0), rt.Document.ContentStart.GetLineStartPosition(0));
            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(ServerTimesViewModel), nameof(CopySEQuery), "Error copying SE Query text");
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error copying SE query text\n{ex.Message}"));
            }
            return;
        }

        public async void ShowTraceDiagnostics()
        {
            var traceDiagnosticsViewModel = new RequestInformationViewModel(this);
            await WindowManager.ShowDialogBoxAsync(traceDiagnosticsViewModel, settings: new Dictionary<string, object>
            {
                { "WindowStyle", WindowStyle.None},
                { "ShowInTaskbar", false},
                { "ResizeMode", ResizeMode.NoResize},
                { "Background", Brushes.Transparent},
                { "AllowsTransparency",true}

            });
        }

        public bool ShowTimelineOnRows { get => this.StorageEventTimelineStyle != StorageEventTimelineStyle.None; }

        private StorageEventTimelineStyle _storageEventTimelineStyle;
        public StorageEventTimelineStyle StorageEventTimelineStyle { get => _storageEventTimelineStyle;
            set
            {
                _storageEventTimelineStyle = value;
                NotifyOfPropertyChange(nameof(StorageEventTimelineStyle));
                NotifyOfPropertyChange(nameof(ShowTimelineOnRows));
                NotifyOfPropertyChange(nameof(StorageEventHeatmapHeight));
                NotifyOfPropertyChange(nameof(TimelineVerticalMargin));
            }
                
        }

        public void SetTimelineOnRowsVisibility(StorageEventTimelineStyle style)
        {
            this.StorageEventTimelineStyle = style;

            NotifyOfPropertyChange(nameof(ShowTimelineOnRows));
            NotifyOfPropertyChange(nameof(StorageEventTimelineStyle));
            NotifyOfPropertyChange(nameof(StorageEventHeatmapHeight));
            NotifyOfPropertyChange(nameof(TimelineVerticalMargin));
        }

        public Task HandleAsync(CopySEQueryEvent message, CancellationToken cancellationToken)
        {
            CopySEQuery();
            return Task.CompletedTask;
        }
        public Task HandleAsync(CopyPasteServerTimingsEvent message, CancellationToken cancellationToken)
        {
            string textResult;
            if (message.IncludeHeader)
            {
                textResult = CopyResultsForComments();
            }
            else
            {
                textResult = CopyResultsForCommentsData();
            }
            _eventAggregator.PublishOnUIThreadAsync(new PasteServerTimingsEvent(message.IncludeHeader, textResult), cancellationToken);
            return Task.CompletedTask;
        }


        public Task HandleAsync(ThemeChangedEvent message, CancellationToken cancellationToken)
        {
            StorageEventHeatmap = null;
            NotifyOfPropertyChange(nameof(StorageEventHeatmap));
            return Task.CompletedTask;
        }

        private ImageSource _storageEventHeatmap;
        public ImageSource StorageEventHeatmap { 
            get {
                // if we have a cached image return that
                if (_storageEventHeatmap != null) return _storageEventHeatmap;
                // if there are no events return an empty image
                if (this.StorageEngineEvents.Count == 0) return new DrawingImage();

                var element = (FrameworkElement)this.GetView();

                Brush scanBrush = (Brush)element.FindResource("Theme.Brush.Accent");
                Brush feBrush = (Brush)element.FindResource("Theme.Brush.Accent2");
                Brush batchBrush =  (Brush)element.FindResource("Theme.Brush.Accent1");
                Brush internalBrush = (Brush)element.FindResource("Theme.Brush.Accent3");

                //_storageEventHeatmap = TimelineHeatmapImageGenerator.GenerateVector(this.StorageEngineEvents.ToList(), 500, 10, feBrush, scanBrush, batchBrush, internalBrush  );
                _storageEventHeatmap = TimelineHeatmapImageGenerator.GenerateBitmap(this.StorageEngineEvents.ToList(), 5000, 10, feBrush, scanBrush, batchBrush, internalBrush);
#if DEBUG
                //TODO - remove debug code
                using (StreamWriter writer = File.CreateText("c:\\temp\\heatmap.xaml"))
                {
                    XamlWriter.Save(_storageEventHeatmap, writer);
                }
#endif                    
                return _storageEventHeatmap;
            }
            set {
                _storageEventHeatmap = value;
                NotifyOfPropertyChange();
            } 
        }

        public double StorageEventHeatmapHeight { get 
            {
                switch (this.StorageEventTimelineStyle) {
                    case DaxStudio.Interfaces.Enums.StorageEventTimelineStyle.Thin: return 8.0;
                    case DaxStudio.Interfaces.Enums.StorageEventTimelineStyle.FullHeight: return 24.0;
                    default: return 12.0; 
                }; 
            } 
        }

        public double TimelineVerticalMargin
        {
            get
            {
                switch (this.StorageEventTimelineStyle)
                {
                    case DaxStudio.Interfaces.Enums.StorageEventTimelineStyle.Thin: return 6.0;
                    case DaxStudio.Interfaces.Enums.StorageEventTimelineStyle.FullHeight: return 6.0;
                    default: return 6.0;
                };
            }
        }

        public bool HasData => TotalDuration > 0 || StorageEngineEvents?.Count > 0;
    }
}
