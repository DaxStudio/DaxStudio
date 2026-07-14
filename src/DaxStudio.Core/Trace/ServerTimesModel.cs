using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;
using DaxStudio.Common;
using DaxStudio.Common.Enums;
using DaxStudio.Core.Enums;
using DaxStudio.Core.Events;
using DaxStudio.Core.Extensions;
using DaxStudio.Interfaces;
using DaxStudio.Interfaces.Enums;
using DaxStudio.Parsers;
using DaxStudio.Parsers.StorageEngine;
using DaxStudio.QueryTrace;
using Newtonsoft.Json;
using Serilog;

namespace DaxStudio.Core.Trace
{
    // UI-free base for the Server Timings tool window. The thin
    // DaxStudio.UI.ViewModels.ServerTimesViewModel shell adds the WPF
    // bits (clipboard helpers, FolderBrowserDialog, the heatmap
    // ImageSource, grid-layout properties, theme handling, dialog launchers
    // and the IHandle<...> subscribers for UI events).
    public abstract class ServerTimesModel : TraceWatcherBaseModel, IServerTimes
    {
        private string _queryEndActivityId = string.Empty;
        private readonly HashSet<string> _internalQueryActivityIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private DaxStudioTraceEventArgs maxStorageEngineVertipaqEvent;
        private DaxStudioTraceEventArgs maxStorageEngineDirectQueryEvent;

        public IGlobalOptions Options { get; set; }
        public Dictionary<string, string> RemapColumnNames { get; set; }
        public Dictionary<string, string> RemapTableNames { get; set; }
        public HashSet<string> DateColumnIds { get; set; }

        protected ServerTimesModel(IEventAggregator eventAggregator, ServerTimingDetailsViewModel serverTimingDetails
            , IGlobalOptions options, IWindowManager windowManager) : base(eventAggregator, options, windowManager)
        {
            _storageEngineEvents = new BindableCollection<TraceStorageEngineEvent>();
            RemapColumnNames = new Dictionary<string, string>();
            RemapTableNames = new Dictionary<string, string>();
            DateColumnIds = new HashSet<string>();
            Options = options;
            StorageEventTimelineStyle = options.StorageEventHeatmapStyle;
            ServerTimingDetails = serverTimingDetails;
        }

        private bool parallelStorageEngineEventsDetected;
        public bool ParallelStorageEngineEventsDetected
        {
            get => parallelStorageEngineEventsDetected;
            set
            {
                parallelStorageEngineEventsDetected = value;
                NotifyOfPropertyChange(nameof(ParallelStorageEngineEventsDetected));
            }
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
        public bool ReplaceXmSqlDatesWithIsoFormat => Options.ReplaceXmSqlDatesWithIsoFormat;
        public bool ShowTotalDirectQueryDuration => Options.ShowTotalDirectQueryDuration;
        public bool ShowStorageEngineNetParallelDuration => Options.ShowStorageEngineNetParallelDuration;
        public bool ShowStorageEngineDependencies => Options.ShowStorageEngineDependencies;
        public bool ShowInModelDiagramVisible => Options.ShowModelDiagram;

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
            NotifyOfPropertyChange(nameof(ReplaceXmSqlDatesWithIsoFormat));
            NotifyOfPropertyChange(nameof(StorageEventHeatmapHeight));
            NotifyOfPropertyChange(nameof(StorageEventTimelineStyle));
            NotifyOfPropertyChange(nameof(TimelineVerticalMargin));
            NotifyOfPropertyChange(nameof(ShowStorageEngineDependencies));
            NotifyOfPropertyChange(nameof(ShowInModelDiagramVisible));
            NotifyOfPropertyChange(nameof(ShowQueryGroupColumn));
        }

