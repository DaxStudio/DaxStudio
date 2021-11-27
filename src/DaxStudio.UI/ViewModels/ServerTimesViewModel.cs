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

namespace DaxStudio.UI.ViewModels
{

    public class TraceStorageEngineEvent {
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
        public string Query { get; set; }
        private DaxStudioTraceEventClassSubclass.Language GetQueryLanguage()
        {
            if (this.Class == DaxStudioTraceEventClass.DirectQueryBegin || this.Class == DaxStudioTraceEventClass.DirectQueryEnd)
            {
                if (Query.StartsWith("DEFINE",StringComparison.InvariantCultureIgnoreCase)
                    || Query.StartsWith("EVALUATE", StringComparison.InvariantCultureIgnoreCase))
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
                    case DaxStudioTraceEventSubclass.VertiPaq:
                    case DaxStudioTraceEventSubclass.VertiPaqCacheExactMatch:
                    case DaxStudioTraceEventSubclass.VertiPaqCacheNotFound:
                    case DaxStudioTraceEventSubclass.VertiPaqCacheProbe:
                    case DaxStudioTraceEventSubclass.VertiPaqScan:
                    case DaxStudioTraceEventSubclass.VertiPaqScanInternal:
                    case DaxStudioTraceEventSubclass.VertiPaqScanLocal:
                    case DaxStudioTraceEventSubclass.VertiPaqScanQueryPlan:
                    case DaxStudioTraceEventSubclass.VertiPaqScanRemote:
                        return DaxStudioTraceEventClassSubclass.Language.xmSQL;
                }
            }
            return DaxStudioTraceEventClassSubclass.Language.Unknown;
        }

        public long? Duration { get; set; }
        public long? CpuTime { get; set; }
        public double? CpuFactor { get; set; }
        public int RowNumber { get; set; }
        public long? EstimatedRows { get; set; }
        public long? EstimatedKBytes { get; set; }
        public bool HighlightQuery { get; set; }
        public bool InternalBatchEvent { get; set; }

        // String that highlight important parts of the query
        // Currently implement only the strong (~E~/~S~) for the following functions:
        // - CallbackDataID
        // - LogAbsValueCallback
        // - RoundValueCallback
        // - EncodeCallback
        // - MinMaxColumnPositionCallback
        //
        // We probably need to cover also the function "Cond", which is a callback without having the callback name
        // But we need to collect use cases to find a way to avoid making mistakes with any expression using "Cond"
        private string _queryRichText = "";
        public string QueryRichText {
            set {
                if (Options.HighlightXmSqlCallbacks)
                {
                    var sb = new StringBuilder(value);
                    sb.Replace("CallbackDataID", "|~S~|CallbackDataID|~E~|");
                    sb.Replace("LogAbsValueCallback", "|~S~|LogAbsValueCallback|~E~|");
                    sb.Replace("RoundValueCallback", "|~S~|RoundValueCallback|~E~|");
                    sb.Replace("EncodeCallback", "|~S~|EncodeCallback|~E~|");
                    sb.Replace("MinMaxColumnPositionCallback", "|~S~|MinMaxColumnPositionCallback|~E~|");
                    _queryRichText = sb.ToString();
                }
                else
                {
                    _queryRichText = value;
                }
            }

            get => _queryRichText;
        }

        private IGlobalOptions _options;
        protected IGlobalOptions Options { get {
                if (_options == null) _options = IoC.Get<IGlobalOptions>();
                return _options;
            }
            private set { _options = value; }
        }

