using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using System.Text.RegularExpressions;
using System.IO;
using System.IO.Packaging;
using System.Text;
using System.Windows.Media;
using Newtonsoft.Json;
using DaxStudio.UI.Interfaces;
using DaxStudio.QueryTrace;
using DaxStudio.Interfaces;
using DaxStudio.UI.Utils;
using Serilog;
using DaxStudio.Common.Enums;
using DaxStudio.UI.Extensions;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;

namespace DaxStudio.UI.ViewModels
{
    public class QueryPlanRow : IQueryPlanRow {
        public string Operation { get;  set; }
        public string IndentedOperation { get;  set; }
        public int Level { get;  set; }
        public int RowNumber { get;  set; }
        public int NextSiblingRowNumber { get; set; }
        public bool HighlightRow { get; set; }

        private const int SPACE_PER_LEVEL = 4;
        public virtual void PrepareQueryPlanRow(string line, int rowNumber) {
            RowNumber = rowNumber;
            Level = line.Where(c => c == '\t').Count();
            Operation = line.Trim();
            IndentedOperation = new string(' ', Level * SPACE_PER_LEVEL) + Operation;
        }
        static public BindableCollection<T> PrepareQueryPlan<T>(string physicalQueryPlan,int startingRowNumber)
            where T : QueryPlanRow, new()
        {
            int rowNumber = startingRowNumber;
            return new BindableCollection<T>((
                from row in physicalQueryPlan.Split(new[] { '\r', '\n' })
                where row.Trim().Length > 0
                select row)
            .Select((line) => {
                var operation = new T();
                operation.PrepareQueryPlanRow(line, ++rowNumber);
                return operation;
            }).ToList());
        }
        static public BindableCollection<T> PrepareQueryPlan<T>(string physicalQueryPlan)
    where T : QueryPlanRow, new()
        {
            return PrepareQueryPlan<T>(physicalQueryPlan, 0);
        }

        static public BindableCollection<T> PreparePhysicalQueryPlan<T>(string physicalQueryPlan, int startingRowNumber)
    where T : PhysicalQueryPlanRow, new()
        {
            BindableCollection<T> rawQueryPlan = PrepareQueryPlan<T>(physicalQueryPlan, startingRowNumber);

            // Evaluate cardinality of CrossApply nodes for CrossJoin logical operators
            var crossAplyNodes =
                from row in rawQueryPlan
                where row.Operation.StartsWith(@"CrossApply") && row.Operation.Contains(@"LogOp=CrossJoin")
                select row;

            foreach (var row in crossAplyNodes)
            {
                // Compute the product of the CrossApply child nodes cardinality
                int? lastChildRow = rawQueryPlan.FirstOrDefault(s => s.RowNumber > row.RowNumber && s.Level == row.Level)?.RowNumber;
                var childNodes =
                    from childRow in rawQueryPlan
                    where childRow.Level == row.Level + 1 
                        && childRow.RowNumber > row.RowNumber 
                        && ((!lastChildRow.HasValue) || childRow.RowNumber <= lastChildRow.Value)
                    select childRow;
                long cardinality = childNodes.Aggregate((long)1, (result, next) => result * next.Records.GetValueOrDefault(1));
                row.Records = cardinality;
            }
            return rawQueryPlan;
        }

        // Is this necessary???
        static public BindableCollection<T> PreparePhysicalQueryPlan<T>(string physicalQueryPlan) 
            where T : PhysicalQueryPlanRow, new() {
            return PrepareQueryPlan<T>(physicalQueryPlan, 0);
        }
    }

    public class PhysicalQueryPlanRow : QueryPlanRow {
        public long? Records { get; set; }

        private const string RecordsPrefix = @"#Records=";
        private const string searchRecords = RecordsPrefix + @"([0-9]*)";
        static Regex recordsRegex = new Regex(searchRecords, RegexOptions.Compiled);

        private const string RecsPrefix = @"#Recs=";
        private const string searchRecs = RecsPrefix + @"([0-9]*)";
        static Regex recsRegex = new Regex(searchRecs, RegexOptions.Compiled);

        private const string CachePrefix = @"Cache:|VertipaqResult:|DirectQueryResult|DataPostFilter:";
        static Regex cacheRegex = new Regex(CachePrefix, RegexOptions.Compiled);