        protected override void ProcessSingleEvent(DaxStudioTraceEventArgs singleEvent)
        {
            base.ProcessSingleEvent(singleEvent);

            // These events are processed in "real-time" during the execution, just to show that something is moving in total time
            // We do not provide details of FE/SE until the execution is completed
            switch (singleEvent.EventClass)
            {
                case DaxStudioTraceEventClass.QueryBegin:
                    QueryStartDateTime = singleEvent.StartTime;
                    TotalDuration = 0;
                    break;
                case DaxStudioTraceEventClass.QueryEnd:
                    // Don't capture ExecutionMetrics for DAX Studio internal queries (e.g. the
                    // session refresh query run after clearing the cache). Track their ActivityId
                    // so any related events can be excluded even if they arrive out of order.
                    if (!string.IsNullOrEmpty(singleEvent.TextData) && singleEvent.TextData.Contains(Constants.InternalQueryHeader))
                    {
                        if (!string.IsNullOrEmpty(singleEvent.ActivityId))
                            _internalQueryActivityIds.Add(singleEvent.ActivityId);
                    }
                    else
                    {
                        _queryEndActivityId = singleEvent.ActivityId;
                    }
                    TotalDuration = (long)(singleEvent.CurrentTime - QueryStartDateTime).TotalMilliseconds;
                    break;
                case DaxStudioTraceEventClass.ExecutionMetrics:
                    if (singleEvent.ActivityId == _queryEndActivityId && !string.IsNullOrEmpty(_queryEndActivityId))
                    {
                        _queryEndActivityId = string.Empty;
                        AllStorageEngineEvents.Add(new ExecutionMetricsTraceEngineEvent(singleEvent, AllStorageEngineEvents.Count + 1, Options, RemapColumnNames, RemapTableNames, DateColumnIds));
                        NotifyOfPropertyChange(nameof(StorageEngineEvents));
                    }
                    break;
                default:
                    TotalDuration = (long)(singleEvent.CurrentTime - QueryStartDateTime).TotalMilliseconds;
                    break;
            }
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
                if (this.IsStart != y.IsStart)
                {
                    return this.IsStart ? -1 : 1;
                }

                var compareTimeStamp = this.TimeStamp.CompareTo(y.TimeStamp);

                if (this.IsStart)
                {
                    if (this.Event.EventClass == DaxStudioTraceEventClass.QueryEnd
                        && y.Event.EventClass != DaxStudioTraceEventClass.QueryEnd)
                    {
                        return -1;
                    }
                    else if (this.Event.EventClass != DaxStudioTraceEventClass.QueryEnd
                        && y.Event.EventClass == DaxStudioTraceEventClass.QueryEnd)
                    {
                        return 1;
                    }
                    else return compareTimeStamp;
                }
                else
                {
                    if (this.Event.EventClass == DaxStudioTraceEventClass.QueryEnd
                        && y.Event.EventClass != DaxStudioTraceEventClass.QueryEnd)
                    {
                        return 1;
                    }
                    else if (this.Event.EventClass != DaxStudioTraceEventClass.QueryEnd
                        && y.Event.EventClass == DaxStudioTraceEventClass.QueryEnd)
                    {
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
                if (!(obj is SortableEvent)) return false;
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
            if (AllStorageEngineEvents?.Count > 0)
            {
                Log.Debug(Constants.LogMessageTemplate, nameof(ServerTimesModel), nameof(ProcessResults), "results have not been cleared, skipping processing");
                return;
            }

            int batchScan = 0;
            long batchStorageEngineDuration = 0;
            long batchStorageEngineCpu = 0;
            long batchStorageEngineQueryCount = 0;

            maxStorageEngineVertipaqEvent = null;
            maxStorageEngineDirectQueryEvent = null;
            bool eventsProcessed = false;

            if (Events != null)
            {
                // Trace events for the DAX Studio internal session-refresh query (run after
                // clearing the cache) can be interleaved with the user's query events and may
                // arrive out of order. Rather than inspecting only the first QueryEnd we remove
                // every QueryEnd tagged with the internal marker along with any related events
                // (matched by ActivityId, e.g. ExecutionMetrics or storage engine scans).
                bool removedInternalEvents = RemoveInternalQueryEvents();

                // If we removed internal query events and there is no user QueryEnd left then
                // there is nothing complete to process yet, so wait for the next final event.
                if (removedInternalEvents && !Events.Any(e => e.EventClass == DaxStudioTraceEventClass.QueryEnd))
                {
                    Log.Debug(Constants.LogMessageTemplate, nameof(ServerTimesModel), nameof(ProcessResults), "No user QueryEnd event present after removing internal query events, skipping processing");
                    return;
                }

                bool IsEnd(DaxStudioTraceEventClass eventClass)
                {
                    return eventClass == DaxStudioTraceEventClass.VertiPaqSEQueryEnd
                        || eventClass == DaxStudioTraceEventClass.DirectQueryEnd
                        || eventClass == DaxStudioTraceEventClass.QueryEnd;
                }

                foreach (var traceEvent in Events.Where(e => IsEnd(e.EventClass)))
                {
                    if (traceEvent.EndTime == traceEvent.StartTime && traceEvent.Duration > 0)
                    {
                        traceEvent.EndTime = traceEvent.StartTime.AddMilliseconds((double)traceEvent.Duration);
                        Log.Verbose($">> fix EndTime row Duration={traceEvent.Duration} StartTime={traceEvent.StartTime.Ticks / 10000} EndTime={traceEvent.EndTime.Ticks / 10000} Duration={traceEvent.Duration} NetParallelDuration={traceEvent.NetParallelDuration} Cpu={traceEvent.CpuTime}");
                    }
                    else if (traceEvent.EndTime >= traceEvent.StartTime && (traceEvent.EndTime - traceEvent.StartTime).TotalMilliseconds > traceEvent.Duration)
                    {
                        traceEvent.Duration = Convert.ToInt64((traceEvent.EndTime - traceEvent.StartTime).TotalMilliseconds);
                        Log.Verbose($">> fix Duration row Duration={traceEvent.Duration} CalcDuration={(traceEvent.EndTime - traceEvent.StartTime).TotalMilliseconds} NetParallelDuration={traceEvent.NetParallelDuration} Cpu={traceEvent.CpuTime}");
                    }
                    else
                    {
                        Log.Verbose($">> NOT row Duration={traceEvent.Duration} StartTime={traceEvent.StartTime.Ticks / 10000} EndTime={traceEvent.EndTime.Ticks / 10000} Duration={traceEvent.Duration} NetParallelDuration={traceEvent.NetParallelDuration} Cpu={traceEvent.CpuTime}");
                    }
                    traceEvent.NetParallelDuration = traceEvent.Duration;
                }

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

                int seLevel = 0;
                double new_FormulaEngineDuration = 0;
                DateTime currentScanTime = DateTime.MinValue;
                foreach (var e in seEvents)
                {
                    Log.Verbose($"** lev={seLevel} Event {e.Event.EventClass} Time={e.TimeStamp.TimeOfDay}");
                    switch (e.Event.EventClass)
                    {
                        case DaxStudioTraceEventClass.QueryEnd:
                            Log.Debug($"QueryEnd StartTime={e.Event.StartTime.Millisecond} EndTime={e.Event.EndTime.Millisecond}");

                            if (e.IsStart)
                            {
                                currentScanTime = e.Event.StartTime;
                            }
                            else
                            {
                                Debug.Assert(currentScanTime > DateTime.MinValue, "Missing QueryBegin event, invalid FE calculation");
                                Debug.Assert(seLevel == 0, "Invalid storage engine level at QueryEnd event, invalid FE calculation");
                                if (seLevel == 0)
                                {
                                    var delta = (e.TimeStamp - currentScanTime).TotalMilliseconds;
                                    new_FormulaEngineDuration += delta;
                                    Log.Verbose($"FE += {delta}ms QueryEnd currentScanTime={currentScanTime.Millisecond} TimeStamp={e.TimeStamp.Millisecond} new_FE={new_FormulaEngineDuration}");
                                }
                            }
                            break;
                        case DaxStudioTraceEventClass.VertiPaqSEQueryEnd:
                        case DaxStudioTraceEventClass.DirectQueryEnd:
                            Log.Verbose($"VertiPaqSEQueryEnd {e.Event.EventSubclassName} StartTime={e.Event.StartTime.Millisecond} EndTime={e.Event.EndTime.Millisecond} Offset={(e.Event.StartTime - currentScanTime).TotalMilliseconds}");
                            if (e.IsStart)
                            {
                                if (seLevel == 0)
                                {
                                    var delta = (e.Event.StartTime - currentScanTime).TotalMilliseconds;
                                    new_FormulaEngineDuration += delta;
                                    Log.Verbose($"FE += {delta}ms VertiPaqSEQueryEnd currentScanTime={currentScanTime.Millisecond} TimeStamp={e.TimeStamp.Millisecond} new_FE={new_FormulaEngineDuration}");
                                }
                                seLevel++;
                            }
                            else
                            {
                                seLevel--;
                                if (seLevel == 0)
                                {
                                    currentScanTime = e.Event.EndTime;
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

                            if (traceEvent.EventSubclass == DaxStudioTraceEventSubclass.BatchVertiPaqScan)
                            {
                                batchScan++;
                                System.Diagnostics.Debug.Assert(batchScan == 1, "Nested VertiScan batches detected or missed SE QueryEnd events!");

                                batchStorageEngineDuration = 0;
                                batchStorageEngineCpu = 0;
                                batchStorageEngineQueryCount = 0;
                            }

                            break;
                        case DaxStudioTraceEventClass.VertiPaqSEQueryEnd:
                            Log.Verbose($"VertiPaqSEQueryEnd {traceEvent.EventSubclass} Duration={traceEvent.Duration} NetParallelDuration={traceEvent.NetParallelDuration} Cpu={traceEvent.CpuTime}");
                            if (traceEvent.EventSubclass == DaxStudioTraceEventSubclass.BatchVertiPaqScan)
                            {
                                batchScan--;
                                System.Diagnostics.Debug.Assert(batchScan == 0, "Nested VertiScan batches detected or missed SE QueryBegin events!");

                                Log.Verbose($"FIX EndScan traceEvent.Duration={traceEvent.Duration}ms batchStorageEngineDuration={batchStorageEngineDuration}");
                                traceEvent.Duration = Math.Max((long)(traceEvent.Duration - batchStorageEngineDuration), 0);
                                traceEvent.NetParallelDuration = traceEvent.Duration;
                                traceEvent.CpuTime = Math.Max((long)(traceEvent.CpuTime - batchStorageEngineCpu), 0);

                                StorageEngineDuration += traceEvent.Duration;
                                StorageEngineNetParallelDuration += traceEvent.Duration;
                                Log.Verbose($"StorageEngineDuration)={StorageEngineDuration}");
                                StorageEngineCpu += traceEvent.CpuTime;
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
                                    UpdateForParallelOperations(ref maxStorageEngineVertipaqEvent, traceEvent);
                                    StorageEngineDuration += traceEvent.Duration;
                                }
                                StorageEngineNetParallelDuration += traceEvent.NetParallelDuration;
                                StorageEngineCpu += traceEvent.CpuTime;
                                StorageEngineQueryCount++;

                            }
                            UpdateTimelineTotalDuration(traceEvent);
                            AllStorageEngineEvents.Add(new TraceStorageEngineEvent(traceEvent, AllStorageEngineEvents.Count + 1, Options, RemapColumnNames, RemapTableNames, DateColumnIds));

                            break;
                        case DaxStudioTraceEventClass.DirectQueryEnd:
                            UpdateForParallelOperations(ref maxStorageEngineDirectQueryEvent, traceEvent);
                            TotalDirectQueryDuration += traceEvent.Duration;
                            StorageEngineDuration += traceEvent.Duration;
                            StorageEngineNetParallelDuration += traceEvent.NetParallelDuration;
                            StorageEngineCpu += traceEvent.CpuTime;
                            StorageEngineQueryCount++;
                            UpdateTimelineTotalDuration(traceEvent);
                            AllStorageEngineEvents.Add(new TraceStorageEngineEvent(traceEvent, AllStorageEngineEvents.Count + 1, Options, RemapColumnNames, RemapTableNames, DateColumnIds));
                            break;

                        case DaxStudioTraceEventClass.AggregateTableRewriteQuery:
                            AllStorageEngineEvents.Add(new RewriteTraceEngineEvent(traceEvent, AllStorageEngineEvents.Count + 1, Options, RemapColumnNames, RemapTableNames, DateColumnIds));
                            break;
                        case DaxStudioTraceEventClass.ExecutionMetrics:
                            break;
                        case DaxStudioTraceEventClass.QueryEnd:

                            TotalDuration = traceEvent.Duration;
                            TotalCpuDuration = traceEvent.CpuTime;
                            QueryEndDateTime = traceEvent.EndTime;
                            QueryStartDateTime = traceEvent.StartTime;
                            ActivityID = traceEvent.ActivityId;
                            RequestID = traceEvent.RequestId;
                            UpdateTimelineTotalDuration(traceEvent);
                            break;
                        case DaxStudioTraceEventClass.QueryBegin:
                            Parameters = traceEvent.RequestParameters;
                            CommandText = traceEvent.TextData;
                            break;
                        case DaxStudioTraceEventClass.VertiPaqSEQueryCacheMatch:

                            VertipaqCacheMatches++;
                            UpdateTimelineTotalDuration(traceEvent);
                            AllStorageEngineEvents.Add(new TraceStorageEngineEvent(traceEvent, AllStorageEngineEvents.Count + 1, Options, RemapColumnNames, RemapTableNames, DateColumnIds));
                            break;
                    }
                }

                // New calculation for parallel SE queries (2022-10-03) Marco Russo
                Log.Verbose($"FormulaEngineDuration={FormulaEngineDuration}ms new={new_FormulaEngineDuration}");
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

                if (QueryHistoryEvent != null)
                {
                    QueryHistoryEvent.FEDurationMs = FormulaEngineDuration;
                    QueryHistoryEvent.SEDurationMs = StorageEngineDuration;
                    QueryHistoryEvent.ServerDurationMs = TotalDuration;

                    _eventAggregator.PublishAsync(QueryHistoryEvent);
                }

                Log.Debug(Constants.LogMessageTemplate, nameof(ServerTimesModel), nameof(ProcessResults), "Publishing ServerTimings event for other view models to consume");
                _eventAggregator.PublishAsync(new ServerTimingsEvent(this));

                Events.Clear();
                UpdateTimelineDurations(QueryStartDateTime, QueryEndDateTime, TimelineTotalDuration);

                if (ShowQueryGroupColumn) _ = RunQueryGroupingAsync();

                Refresh();
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
                traceEvent.StartOffsetMs = Convert.ToInt64((traceEvent.StartTime - queryStartDateTime).TotalMilliseconds);
                traceEvent.TotalQueryDuration = totalDuration;
            }

            NotifyOfPropertyChange(nameof(StorageEngineEvents));
        }

        private async Task RunQueryGroupingAsync()
        {
            try
            {
                var queries = AllStorageEngineEvents
                    .Where(e => e.IsScanEvent && !e.IsInternalEvent && !string.IsNullOrWhiteSpace(e.Query))
                    .Select(e => (e.RowNumber, e.Query))
                    .ToList();

                var grouper = new XmSqlQueryGrouper();
                var groupResult = await Task.Run(() => grouper.GroupQueries(queries));

                foreach (var evt in AllStorageEngineEvents)
                {
                    if (groupResult.QueryToStructuralGroup.TryGetValue(evt.RowNumber, out int structId))
                        evt.QueryGroup = structId;
                }

                var structGroupFingerprints = groupResult.StructuralGroups.ToDictionary(g => g.GroupId, g => g.Fingerprint);

                var queryTexts = AllStorageEngineEvents
                    .Where(e => e.QueryGroup.HasValue)
                    .ToDictionary(e => e.RowNumber, e => e.Query);

                var groupSummaries = AllStorageEngineEvents
                    .Where(e => e.QueryGroup.HasValue)
                    .GroupBy(e => e.QueryGroup.Value)
                    .ToDictionary(g => g.Key, g =>
                    {
                        structGroupFingerprints.TryGetValue(g.Key, out var fingerprint);

                        var cacheHits = g.Count(e => e.Subclass == DaxStudioTraceEventSubclass.VertiPaqCacheExactMatch);
                        var nonCacheEvents = g.Where(e => e.Subclass != DaxStudioTraceEventSubclass.VertiPaqCacheExactMatch
                                                       && e.Subclass != DaxStudioTraceEventSubclass.VertiPaqScanInternal
                                                       && e.Subclass != DaxStudioTraceEventSubclass.BatchVertiPaqScan).ToList();

                        return new QueryGroupSummary
                        {
                            GroupId = g.Key,
                            GroupType = XmSqlQueryGrouper.DetermineGroupType(groupResult, g.Key, queryTexts, nonCacheEvents.Count),
                            EventCount = nonCacheEvents.Count,
                            CacheHits = cacheHits,
                            TotalDuration = nonCacheEvents.Sum(e => e.Duration ?? 0),
                            TotalCpu = nonCacheEvents.Sum(e => e.CpuTime ?? 0),
                            TotalRows = nonCacheEvents.Sum(e => e.EstimatedRows ?? 0),
                            TotalKB = nonCacheEvents.Sum(e => e.EstimatedKBytes ?? 0)
                        };
                    });

                foreach (var evt in AllStorageEngineEvents)
                {
                    if (evt.QueryGroup.HasValue && groupSummaries.TryGetValue(evt.QueryGroup.Value, out var summary))
                        evt.QueryGroupSummary = summary;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to run query similarity grouping in Server Timings");
            }
        }

        private static bool IsInternalQueryEnd(DaxStudioTraceEventArgs traceEvent)
        {
            return traceEvent.EventClass == DaxStudioTraceEventClass.QueryEnd
                && !string.IsNullOrEmpty(traceEvent.TextData)
                && traceEvent.TextData.Contains(Constants.InternalQueryHeader);
        }

        // Removes trace events belonging to DAX Studio internal queries (such as the session
        // refresh query executed after clearing the cache) from the Events buffer. Because
        // events can arrive out of order we cannot rely on the first QueryEnd - we collect the
        // ActivityIds of every QueryEnd carrying the internal marker (including any recorded
        // during real-time processing) and then drop those QueryEnd events plus any related
        // events sharing the same ActivityId (e.g. ExecutionMetrics and storage engine scans).
        // Returns true if any internal query events were removed.
        private bool RemoveInternalQueryEvents()
        {
            var internalActivityIds = new HashSet<string>(_internalQueryActivityIds, StringComparer.OrdinalIgnoreCase);
            foreach (var traceEvent in Events)
            {
                if (IsInternalQueryEnd(traceEvent) && !string.IsNullOrEmpty(traceEvent.ActivityId))
                {
                    internalActivityIds.Add(traceEvent.ActivityId);
                }
            }

            bool IsInternalQueryEvent(DaxStudioTraceEventArgs traceEvent)
            {
                if (IsInternalQueryEnd(traceEvent)) return true;
                return !string.IsNullOrEmpty(traceEvent.ActivityId)
                    && internalActivityIds.Contains(traceEvent.ActivityId);
            }

            // the tracked ActivityIds have now been folded into the local set, so reset them
            _internalQueryActivityIds.Clear();

            if (!Events.Any(IsInternalQueryEvent)) return false;

            Log.Debug(Constants.LogMessageTemplate, nameof(ServerTimesModel), nameof(RemoveInternalQueryEvents), "Removing DAX Studio internal query events from the trace buffer");

            var retainedEvents = Events.Where(traceEvent => !IsInternalQueryEvent(traceEvent)).ToList();
            while (Events.TryDequeue(out _)) { }
            foreach (var traceEvent in retainedEvents) Events.Enqueue(traceEvent);

            return true;
        }

        // This function assumes that the events arrive in StartTime order, then we check if
        // the start/end times of the current event overlap with the end time of the previous
        // event with the latest end time.
        private void UpdateForParallelOperations(ref DaxStudioTraceEventArgs maxEvent, DaxStudioTraceEventArgs traceEvent)
        {
            if (maxEvent == null)
            {
                maxEvent = traceEvent;
                return;
            }

            var overlapEventsMs = (maxEvent.EndTime - traceEvent.StartTime).TotalMilliseconds;
            if (overlapEventsMs > 0)
            {
                ParallelStorageEngineEventsDetected = (overlapEventsMs > 10);

                if (maxEvent.EndTime > traceEvent.EndTime)
                {
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
            protected set
            {
                _totalDuration = value;
            }
        }
        private long _formulaEngineDuration;
        public long FormulaEngineDuration
        {
            get { return _formulaEngineDuration; }
            protected set
            {
                _formulaEngineDuration = value;
            }
        }
        private long _storageEngineDuration;
        public long StorageEngineDuration
        {
            get { return _storageEngineDuration; }
            protected set
            {
                _storageEngineDuration = value;
            }
        }

        private long _storageEngineNetParallelDuration;
        public long StorageEngineNetParallelDuration
        {
            get { return _storageEngineNetParallelDuration; }
            protected set
            {
                _storageEngineNetParallelDuration = value;
                NotifyOfPropertyChange(() => StorageEngineNetParallelDuration);
            }
        }

        private long _storageEngineCpu;
        public long StorageEngineCpu
        {
            get { return _storageEngineCpu; }
            protected set
            {
                _storageEngineCpu = value;
            }
        }
        private long _storageEngineQueryCount;
        public long StorageEngineQueryCount
        {
            get { return _storageEngineQueryCount; }
            protected set
            {
                _storageEngineQueryCount = value;
            }
        }

        private int _vertipaqCacheMatches;
        public int VertipaqCacheMatches
        {
            get { return _vertipaqCacheMatches; }
            set
            {
                _vertipaqCacheMatches = value;
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
                                  && e.ClassSubclass.Subclass != DaxStudioTraceEventSubclass.TabularQuery
                                  && e.ClassSubclass.Subclass != DaxStudioTraceEventSubclass.TabularQueryInternal
                                  && e.Class != DaxStudioTraceEventClass.ExecutionMetrics
                                  && e.ClassSubclass.QueryLanguage != DaxStudioTraceEventClassSubclass.Language.SQL
                               ) && ServerTimingDetails.ShowScan)
                               ||
                               (e.ClassSubclass.QueryLanguage == DaxStudioTraceEventClassSubclass.Language.SQL && ServerTimingDetails.ShowSql)
                               ||
                              ((e.ClassSubclass.Subclass == DaxStudioTraceEventSubclass.TabularQuery
                              || e.ClassSubclass.Subclass == DaxStudioTraceEventSubclass.TabularQueryInternal) && ServerTimingDetails.ShowTabularQueries)
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
        public bool IsSEQuery
        {
            get => _isSEQuery;
            set
            {
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
        }

        public bool ScrollLeft { get; set; }

        public void ToggleScrollLeft()
        {
            ScrollLeft = !ScrollLeft;
            NotifyOfPropertyChange(nameof(ScrollLeft));
        }

        public virtual void SavePackage(Package package)
        {
            Uri uriTom = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.ServerTimings, UriKind.Relative));
            using (TextWriter tw = new StreamWriter(package.CreatePart(uriTom, "application/json", CompressionOption.Maximum).GetStream(), Encoding.UTF8))
            {
                tw.Write(GetJson());
                tw.Close();
            }
        }

        public string GetJson()
        {
            var m = new ServerTimesSnapshot()
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
                RequestID = this.RequestID,
                ErrorMessage = this.ErrorMessage,
                TimelineTotalDuration = this.TimelineTotalDuration
            };
            var json = JsonConvert.SerializeObject(m, Formatting.None, new JsonSerializerSettings() { DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate });
            return json;
        }

        public void LoadJson(string data)
        {
            var eventConverter = new ServerTimingConverter();
            var deseralizeSettings = new JsonSerializerSettings();
            deseralizeSettings.Converters.Add(eventConverter);
            deseralizeSettings.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple;
            deseralizeSettings.TypeNameHandling = TypeNameHandling.Auto;

            ServerTimesSnapshot m = JsonConvert.DeserializeObject<ServerTimesSnapshot>(data, deseralizeSettings);

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
            ErrorMessage = m.ErrorMessage;

            AllStorageEngineEvents.Clear();
            if (m.StoreageEngineEvents != null)
                AllStorageEngineEvents.AddRange(m.StoreageEngineEvents);
            else
                AllStorageEngineEvents.AddRange(m.StorageEngineEvents);

            AllStorageEngineEvents.Apply(se =>
            {
                se.HighlightQuery = se.QueryRichText?.ContainsCallback() ?? false;
                if (se.Class == DaxStudioTraceEventClass.DirectQueryEnd
                    && se.ClassSubclass.QueryLanguage == DaxStudioTraceEventClassSubclass.Language.SQL
                    && _globalOptions.FormatDirectQuerySql)
                {
                    se.QueryRichText = SqlFormatter.FormatSql(se.TextData ?? se.Query);
                }
            });
            if (m.FileFormatVersion <= 4)
            {
                AllStorageEngineEvents.Apply(se => UpdateTimelineTotalDuration(new DaxStudioTraceEventArgs(se.Class.ToString(), se.Subclass.ToString(), se.Duration ?? 0, se.CpuTime ?? 0, se.TextData ?? se.Query, string.Empty, se.StartTime)));
                UpdateTimelineDurations(QueryStartDateTime, QueryEndDateTime, TimelineTotalDuration);
            }

            if (ShowQueryGroupColumn) _ = RunQueryGroupingAsync();
        }

        private ServerTimingDetailsViewModel _serverTimingDetails;
        public ServerTimingDetailsViewModel ServerTimingDetails
        {
            get { return _serverTimingDetails; }
            set
            {
                if (_serverTimingDetails != null) { _serverTimingDetails.PropertyChanged -= ServerTimingDetails_PropertyChanged; }
                _serverTimingDetails = value;
                _serverTimingDetails.ShowObjectName = Options.ShowObjectNameInServerTimings;
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

        protected virtual void ServerTimingDetails_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "ShowScan":
                case "ShowBatch":
                case "ShowCache":
                case "ShowInternal":
                case "ShowMetrics":
                case "ShowSql":
                case "ShowTabularQueries":
                    NotifyOfPropertyChange(nameof(StorageEngineEvents));
                    break;
                case "ShowObjectName":
                    ShowObjectName = ServerTimingDetails.ShowObjectName;
                    break;
            }
        }

        public override void ClearAll()
        {
            Log.Debug(Constants.LogMessageTemplate, nameof(ServerTimesModel), nameof(ClearAll), "Clearing all event data");
            _queryEndActivityId = string.Empty;
            _internalQueryActivityIds.Clear();
            AllStorageEngineEvents.Clear();
            FormulaEngineDuration = 0;
            StorageEngineDuration = 0;
            StorageEngineNetParallelDuration = 0;
            TotalDirectQueryDuration = 0;
            StorageEngineCpu = 0;
            StorageEngineQueryCount = 0;
            VertipaqCacheMatches = 0;
            TotalDuration = 0;
            TimelineTotalDuration = 0;
            ParallelStorageEngineEventsDetected = false;
            OnClearAllStorageEvents();

            NotifyOfPropertyChange(nameof(AllStorageEngineEvents));
            NotifyOfPropertyChange(nameof(StorageEngineEvents));
            NotifyOfPropertyChange(nameof(CanExport));
            NotifyOfPropertyChange(nameof(CanShowTraceDiagnostics));
            NotifyOfPropertyChange(nameof(CanShowQueryDependencies));
        }

        /// <summary>
        /// Hook for the UI shell to reset visual state (e.g. clear the storage-event heatmap)
        /// when ClearAll runs. The default implementation does nothing in Core.
        /// </summary>
        protected virtual void OnClearAllStorageEvents() { }

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

        public bool IsCopyResultsForCommentsVisible => Options.ShowCopyMetricsComments;
        public bool IsCopyResultsForCommentsDataVisible => Options.ShowCopyMetricsComments;

        public override bool CanCopyResults => CanExport;
        public override bool IsCopyResultsVisible => true;

        public override bool CanExport => AllStorageEngineEvents.Count > 0;
        public override void ExportTraceDetails(string filePath)
        {
            File.WriteAllText(filePath, GetJson());
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
                    File.WriteAllText(filePath, tse.QueryRichText);
                }
            }
        }

        public bool CanShowTraceDiagnostics => AllStorageEngineEvents.Count > 0;

        private string _activityId = string.Empty;
        public string ActivityID
        {
            get => _activityId;
            set
            {
                _activityId = value;
                NotifyOfPropertyChange();
            }
        }

        private string _requestId = string.Empty;
        public string RequestID
        {
            get => _requestId;
            set
            {
                _requestId = value;
                NotifyOfPropertyChange();
            }
        }

        public DateTime StartDatetime { get => QueryStartDateTime; }
        public string CommandText { get; set; }
        public string Parameters { get; set; }
        public long TimelineTotalDuration { get; protected set; }

        public bool CanShowQueryDependencies => AllStorageEngineEvents.Count > 0;
        public bool CanShowInModelDiagram => AllStorageEngineEvents.Count > 0;

        public bool ShowTimelineOnRows { get => this.StorageEventTimelineStyle != StorageEventTimelineStyle.None; }

        private StorageEventTimelineStyle _storageEventTimelineStyle;
        public StorageEventTimelineStyle StorageEventTimelineStyle
        {
            get => _storageEventTimelineStyle;
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

        public double StorageEventHeatmapHeight
        {
            get
            {
                switch (this.StorageEventTimelineStyle)
                {
                    case StorageEventTimelineStyle.Thin: return 8.0;
                    case StorageEventTimelineStyle.FullHeight: return 24.0;
                    default: return 12.0;
                }
            }
        }

        public double TimelineVerticalMargin
        {
            get
            {
                switch (this.StorageEventTimelineStyle)
                {
                    case StorageEventTimelineStyle.Thin: return 6.0;
                    case StorageEventTimelineStyle.FullHeight: return 6.0;
                    default: return 6.0;
                }
            }
        }

        public bool HasData => TotalDuration > 0 || StorageEngineEvents?.Count > 0;

        public bool ShowObjectName
        {
            get { return _globalOptions.ShowObjectNameInServerTimings; }
            set
            {
                _globalOptions.ShowObjectNameInServerTimings = value;
                NotifyOfPropertyChange();
            }
        }

        public bool ShowQueryGroupColumn
        {
            get { return _globalOptions.ShowQueryGroupColumn; }
            set
            {
                _globalOptions.ShowQueryGroupColumn = value;
                NotifyOfPropertyChange();
            }
        }
        public bool ShowSql { get => ServerTimingDetails.ShowSql; set => ServerTimingDetails.ShowSql = value; }
        public bool ShowTabularQueries { get => ServerTimingDetails.ShowTabularQueries; set => ServerTimingDetails.ShowTabularQueries = value; }

        public void ToggleSql()
        {
            ShowSql = !ShowSql;
            NotifyOfPropertyChange(nameof(ShowSql));
            NotifyOfPropertyChange(nameof(StorageEngineEvents));
        }

        public void ToggleTabularQueries()
        {
            ShowTabularQueries = !ShowTabularQueries;
            NotifyOfPropertyChange(nameof(ShowTabularQueries));
            NotifyOfPropertyChange(nameof(StorageEngineEvents));
        }

        public TooltipStruct Tooltips { get; } = new TooltipStruct();

        public struct TooltipStruct
        {
            public string Line => "This is the order the events occurred in the trace.";
            public string Subclass => "The subclass of the event, which indicates the type of operation.";
            public string Duration => "The total duration of the operation in milliseconds.";
            public string Cpu => "The CPU time consumed by the event in milliseconds.";
            public string Parallelism => "The parallelism factor indicates how many threads were used to process the event. A value of 1 means single-threaded execution, while higher values indicate multi-threaded execution.";
            public string Rows => "The number of rows processed by the event. This is typically relevant for scan operations.";
            public string Kb => "The size of the data processed by the event in kilobytes.";
            public string Object => "The name of the object associated with the event.";
            public string Timeline => "The timeline of the event, which shows when it occurred relative to other events in the trace. And shows the pattern of Formula Engine vs Storage Engine operations";
            public string Query => "The query text associated with the event. This may include xmSQL or SQL queries depending on the event type.";
        }
    }
}
