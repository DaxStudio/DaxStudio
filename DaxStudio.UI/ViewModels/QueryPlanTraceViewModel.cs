using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using Microsoft.AnalysisServices;
using System.Text.RegularExpressions;

namespace DaxStudio.UI.ViewModels
{
    public class QueryPlanRow {
        public string Operation { get; private set; }
        public string IndentedOperation { get; private set; }
        public int Level { get; private set; }
        public int RowNumber { get; private set; }

        private const int SPACE_PER_LEVEL = 4;
        public virtual void PrepareQueryPlanRow(string line, int rowNumber) {
            RowNumber = rowNumber;
            Level = line.Where(c => c == '\t').Count();
            Operation = line.Trim();
            IndentedOperation = new string(' ', Level * SPACE_PER_LEVEL) + Operation;
        }
        static public IEnumerable<T> PrepareQueryPlan<T>(string physicalQueryPlan) 
            where T : QueryPlanRow, new() {
            int rowNumber = 0;
            return (
                from row in physicalQueryPlan.Split(new[] { '\r', '\n' })
                where row.Trim().Length > 0
                select row)
            .Select( (line) => {
                var operation = new T();
                operation.PrepareQueryPlanRow(line, ++rowNumber);
                return operation; 
            }).ToList();
        }
    }

    public class PhysicalQueryPlanRow : QueryPlanRow {
        public long? Records { get; private set; }

        private const string RecordsPrefix = @"#Records=";
        private const string searchRecords = RecordsPrefix + @"([0-9]*)";
        static Regex recordsRegex = new Regex(searchRecords,RegexOptions.Compiled);

        public override void PrepareQueryPlanRow(string line, int rowNumber) { 
            base.PrepareQueryPlanRow(line, rowNumber);
            var matchRecords = recordsRegex.Match(line);
            if (matchRecords.Success) {
                Records = int.Parse(matchRecords.Value.Substring(RecordsPrefix.Length));
            }
        }
    }

    public class LogicalQueryPlanRow : QueryPlanRow {

    }

    //[Export(typeof(ITraceWatcher)),PartCreationPolicy(CreationPolicy.NonShared)]
    class QueryPlanTraceViewModel: TraceWatcherBaseViewModel
    {
        [ImportingConstructor]
        public QueryPlanTraceViewModel(IEventAggregator eventAggregator) : base(eventAggregator)
        {
            _physicalQueryPlanRows = new BindableCollection<PhysicalQueryPlanRow>();
            _logicalQueryPlanRows = new BindableCollection<LogicalQueryPlanRow>();
        }

        protected override List<TraceEventClass> GetMonitoredEvents()
        {
            return new List<TraceEventClass> 
                { TraceEventClass.DAXQueryPlan
                , TraceEventClass.QueryEnd };
        }
    

        // This method is called after the WaitForEvent is seen (usually the QueryEnd event)
        // This is where you can do any processing of the events before displaying them to the UI
        protected override void ProcessResults()
        {

            foreach (var traceEvent in Events)
            {
                if (traceEvent.EventClass == TraceEventClass.DAXQueryPlan && traceEvent.EventSubclass == TraceEventSubclass.DAXVertiPaqLogicalPlan)
                {
                    LogicalQueryPlanText = traceEvent.TextData;
                    PrepareLogicalQueryPlan(traceEvent.TextData);
                    NotifyOfPropertyChange(() => LogicalQueryPlanText);
                }
                if (traceEvent.EventClass == TraceEventClass.DAXQueryPlan && traceEvent.EventSubclass == TraceEventSubclass.DAXVertiPaqPhysicalPlan)
                {
                    PhysicalQueryPlanText = traceEvent.TextData;
                    PreparePhysicalQueryPlan(traceEvent.TextData);
                    NotifyOfPropertyChange(() => PhysicalQueryPlanText);
                }
                if (traceEvent.EventClass == TraceEventClass.QueryEnd)
                {
                    TotalDuration = traceEvent.Duration;
                    NotifyOfPropertyChange(() => TotalDuration);
                }
            }
        }

        protected void PreparePhysicalQueryPlan(string physicalQueryPlan) 
        {
            _physicalQueryPlanRows = QueryPlanRow.PrepareQueryPlan<PhysicalQueryPlanRow>(physicalQueryPlan);
            NotifyOfPropertyChange(() => PhysicalQueryPlanRows);
        }

        protected void PrepareLogicalQueryPlan(string logicalQueryPlan) {
            _logicalQueryPlanRows = QueryPlanRow.PrepareQueryPlan<LogicalQueryPlanRow>(logicalQueryPlan);
            NotifyOfPropertyChange(() => LogicalQueryPlanRows);
        }

        public string PhysicalQueryPlanText { get; private set; }
        public string LogicalQueryPlanText { get; private set; }
        public long TotalDuration { get; private set; }

        private IEnumerable<PhysicalQueryPlanRow> _physicalQueryPlanRows;
        private IEnumerable<LogicalQueryPlanRow> _logicalQueryPlanRows;

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

        public IObservableCollection<PhysicalQueryPlanRow> PhysicalQueryPlanRows {
            get {
                var pqp = from r in _physicalQueryPlanRows
                          select r;
                return new BindableCollection<PhysicalQueryPlanRow>(pqp);
            }
        }

        public IObservableCollection<LogicalQueryPlanRow> LogicalQueryPlanRows {
            get {
                var lqp = from r in _logicalQueryPlanRows
                          select r;
                return new BindableCollection<LogicalQueryPlanRow>(lqp);
            }
        }
        
        // IToolWindow interface
        public override string Title
        {
            get { return "Query Plan"; }
            set { }
        }
        
    }
}