        public TraceStorageEngineEvent(DaxStudioTraceEventArgs ev, int rowNumber, IGlobalOptions options, Dictionary<string, string> remapColumns, Dictionary<string, string> remapTables)
        {
            Options = options;

            RowNumber = rowNumber;
            Class = ev.EventClass;
            Subclass = ev.EventSubclass;
            InternalBatchEvent = ev.InternalBatchEvent;
            switch (Class)
            {
                //case DaxStudioTraceEventClass.DirectQueryEnd:
                //    Query = ev.TextData;
                //    break;
                case DaxStudioTraceEventClass.AggregateTableRewriteQuery:
                    // the rewrite event does not have a query or storage engine timings
                    break;
                case DaxStudioTraceEventClass.DirectQueryBegin:
                case DaxStudioTraceEventClass.DirectQueryEnd:
                    // Don't process DirectQuery text
                    Query = ev.TextData;
                    QueryRichText = Query;
                    break;
                default:
                    // Replace column names
                    string queryRemapped = Options.ReplaceXmSqlColumnNames ? ev.TextData.ReplaceTableOrColumnNames( remapColumns ) : ev.TextData;
                    // replace table names
                    queryRemapped = Options.ReplaceXmSqlTableNames ? queryRemapped.ReplaceTableOrColumnNames( remapTables ) : queryRemapped;

                    Query = Options.SimplifyXmSqlSyntax ? queryRemapped.RemoveDaxGuids().RemoveXmSqlSquareBrackets().RemoveAlias().RemoveLineage().FixEmptyArguments().RemoveRowNumberGuid().RemovePremiumTags() : queryRemapped;
                    QueryRichText = Query;
                    // Set flag in case any highlight is present
                    HighlightQuery = QueryRichText.Contains("|~E~|");
                    break;
            }
            
            // Skip Duration/Cpu Time for Cache Match
            if (ClassSubclass.Subclass != DaxStudioTraceEventSubclass.VertiPaqCacheExactMatch)
            {
                Duration = ev.Duration;
                if (ClassSubclass.Subclass != DaxStudioTraceEventSubclass.RewriteAttempted)
                {
                    CpuTime = ev.CpuTime;
                    CpuFactor = ev.CpuFactor;
                }
            }
            if (Query != null && Query?.Length > 0)
            {
                long rows, bytes;
                if (Query.ExtractEstimatedSize(out rows, out bytes))
                {
                    EstimatedRows = rows;
                    EstimatedKBytes = 1 + bytes / 1024;
                }
            }
        }
        public TraceStorageEngineEvent() { }
    }

    public class RewriteTraceEngineEvent : TraceStorageEngineEvent
    {

        public RewriteTraceEngineEvent() { }

        public RewriteTraceEngineEvent(DaxStudioTraceEventArgs ev, int rowNumber, IGlobalOptions options, Dictionary<string, string> remapColumns, Dictionary<string, string> remapTables) : base(ev, rowNumber, options, remapColumns, remapTables) {
            TextData = ev.TextData;
        }
        
        public string Table { get; set; }
        public string MatchingResult { get; set; }
        public string Mapping { get; set; }
        private string _textData;
        public string TextData { get { return _textData; } set {
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

    }

    public static class TraceStorageEngineExtensions {
        const string searchGuid = @"([_-]\{?([0-9a-fA-F]){8}-([0-9a-fA-F]){4}-([0-9a-fA-F]){4}-([0-9a-fA-F]){4}-([0-9a-fA-F]){12}\}?)";
        //const string searchXmSqlSquareBracketsNoSpace = @"(?<![\.'])\[([^\[^ ])*\]";
        const string searchXmSqlSquareBracketsWithSpace = @"(?<![\.0-9a-zA-Z'])\[([^\[])*\]";
        const string searchXmSqlDotSeparator = @"\.\[";
        const string searchXmSqlParenthesis = @"\ *[\(\)]\ *";
        const string searchXmSqlAlias = @" AS[\r\n\t\s]?\'[^\']*\'";
        // const string searchXmSqlLineage = @" \( [0-9]+ \) ";
        const string searchXmSqlLineageBracket = @" \( [0-9]+ \) \]";
        const string searchXmSqlLineageQuoted = @" \( [0-9]+ \) \'";
        const string searchXmSqlLineageDollar = @" \( [0-9]+ \) \$";
        const string searchXmSqlEmptyArguments = @" \(\s*\) ";
        const string searchXmSqlRowNumberGuidBracket = @"\[RowNumber [0-9A-F ]*\]";
        const string searchXmSqlRowNumberGuidQuoted = @"\$RowNumber [0-9A-F ]*\'";
        const string seachXmSqlPremiumTags = @"<pii>|</pii>";

        const string searchXmSqlPatternSize = @"Estimated size .* : (?<rows>\d+), (?<bytes>\d+)";

        //const string searchDaxQueryPlanSquareBrackets = @"^\'\[([^\[^ ])*\]";
        //const string searchQuotedIdentifiers = @"\'([^ ])*\'";

        static Regex guidRemoval = new Regex(searchGuid, RegexOptions.Compiled);
        static Regex xmSqlSquareBracketsWithSpaceRemoval = new Regex(searchXmSqlSquareBracketsWithSpace, RegexOptions.Compiled);
        //static Regex xmSqlSquareBracketsNoSpaceRemoval = new Regex(searchXmSqlSquareBracketsNoSpace, RegexOptions.Compiled);
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

        public static string RemoveDaxGuids(this string daxQuery) {
            return guidRemoval.Replace(daxQuery, "");
        }
        private static string RemoveSquareBracketsWithSpace(Match match) {
            return match.Value.Replace("[", "'").Replace("]", "'");
        }
        private static string RemoveSquareBracketsNoSpace(Match match) {
            return match.Value.Replace("[", "").Replace("]", "");
        }
        private static string FixSpaceParenthesis(Match match) {
            string parenthesis = match.Value.Trim();
            return " " + parenthesis + " ";
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
        public static string RemoveXmSqlSquareBrackets(this string daxQuery) {
            // Reviewed on 2017-10-13
            // The first removal should be useless and I commented it.
            // Code was originally written on a plane offline... 
            // string daxQueryNoBracketsOnTableNames = xmSqlSquareBracketsNoSpaceRemoval.Replace(
            //             daxQuery,
            //             RemoveSquareBracketsNoSpace
            //        );
            string daxQueryNoBrackets = xmSqlSquareBracketsWithSpaceRemoval.Replace(
                            daxQuery,  // daxQueryNoBracketsOnTableNames,
                            RemoveSquareBracketsWithSpace);
            string daxQueryNoDots = xmSqlDotSeparator.Replace(daxQueryNoBrackets, "[");
            string result = xmSqlParenthesis.Replace(daxQueryNoDots, FixSpaceParenthesis);
            return result;
        }

        public static bool ExtractEstimatedSize(this string daxQuery, out long rows, out long bytes) {
            var m = xmSqlPatternSize.Match(daxQuery);
            string rowsString = m.Groups["rows"].Value;
            string bytesString = m.Groups["bytes"].Value;
            bool foundRows = long.TryParse(rowsString, out rows);
            bool foundBytes = long.TryParse(bytesString, out bytes);
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
    }

    //[Export(typeof(ITraceWatcher)),PartCreationPolicy(CreationPolicy.NonShared)]
    class ServerTimesViewModel
        : TraceWatcherBaseViewModel, ISaveState, IServerTimes
        , IHandle<UpdateGlobalOptions>
    {
        public IGlobalOptions Options { get; set; }
        public Dictionary<string, string> RemapColumnNames { get; set; }
        public Dictionary<string, string> RemapTableNames { get; set; }

        [ImportingConstructor]
        public ServerTimesViewModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions, ServerTimingDetailsViewModel serverTimingDetails
            , IGlobalOptions options) : base(eventAggregator, globalOptions)
        {
            _storageEngineEvents = new BindableCollection<TraceStorageEngineEvent>();
            RemapColumnNames = new Dictionary<string, string>();
            RemapTableNames = new Dictionary<string, string>();
            Options = options;
            ServerTimingDetails = serverTimingDetails;
            //ServerTimingDetails.PropertyChanged += ServerTimingDetails_PropertyChanged;
        }

        #region Tooltip properties
        public string TotalTooltip => "The total server side duration of the query";
        public string FETooltip => "Formula Engine (FE) Duration";
        public string SETooltip => "Storage Engine (SE) Duration";
        public string SECpuTooltip => "Storage Engine CPU Duration";
        public string SEQueriesTooltip => "The number of queries sent to the Storage Engine while processing this query";
        public string SECacheTooltip => "The number of queries sent to the Storage Engine that were answered from the SE Cache";
        #endregion

        protected override List<DaxStudioTraceEventClass> GetMonitoredEvents()
        {
            //return new List<TraceEventClass> 
            //    { TraceEventClass.QuerySubcube
            //    , TraceEventClass.VertiPaqSEQueryEnd
            //    , TraceEventClass.VertiPaqSEQueryCacheMatch
            //    , TraceEventClass.QueryEnd };

            return new List<DaxStudioTraceEventClass>
                { DaxStudioTraceEventClass.QuerySubcube
                , DaxStudioTraceEventClass.VertiPaqSEQueryBegin
                , DaxStudioTraceEventClass.VertiPaqSEQueryEnd
                , DaxStudioTraceEventClass.VertiPaqSEQueryCacheMatch
                , DaxStudioTraceEventClass.AggregateTableRewriteQuery
                , DaxStudioTraceEventClass.DirectQueryEnd
                , DaxStudioTraceEventClass.QueryBegin
                , DaxStudioTraceEventClass.QueryEnd};
        }

        public bool HighlightXmSqlCallbacks
        {
            get => Options.HighlightXmSqlCallbacks;
        }

        public bool SimplifyXmSqlSyntax
        {
            get => Options.SimplifyXmSqlSyntax;
        }

        public bool ReplaceXmSqlColumnNames
        {
            get => Options.ReplaceXmSqlColumnNames;
        }

        public bool ReplaceXmSqlTableNames
        {
            get => Options.ReplaceXmSqlTableNames;
        }

        public void Handle(UpdateGlobalOptions message)
        {
            NotifyOfPropertyChange(nameof(HighlightXmSqlCallbacks));
            NotifyOfPropertyChange(nameof(SimplifyXmSqlSyntax));
            NotifyOfPropertyChange(nameof(ReplaceXmSqlColumnNames));
            // TODO - update server timing pane?
        }

        // This method is called after the WaitForEvent is seen (usually the QueryEnd event)
        // This is where you can do any processing of the events before displaying them to the UI
        protected override void ProcessResults()
        {
            ClearAll();

            int batchScan = 0;
            long batchStorageEngineDuration = 0;
            long batchStorageEngineCpu = 0;
            long batchStorageEngineQueryCount = 0;

            if (Events != null)
            {
                foreach (var traceEvent in Events)
                {
                    if (traceEvent.EventClass == DaxStudioTraceEventClass.VertiPaqSEQueryBegin)
                    {
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
                    }

                    if (traceEvent.EventClass == DaxStudioTraceEventClass.VertiPaqSEQueryEnd)
                    {
                        // At the end of a batch, we compute the cost for the batch and assign the cost to the complete query
                        if (traceEvent.EventSubclass == DaxStudioTraceEventSubclass.BatchVertiPaqScan)
                        {
                            batchScan--;
                            // The value should never be greater than 1. If it happens, we should log it and investigate as possible bug.
                            System.Diagnostics.Debug.Assert(batchScan == 0, "Nested VertiScan batches detected or missed SE QueryBegin events!");

                            // Subtract from the batch event the total computed for the scan events within the batch
                            traceEvent.Duration -= batchStorageEngineDuration;
                            traceEvent.CpuTime -= batchStorageEngineCpu;

                            StorageEngineDuration += traceEvent.Duration;
                            StorageEngineCpu += traceEvent.CpuTime;
                            // Currently, we do not compute a storage engine query for the batch event - we might uncomment this if we decide to show the Batch event by default
                            StorageEngineQueryCount++;

                        }
                        else if (traceEvent.EventSubclass == DaxStudioTraceEventSubclass.VertiPaqScan)
                        {
                            if (batchScan > 0) {
                                traceEvent.InternalBatchEvent = true;
                                batchStorageEngineDuration += traceEvent.Duration;
                                batchStorageEngineCpu += traceEvent.CpuTime;
                                batchStorageEngineQueryCount++;
                            }
                            StorageEngineDuration += traceEvent.Duration;
                            StorageEngineCpu += traceEvent.CpuTime;
                            StorageEngineQueryCount++;
                        }
                        _storageEngineEvents.Add(new TraceStorageEngineEvent(traceEvent, _storageEngineEvents.Count + 1, Options, RemapColumnNames, RemapTableNames));
                    }

                    if (traceEvent.EventClass == DaxStudioTraceEventClass.DirectQueryEnd)
                    {
                        StorageEngineDuration += traceEvent.Duration;
                        StorageEngineCpu += traceEvent.CpuTime;
                        StorageEngineQueryCount++;
                        _storageEngineEvents.Add(new TraceStorageEngineEvent(traceEvent, _storageEngineEvents.Count + 1, Options, RemapColumnNames, RemapTableNames));
                    }

                    if (traceEvent.EventClass == DaxStudioTraceEventClass.AggregateTableRewriteQuery)
                    {
                        _storageEngineEvents.Add(new RewriteTraceEngineEvent(traceEvent, _storageEngineEvents.Count + 1, Options, RemapColumnNames, RemapTableNames));
                    }

                    if (traceEvent.EventClass == DaxStudioTraceEventClass.QueryEnd)
                    {
                        TotalDuration = traceEvent.Duration;
                        TotalCpuDuration = traceEvent.CpuTime;
                        QueryEndDateTime = traceEvent.EndTime;
                    }
                    if (traceEvent.EventClass == DaxStudioTraceEventClass.VertiPaqSEQueryCacheMatch)
                    {
                        VertipaqCacheMatches++;
                        _storageEngineEvents.Add(new TraceStorageEngineEvent(traceEvent, _storageEngineEvents.Count + 1, Options, RemapColumnNames, RemapTableNames));
                    }
                }

                FormulaEngineDuration = TotalDuration - StorageEngineDuration;
                if (QueryHistoryEvent != null)
                {
                    QueryHistoryEvent.FEDurationMs = FormulaEngineDuration;
                    QueryHistoryEvent.SEDurationMs = StorageEngineDuration;
                    QueryHistoryEvent.ServerDurationMs = TotalDuration;

                    _eventAggregator.PublishOnUIThread(QueryHistoryEvent);
                }
                if (Events.Count > 0) _eventAggregator.PublishOnUIThread(new ServerTimingsEvent(this));

                Events.Clear();

                NotifyOfPropertyChange(nameof(StorageEngineEvents));
                NotifyOfPropertyChange(nameof(CanExport));
                NotifyOfPropertyChange(nameof(CanCopyResults));
            }
        }

        public DateTime QueryEndDateTime { get; set; }

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
                return TotalDuration == 0 ? 0 : (double)StorageEngineDuration / (double)TotalDuration;
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
                NotifyOfPropertyChange(() => TotalDuration);
                NotifyOfPropertyChange(() => StorageEngineDurationPercentage);
                NotifyOfPropertyChange(() => FormulaEngineDurationPercentage);
                NotifyOfPropertyChange(() => VertipaqCacheMatchesPercentage);
                NotifyOfPropertyChange(() => TotalCpuFactor);
            }
        }
        private long _formulaEngineDuration;
        public long FormulaEngineDuration
        {
            get { return _formulaEngineDuration; }
            private set
            {
                _formulaEngineDuration = value;
                NotifyOfPropertyChange(() => FormulaEngineDuration);
                NotifyOfPropertyChange(() => FormulaEngineDurationPercentage);
            }
        }
        private long _storageEngineDuration;
        public long StorageEngineDuration
        {
            get { return _storageEngineDuration; }
            private set
            {
                _storageEngineDuration = value;
                NotifyOfPropertyChange(() => StorageEngineDuration);
                NotifyOfPropertyChange(() => StorageEngineDurationPercentage);
                NotifyOfPropertyChange(() => StorageEngineCpuFactor);
            }
        }
        private long _storageEngineCpu;
        public long StorageEngineCpu
        {
            get { return _storageEngineCpu; }
            private set
            {
                _storageEngineCpu = value;
                NotifyOfPropertyChange(() => StorageEngineCpu);
                NotifyOfPropertyChange(() => StorageEngineCpuFactor);
            }
        }
        private long _storageEngineQueryCount;
        public long StorageEngineQueryCount
        {
            get { return _storageEngineQueryCount; }
            private set
            {
                _storageEngineQueryCount = value;
                NotifyOfPropertyChange(() => StorageEngineQueryCount);
            }
        }

        private int _vertipaqCacheMatches;
        public int VertipaqCacheMatches
        {
            get { return _vertipaqCacheMatches; }
            set
            {
                _vertipaqCacheMatches = value;
                NotifyOfPropertyChange(() => VertipaqCacheMatches);
                NotifyOfPropertyChange(() => VertipaqCacheMatchesPercentage);
            }
        }

        private readonly BindableCollection<TraceStorageEngineEvent> _storageEngineEvents;

        public IObservableCollection<TraceStorageEngineEvent> StorageEngineEvents
        {
            get
            {
                var fse = from e in _storageEngineEvents
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
                           ) && ServerTimingDetails.ShowScan)
                           ||
                           (e.ClassSubclass.Subclass == DaxStudioTraceEventSubclass.RewriteAttempted && ServerTimingDetails.ShowRewriteAttempts)


                          select e;
                return new BindableCollection<TraceStorageEngineEvent>(fse);
            }
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
                NotifyOfPropertyChange(() => SelectedEvent);
            }
        }

        // IToolWindow interface
        public override string Title => "Server Timings";
        public override string ContentId => "server-timings-trace";
        public override ImageSource IconSource
        {
            get
            {
                var imgSourceConverter = new ImageSourceConverter();
                return imgSourceConverter.ConvertFromInvariantString(
                    @"pack://application:,,,/DaxStudio.UI;component/images/icon-timings@17px.png") as ImageSource;

            }
        }

        public override string ToolTipText => "Runs a server trace to record detailed timing information for performance profiling";

        public override void OnReset()
        {
            IsBusy = false;
            Events.Clear();
            ProcessResults();
        }

        #region ISaveState methods
        void ISaveState.Save(string filename)
        {
            string json = GetJson();
            File.WriteAllText(filename + ".serverTimings", json);

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
            _eventAggregator.PublishOnUIThread(new ShowTraceWindowEvent(this));
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
                StorageEngineCpu = this.StorageEngineCpu,
                TotalDuration = this.TotalDuration,
                VertipaqCacheMatches = this.VertipaqCacheMatches,
                StorageEngineQueryCount = this.StorageEngineQueryCount,
                StoreageEngineEvents = this._storageEngineEvents,
                TotalCpuDuration = this.TotalCpuDuration,
                QueryEndDateTime = this.QueryEndDateTime
            };
            var json = JsonConvert.SerializeObject(m, Formatting.Indented);
            return json;
        }

        void ISaveState.Load(string filename)
        {
            filename = filename + ".serverTimings";
            if (!File.Exists(filename)) return;

            _eventAggregator.PublishOnUIThread(new ShowTraceWindowEvent(this));
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

            FormulaEngineDuration = m.FormulaEngineDuration;
            StorageEngineDuration = m.StorageEngineDuration;
            StorageEngineCpu = m.StorageEngineCpu;
            TotalDuration = m.TotalDuration;
            VertipaqCacheMatches = m.VertipaqCacheMatches;
            StorageEngineQueryCount = m.StorageEngineQueryCount;
            TotalCpuDuration = m.TotalCpuDuration;
            QueryEndDateTime = m.QueryEndDateTime;
            this._storageEngineEvents.Clear();
            this._storageEngineEvents.AddRange(m.StoreageEngineEvents);
            NotifyOfPropertyChange(() => StorageEngineEvents);
        }

        #endregion


        #region Properties to handle layout changes

        public int TextGridRow { get { return ServerTimingDetails?.LayoutBottom ?? false ? 4 : 1; } }
        public int TextGridRowSpan { get { return ServerTimingDetails?.LayoutBottom ?? false ? 1 : 3; } }
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

        private void ServerTimingDetails_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "LayoutBottom":
                case "LayoutRight":
                    NotifyOfPropertyChange(() => TextGridColumn);
                    NotifyOfPropertyChange(() => TextGridRow);
                    NotifyOfPropertyChange(() => TextGridRowSpan);
                    NotifyOfPropertyChange(() => TextColumnWidth);
                    break;
                default:
                    NotifyOfPropertyChange(() => StorageEngineEvents);
                    break;
            }
        }

        #endregion

        #region Title Bar Button Methods

        public override void ClearAll()
        {
            FormulaEngineDuration = 0;
            StorageEngineDuration = 0;
            TotalCpuDuration = 0;
            StorageEngineCpu = 0;
            StorageEngineQueryCount = 0;
            VertipaqCacheMatches = 0;
            TotalDuration = 0;
            _storageEngineEvents.Clear();
            NotifyOfPropertyChange(() => StorageEngineEvents);
            NotifyOfPropertyChange(() => CanExport);
        }

        public override void CopyAll()
        {
            Log.Warning("CopyAll Method not implemented for ServerTimesViewModel");
        }
        #endregion

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
        public void CopyResultsData(bool includeHeader)
        {
            var dataObject = new DataObject();
            var headers = string.Empty;
            if (includeHeader) headers = "Query End\tTotal\tFE\tSE\tSE CPU\tSE CPU(parallelism factor)\tSE Queries\tSE Cache\n";
            var values = $"{QueryEndDateTime.ToString(Constants.IsoDateFormatPaste)}\t{TotalDuration}\t{FormulaEngineDuration}\t{StorageEngineDuration}\t{StorageEngineCpu}\t{StorageEngineCpuFactor}\t{StorageEngineQueryCount}\t{VertipaqCacheMatches}";
            dataObject.SetData(DataFormats.StringFormat, $"{headers}{values}");
            dataObject.SetData(DataFormats.CommaSeparatedValue, $"{headers.Replace("\t", CultureInfo.CurrentCulture.TextInfo.ListSeparator)}\n{values.Replace("\t", CultureInfo.CurrentCulture.TextInfo.ListSeparator)}");

            Clipboard.SetDataObject(dataObject);
        }

        public override bool CanExport => _storageEngineEvents.Count > 0;
        public override void ExportTraceDetails(string filePath)
        {
            File.WriteAllText(filePath, GetJson());
        }

    }
}
