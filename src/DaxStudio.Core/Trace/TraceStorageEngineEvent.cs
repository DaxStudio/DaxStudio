using System;
using System.Collections.Generic;
using Caliburn.Micro;
using DaxStudio.Common;
using DaxStudio.Common.Enums;
using DaxStudio.Core.Model;
using DaxStudio.Interfaces;
using DaxStudio.Parsers;
using DaxStudio.Parsers.StorageEngine;
using DaxStudio.QueryTrace;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Serilog;

namespace DaxStudio.Core.Trace
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
        public int? QueryGroup { get; set; }
        public QueryGroupSummary QueryGroupSummary { get; set; }
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

        public bool IsScanEvent
        {
            get
            {
                return this.Class == DaxStudioTraceEventClass.VertiPaqSEQueryBegin
                    || this.Class == DaxStudioTraceEventClass.VertiPaqSEQueryCacheMatch
                    || this.Class == DaxStudioTraceEventClass.VertiPaqSEQueryEnd
                    || this.Class == DaxStudioTraceEventClass.DirectQueryBegin
                    || this.Class == DaxStudioTraceEventClass.DirectQueryEnd;
            }
        }

        public bool IsInternalEvent
        {
            get
            {
                return this.Subclass == DaxStudioTraceEventSubclass.VertiPaqScanInternal;
            }
        }

        /// <summary>
        /// Returns true for DirectQuery events (SQL queries to external data sources).
        /// These contain SQL syntax, not xmSQL, so they need different parsing.
        /// </summary>
        public bool IsDirectQueryEvent
        {
            get
            {
                return this.Class == DaxStudioTraceEventClass.DirectQueryBegin
                    || this.Class == DaxStudioTraceEventClass.DirectQueryEnd;
            }
        }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
       
        private string _queryRichText = string.Empty;

        [JsonIgnore]
        public string QueryRichText {
            set {
                if (value != null)
                {
                    _queryRichText = value;
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
        public string ObjectName { get; set; }
        public long? StartOffsetMs { get; set; }
        public long? TotalQueryDuration { get; set; } = 0;

        [JsonIgnore]
        public long? TimelineDuration => TotalQueryDuration + 1;

        [JsonIgnore]
        public long? DisplayDuration => Convert.ToInt64((EndTime - StartTime).TotalMilliseconds);

        public TraceStorageEngineEvent(DaxStudioTraceEventArgs ev, int rowNumber, IGlobalOptions options, Dictionary<string, string> remapColumns, Dictionary<string, string> remapTables, HashSet<string> dateColumnIds = null)
        {
            Options = options;
            RowNumber = rowNumber;
            Class = ev.EventClass;
            Subclass = ev.EventSubclass;
            InternalBatchEvent = ev.InternalBatchEvent;
            StartTime = ev.StartTime;
            EndTime = ev.EndTime;
            TextData = ev.TextData;
            ObjectName = ev.ObjectName;

            FormatQuery(remapColumns, remapTables, dateColumnIds);

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
        }

        /// <summary>
        /// Formats the query text based on current Options. Can be called again to
        /// re-apply formatting after options change (e.g., toggling simplify/format settings).
        /// </summary>
        public void FormatQuery(Dictionary<string, string> remapColumns = null, Dictionary<string, string> remapTables = null, HashSet<string> dateColumnIds = null)
        {
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
                    if (!IsDaxDirectQuery(TextData) && Options.FormatDirectQuerySql)
                    {
                        Query = SqlFormatter.FormatSql(TextData);
                    }
                    else
                    {
                        Query = TextData;
                    }
                    break;
                default:
                    if (Options.UseAntlrParser)
                    {
                        // ANTLR-based formatting: single parse tree walk handles formatting, simplification, and estimated size extraction
                        var antlrResult = AntlrXmSqlFormatter.Format(
                            TextData,
                            Options.FormatXmSql,
                            Options.SimplifyXmSqlSyntax,
                            out long antlrRows,
                            out long antlrBytes,
                            out bool antlrHasSize,
                            Options.ReplaceXmSqlDatesWithIsoFormat,
                            remapColumns,
                            remapTables,
                            dateColumnIds);

                        if (antlrResult != null)
                        {
                            Query = antlrResult;

                            if (antlrHasSize)
                            {
                                EstimatedRows = antlrRows;
                                EstimatedKBytes = 1 + antlrBytes / 1024;
                            }
                            break;
                        }

                        // Fall through to regex approach if ANTLR parsing failed
                        Log.Warning(Constants.LogMessageTemplate, nameof(TraceStorageEngineEvent), nameof(FormatQuery), "ANTLR parsing of xmSQL failed, falling back to regex-based formatting. Query text: " + TextData);
                    }

                    string rawText = Options.SimplifyXmSqlSyntax ? TextData.RemovePremiumTags() : TextData;
                    // Format xmSQL
                    string queryFormatted = Options.FormatXmSql ? rawText.FormatXmSql() : rawText;
                    // Normalize tabs to 4 spaces
                    queryFormatted = queryFormatted.Replace("\t", "    ");
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
                                    .FormatIndexSize()
                                : queryRemapped;

                    // Convert OA date values in COALESCE filters to ISO dates
                    if (Options.ReplaceXmSqlDatesWithIsoFormat)
                    {
                        Query = Query.ConvertCoalesceDatesToIso();
                    }
                    break;
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
                // Set flag in case any callback is present
                HighlightQuery = Query.ContainsCallback();
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
}
