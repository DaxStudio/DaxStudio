using System.Collections.Generic;
using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using System.Linq;
using Microsoft.AnalysisServices;
using System.Text.RegularExpressions;

namespace DaxStudio.UI.ViewModels
{
    public class TraceStorageEngineEvent {
        public TraceEventSubclass Subclass { get; private set; }
        public string Query { get; private set; }
        public long Duration { get; private set; }
        public long CpuTime { get; private set; }
        public int RowNumber { get; private set; }

        public TraceStorageEngineEvent( TraceEventArgs ev, int rowNumber ) {
            RowNumber = rowNumber;
            Subclass = ev.EventSubclass;
            Query = ev.TextData.RemoveDaxGuids().RemoveXmSqlSquareBrackets();
            // Skip Duration/Cpu Time for Cache Match
            if (Subclass != TraceEventSubclass.VertiPaqCacheExactMatch) {
                Duration = ev.Duration;
                CpuTime = ev.CpuTime;
            }
        }
    }

    public static class TraceStorageEngineExtensions {
        const string searchGuid = @"_(\{{0,1}([0-9a-fA-F]){8}-([0-9a-fA-F]){4}-([0-9a-fA-F]){4}-([0-9a-fA-F]){4}-([0-9a-fA-F]){12}\}{0,1})";
        const string searchXmSqlSquareBracketsNoSpace = @"(?<!\.)\[([^\[^ ])*\]";
        const string searchXmSqlSquareBracketsWithSpace = @"(?<!\.)\[([^\[])*\]";
        const string searchXmSqlDotSeparator = @"\.\[";
        const string searchXmSqlParenthesis = @"\ *[\(\)]\ *";
        //const string searchDaxQueryPlanSquareBrackets = @"^\'\[([^\[^ ])*\]";
        //const string searchQuotedIdentifiers = @"\'([^ ])*\'";

        static Regex guidRemoval = new Regex(searchGuid, RegexOptions.Compiled);
        static Regex xmSqlSquareBracketsWithSpaceRemoval = new Regex(searchXmSqlSquareBracketsWithSpace, RegexOptions.Compiled);
        static Regex xmSqlSquareBracketsNoSpaceRemoval = new Regex(searchXmSqlSquareBracketsNoSpace, RegexOptions.Compiled);
        static Regex xmSqlDotSeparator = new Regex(searchXmSqlDotSeparator, RegexOptions.Compiled);
        static Regex xmSqlParenthesis = new Regex(searchXmSqlParenthesis, RegexOptions.Compiled);

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

        public static string RemoveXmSqlSquareBrackets(this string daxQuery) {
            // TODO: probably it is not necessary to use RemoveSquareBrackets
            // look for a Regex replace expression looking at Regex doc (written on a plane offline)
            string daxQueryNoBracketsOnTableNames = xmSqlSquareBracketsNoSpaceRemoval.Replace(
                        daxQuery,
                        RemoveSquareBracketsNoSpace
                    );
            string daxQueryNoBrackets = xmSqlSquareBracketsWithSpaceRemoval.Replace(
                            daxQueryNoBracketsOnTableNames,
                            RemoveSquareBracketsWithSpace);
            string daxQueryNoDots = xmSqlDotSeparator.Replace(daxQueryNoBrackets, "[");
            string result = xmSqlParenthesis.Replace(daxQueryNoDots, FixSpaceParenthesis);
            return result;
        }
    }

    //[Export(typeof(ITraceWatcher)),PartCreationPolicy(CreationPolicy.NonShared)]
    class ServerTimesViewModel
        : TraceWatcherBaseViewModel 
        