        public override void PrepareQueryPlanRow(string line, int rowNumber) { 
            base.PrepareQueryPlanRow(line, rowNumber);
            var matchRecords = recordsRegex.Match(line);
            if (matchRecords.Success) {
                Records = int.Parse(matchRecords.Value.Substring(RecordsPrefix.Length));
            }
            else
            {
                var matchRecs = recsRegex.Match(line);
                if (matchRecs.Success)
                {
                    Records = int.Parse(matchRecs.Value.Substring(RecsPrefix.Length));
                }
            }
            var cacheRecords = cacheRegex.Match(line);
            if (cacheRecords.Success)
            {
                HighlightRow = true;
            }
        }
    }

    public class LogicalQueryPlanRow : QueryPlanRow {

    }

    //[Export(typeof(ITraceWatcher)),PartCreationPolicy(CreationPolicy.NonShared)]
    class QueryPlanTraceViewModel: TraceWatcherBaseViewModel, 
        ISaveState,
        ITraceDiagnostics,
        IHaveData
    {
        [ImportingConstructor]
        public QueryPlanTraceViewModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions, IWindowManager windowManager) 
            : base(eventAggregator, globalOptions, windowManager) 

        {
            _physicalQueryPlanRows = new BindableCollection<PhysicalQueryPlanRow>();
            _logicalQueryPlanRows = new BindableCollection<LogicalQueryPlanRow>();
        }

        protected override List<DaxStudioTraceEventClass> GetMonitoredEvents()
        {
            return new List<DaxStudioTraceEventClass> 
                { DaxStudioTraceEventClass.DAXQueryPlan
                , DaxStudioTraceEventClass.QueryBegin
                , DaxStudioTraceEventClass.QueryEnd };
        }
    

        // This method is called after the WaitForEvent is seen (usually the QueryEnd event)
        // This is where you can do any processing of the events before displaying them to the UI
        protected override void ProcessResults()
        {
            if (PhysicalQueryPlanRows?.Count > 0 || LogicalQueryPlanRows?.Count > 0) return;
            // results have not been cleared so this is probably and end event from some other
            // action like a tooltip populating

            while (!Events.IsEmpty)
            {
                Events.TryDequeue(out var traceEvent);
                if (traceEvent.EventClass == DaxStudioTraceEventClass.DAXQueryPlan
                    && traceEvent.EventSubclass == DaxStudioTraceEventSubclass.DAXVertiPaqLogicalPlan)
                {
                    LogicalQueryPlanText = traceEvent.TextData;
                    PrepareLogicalQueryPlan(traceEvent.TextData);
                    NotifyOfPropertyChange(() => LogicalQueryPlanText);
                }
                if (traceEvent.EventClass == DaxStudioTraceEventClass.DAXQueryPlan 
                    && traceEvent.EventSubclass == DaxStudioTraceEventSubclass.DAXVertiPaqPhysicalPlan)
                {
                    PhysicalQueryPlanText = traceEvent.TextData;
                    PreparePhysicalQueryPlan(traceEvent.TextData);
                    NotifyOfPropertyChange(() => PhysicalQueryPlanText);
                }
                if (traceEvent.EventClass == DaxStudioTraceEventClass.QueryBegin)
                {
                    Parameters = traceEvent.RequestParameters;
                    StartDatetime = traceEvent.StartTime;
                }
                if (traceEvent.EventClass == DaxStudioTraceEventClass.QueryEnd)
                {
                    ActivityID = traceEvent.ActivityId;
                    CommandText = traceEvent.TextData;
                    TotalDuration = traceEvent.Duration;
                    NotifyOfPropertyChange(() => TotalDuration);
                }
            }
            NotifyOfPropertyChange(nameof(CanExport));
            NotifyOfPropertyChange(nameof(CanShowTraceDiagnostics));
        }

        public override void OnReset() {
            IsBusy = false;
            ClearAll();
            ProcessResults();
        }

        protected void PreparePhysicalQueryPlan(string physicalQueryPlan) 
        {
            _physicalQueryPlanRows.AddRange( QueryPlanRow.PreparePhysicalQueryPlan<PhysicalQueryPlanRow>(physicalQueryPlan, _physicalQueryPlanRows.Count));
            UpdateNextSibling(_physicalQueryPlanRows);
            NotifyOfPropertyChange(() => PhysicalQueryPlanRows);
        }