    {
        [ImportingConstructor]
        public ServerTimesViewModel(IEventAggregator eventAggregator) : base(eventAggregator)
        {
            _storageEngineEvents = new BindableCollection<TraceStorageEngineEvent>();
        }

        protected override List<TraceEventClass> GetMonitoredEvents()
        {
            return new List<TraceEventClass> 
                { TraceEventClass.QuerySubcube
                , TraceEventClass.VertiPaqSEQueryEnd
                , TraceEventClass.VertiPaqSEQueryCacheMatch
                , TraceEventClass.QueryEnd };
        }

        // This method is called after the WaitForEvent is seen (usually the QueryEnd event)
        // This is where you can do any processing of the events before displaying them to the UI
        protected override void ProcessResults()
        {
            FormulaEngineDuration = 0;
            StorageEngineDuration = 0;
            StorageEngineCpu = 0;
            StorageEngineQueryCount = 0;
            VertipaqCacheMatches = 0;
            TotalDuration = 0;
            _storageEngineEvents.Clear();

            foreach (var traceEvent in Events)
            {
                if (traceEvent.EventClass == TraceEventClass.VertiPaqSEQueryEnd)
                {
                    if (traceEvent.EventSubclass == TraceEventSubclass.VertiPaqScan) {
                        StorageEngineDuration += traceEvent.Duration;
                        StorageEngineCpu += traceEvent.CpuTime;
                        StorageEngineQueryCount++;
                    }
                    _storageEngineEvents.Add(new TraceStorageEngineEvent(traceEvent,_storageEngineEvents.Count()+1));
                }
                    
                if (traceEvent.EventClass == TraceEventClass.QueryEnd)
                {
                    TotalDuration = traceEvent.Duration;
                    FormulaEngineDuration = traceEvent.CpuTime;
                }
                if (traceEvent.EventClass == TraceEventClass.VertiPaqSEQueryCacheMatch)
                {
                    VertipaqCacheMatches++;
                    _storageEngineEvents.Add(new TraceStorageEngineEvent(traceEvent,_storageEngineEvents.Count()+1));
                }
            }
            Events.Clear();
            NotifyOfPropertyChange(() => StorageEngineDuration);
            NotifyOfPropertyChange(() => StorageEngineDurationPercentage);
            NotifyOfPropertyChange(() => FormulaEngineDuration);
            NotifyOfPropertyChange(() => FormulaEngineDurationPercentage);
            NotifyOfPropertyChange(() => StorageEngineCpu);
            NotifyOfPropertyChange(() => TotalDuration);
            NotifyOfPropertyChange(() => VertipaqCacheMatches);
            NotifyOfPropertyChange(() => VertipaqCacheMatchesPercentage);
            NotifyOfPropertyChange(() => StorageEngineQueryCount);
            NotifyOfPropertyChange(() => StorageEngineEvents);
        }

        public double StorageEngineDurationPercentage {
            get
            {
                return TotalDuration == 0? 0: (double)StorageEngineDuration / (double)TotalDuration;
            }
        }
        public double FormulaEngineDurationPercentage {
            // marcorusso: we use the formula engine total provided by Query End event in CPU Time
            // get { return TotalDuration == 0 ? 0:(TotalDuration-StorageEngineDuration)/TotalDuration;}
            get {
                return TotalDuration == 0 ? 0 : (double)FormulaEngineDuration / (double)TotalDuration;
            }
        }
        public double VertipaqCacheMatchesPercentage {
            get {
                return StorageEngineQueryCount == 0 ? 0 : (double)VertipaqCacheMatches / (double)StorageEngineQueryCount;
            }
        }
        public long TotalDuration { get; private set; }
        public long FormulaEngineDuration { get; private set; }
        public long StorageEngineDuration { get; private set; }
        public long StorageEngineCpu { get; private set; }
        public long StorageEngineQueryCount { get; private set; }
        public int VertipaqCacheMatches { get; set; }
 
        private readonly BindableCollection<TraceStorageEngineEvent> _storageEngineEvents;

        public IObservableCollection<TraceStorageEngineEvent> StorageEngineEvents 
        {
            get {
                var fse = from e in _storageEngineEvents
                          where
                          (e.Subclass == TraceEventSubclass.VertiPaqScanInternal && InternalVisible)
                          ||
                          (e.Subclass == TraceEventSubclass.VertiPaqCacheExactMatch && CacheVisible)
                          ||
                          (e.Subclass != TraceEventSubclass.VertiPaqCacheExactMatch && e.Subclass != TraceEventSubclass.VertiPaqScanInternal)
                          select e;
                return new BindableCollection<TraceStorageEngineEvent>(fse);
            }
        }

        private TraceStorageEngineEvent _selectedEvent;
        public TraceStorageEngineEvent SelectedEvent {
            get {
                return _selectedEvent;
            }
            set {
                _selectedEvent = value;
                NotifyOfPropertyChange(() => SelectedEvent);
            }
        }

        // Filter for visualization 
        private bool _cacheVisible;
        private bool _internalVisible;
        public bool CacheVisible {
            get { return _cacheVisible; }
            set { _cacheVisible = value; NotifyOfPropertyChange(() => StorageEngineEvents); }
        }
        public bool InternalVisible {
            get { return _internalVisible; }
            set { _internalVisible = value; NotifyOfPropertyChange(() => StorageEngineEvents); }
        }
    
        // IToolWindow interface
        public override string Title
        {
            get { return "Server Timings"; }
            set { }
        }

    }
}