        protected void PrepareLogicalQueryPlan(string logicalQueryPlan) {
            _logicalQueryPlanRows = QueryPlanRow.PrepareQueryPlan<LogicalQueryPlanRow>(logicalQueryPlan);
            UpdateNextSibling(_logicalQueryPlanRows);
            NotifyOfPropertyChange(() => LogicalQueryPlanRows);
        }

        // this method updates each row with it's next sibling so that we know how to highlight
        // all the child rows under the currently selected operation
        private void UpdateNextSibling(IEnumerable<IQueryPlanRow> logicalQueryPlanRows)
        {
            var siblingStack = new Stack<IQueryPlanRow>();
            IQueryPlanRow prevRow = null;
            var prevLevel = -1;
            foreach (var row in logicalQueryPlanRows)
            {

                if (row.Level <= prevLevel && siblingStack.Any())
                {
                    if (row.Level == prevLevel)
                    {
                        var prev = siblingStack.Pop();
                        prev.NextSiblingRowNumber = prev.RowNumber;
                    }
                    else
                    {
                        while (true)
                        {
                            var prev = siblingStack.Pop();
                            if (prev.RowNumber == row.RowNumber - 1) {
                                prev.NextSiblingRowNumber = prev.RowNumber;
                            }
                            else {
                                prev.NextSiblingRowNumber = row.RowNumber;
                            }
                            if (prev.Level <= row.Level) break;
                            if (!siblingStack.Any()) break;
                        }
                    }
                    
                }

                siblingStack.Push(row);
                prevRow = row;
                prevLevel = row.Level;
            }

            // anything remaining on the stack will cover all operations 
            while (siblingStack.Any())
            {
                var row = siblingStack.Pop();
                row.NextSiblingRowNumber = prevRow.RowNumber + 1;
            }
        }

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

        public string PhysicalQueryPlanText { get; private set; }
        public string LogicalQueryPlanText { get; private set; }
        public long TotalDuration { get; private set; }

        private BindableCollection<PhysicalQueryPlanRow> _physicalQueryPlanRows;
        private BindableCollection<LogicalQueryPlanRow> _logicalQueryPlanRows;

        private PhysicalQueryPlanRow _selectedPhysicalRow;
        public PhysicalQueryPlanRow SelectedPhysicalRow {
            get {
                return _selectedPhysicalRow;
            }
            set {
                _selectedPhysicalRow = value;
                NotifyOfPropertyChange(() => SelectedPhysicalRow);
            }
        }

        private LogicalQueryPlanRow _selectedLogicalRow;
        public LogicalQueryPlanRow SelectedLogicalRow {
            get {
                return _selectedLogicalRow;
            }
            set {
                _selectedLogicalRow = value;
                NotifyOfPropertyChange(() => SelectedLogicalRow);
            }
        }

        public BindableCollection<PhysicalQueryPlanRow> PhysicalQueryPlanRows {
            get {
                return _physicalQueryPlanRows;
            }
            private set { _physicalQueryPlanRows = value; }
        }

        public BindableCollection<LogicalQueryPlanRow> LogicalQueryPlanRows {
            get {
                return _logicalQueryPlanRows;
            }
            private set { _logicalQueryPlanRows = value; }
        }
        
        // IToolWindow interface
        public override string Title => "Query Plan";
        public override string ImageResource => "query_planDrawingImage";
        public override string TraceSuffix => "plans";
        public override string ContentId => "query-plan";
        public override string KeyTip => "QP";
        public override int SortOrder => 20;
        public override string ToolTipText => "Runs a server trace to capture the Logical and Physical DAX Query Plans";

        public override bool FilterForCurrentSession { get { return true; } }

        protected override bool IsFinalEvent(DaxStudioTraceEventArgs traceEvent)
        {
            return traceEvent.EventClass == DaxStudioTraceEventClass.QueryEnd ||
                   traceEvent.EventClass == DaxStudioTraceEventClass.Error;
        }

        #region ISaveState Methods

        void ISaveState.Save(string filename)
        {
            string json = ((ISaveState)this).GetJson();
            File.WriteAllText(filename + ".queryPlans", json);
        }

        public string GetJson()
        {
            var m = new QueryPlanModel()
            {
                PhysicalQueryPlanRows = this.PhysicalQueryPlanRows,
                LogicalQueryPlanRows = this.LogicalQueryPlanRows,
                ActivityID = this.ActivityID,
                CommandText = this.CommandText,
                Parameters = this.Parameters,
                StartDatetime = this.StartDatetime
            };
            var json = JsonConvert.SerializeObject(m, Formatting.Indented);
            return json;
        }

        void ISaveState.Load(string filename)
        {
            filename = filename + ".queryPlans";
            if (!File.Exists(filename)) return;

            _eventAggregator.PublishOnUIThreadAsync(new ShowTraceWindowEvent(this));
            string data = File.ReadAllText(filename);
            LoadJson(data);
        }

        public void LoadJson(string data)
        {
            QueryPlanModel m = JsonConvert.DeserializeObject<QueryPlanModel>(data);

            PhysicalQueryPlanRows = m.PhysicalQueryPlanRows;
            LogicalQueryPlanRows = m.LogicalQueryPlanRows;
            ActivityID = m.ActivityID;
            StartDatetime = m.StartDatetime;
            CommandText = m.CommandText;
            Parameters = m.Parameters;

            NotifyOfPropertyChange(nameof(PhysicalQueryPlanRows));
            NotifyOfPropertyChange(nameof(LogicalQueryPlanRows));
            NotifyOfPropertyChange(nameof(CanExport));
            NotifyOfPropertyChange(nameof( CanShowTraceDiagnostics));
        }

        public void SavePackage(Package package)
        {

            Uri uriTom = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.QueryPlan, UriKind.Relative));
            using (TextWriter tw = new StreamWriter(package.CreatePart(uriTom, "application/json", CompressionOption.Maximum).GetStream(), Encoding.UTF8))
            {
                tw.Write(((ISaveState)this).GetJson());
                tw.Close();
            }
        }

        public void LoadPackage(Package package)
        {
            var uri = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.QueryPlan, UriKind.Relative));
            if (!package.PartExists(uri)) return;

            _eventAggregator.PublishOnUIThreadAsync(new ShowTraceWindowEvent(this));
            var part = package.GetPart(uri);
            using (TextReader tr = new StreamReader(part.GetStream()))
            {
                string data = tr.ReadToEnd();
                LoadJson(data);
            }

        }
        #endregion

        #region Title Bar Button Methods

        public override void ClearAll()
        {
            Events.Clear();
            _physicalQueryPlanRows.Clear();
            _logicalQueryPlanRows.Clear();
            NotifyOfPropertyChange(() => PhysicalQueryPlanRows);
            NotifyOfPropertyChange(() => LogicalQueryPlanRows);
        }

        public override void CopyAll()
        {
            Log.Warning("CopyAll method not implemented for QueryPlanTraceViewModel");
        }

        public override void CopyResults()
        {
            // QueryPlan does not support this operation
            throw new NotImplementedException();
        }

        #endregion

        public override bool CanExport => _logicalQueryPlanRows.Count > 0;

        public string ActivityID { get; set; }

        public DateTime StartDatetime { get; set; }

        public string CommandText { get; set; }
        public string Parameters { get; set; }

        public override void ExportTraceDetails(string filePath)
        {
            File.WriteAllText(filePath, GetJson());
        }
        public override void CopyEventContent()
        {
            Log.Warning("CopyEventContent not implemented for QueryPlanTraceViewModel");
            throw new NotImplementedException();
        }

        public bool CanShowTraceDiagnostics => CanExport;

        public bool HasData => LogicalQueryPlanRows?.Count > 0 || PhysicalQueryPlanRows?.Count > 0;

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
        //protected override void OnUpdateGlobalOptions(UpdateGlobalOptions message)
        //{
        //    base.OnUpdateGlobalOptions(message);
        //    NotifyOfPropertyChange(nameof(ShowQueryPlanNextLine));
        //    NotifyOfPropertyChange(nameof(ShowQueryPlanLineLevel));
        //}


        //public bool ShowQueryPlanNextLine => GlobalOptions.ShowQueryPlanNextLine;
        //public bool ShowQueryPlanLineLevel => GlobalOptions.ShowQueryPlanLineLevel;

    }
}
