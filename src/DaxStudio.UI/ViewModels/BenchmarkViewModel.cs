using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FontAwesome.WPF;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using DaxStudio.UI.Interfaces;
using DaxStudio.Interfaces;
using System.Data;
using DaxStudio.UI.Extensions;
using DaxStudio.Common;
using Serilog;

namespace DaxStudio.UI.ViewModels
{
    public class BenchmarkViewModel : Screen, IDisposable
        ,IHandle<ServerTimingsEvent>
        ,IHandle<TraceChangedEvent>
    {

        [ImportingConstructor]
        public BenchmarkViewModel(IEventAggregator eventAggregator, DocumentViewModel document, RibbonViewModel ribbon)
        {
            EventAggregator = eventAggregator;
            EventAggregator.Subscribe(this);
            Document = document;
            Ribbon = ribbon;
            
            ColdRunStyle = Ribbon.RunStyles.FirstOrDefault(rs => rs.Icon == RunStyleIcons.ClearThenRun);
            WarmRunStyle = Ribbon.RunStyles.FirstOrDefault(rs => rs.Icon == RunStyleIcons.RunOnly);
            TimerRunTarget = Ribbon.ResultsTargets.FirstOrDefault(t => t.GetType() == typeof(ResultTargetTimer));
            ProgressIcon = FontAwesomeIcon.ClockOutline;
            ProgressSpin = false;
            ProgressMessage = "Ready";
            ProgressPercentage = 0;
            ProgressColor = "LightGray";
            RunSameWarmAndCold = true;
        }


        #region Public Methods
        public void Run()
        {
            try
            {
                ProgressIcon = FontAwesomeIcon.Refresh;
                ProgressSpin = true;
                ProgressMessage = "Starting Server Timings trace...";
                ProgressColor = "RoyalBlue";

                SetSelectedOutputTarget(OutputTarget.Timer);

                // clear out any existing benchmark tables
                BenchmarkDataSet.Tables.Clear();

                CreateSummaryOutputTable();
                CreateDetailOutputTable();

                // start server timings
                // once the server timings starts it will trigger the first query
                StartServerTimings();
            }
            catch( Exception ex)
            {
                Log.Error(ex, DaxStudio.Common.Constants.LogMessageTemplate, nameof(BenchmarkViewModel), nameof(Run), ex.Message);
                EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"An error occurred while attempting to run the benchmark: {ex.Message}"));
            }
        }

        public void Cancel()
        {
            IsCancelled = true;
            TryClose(true);
        }
        #endregion

        #region Internal Methods
        private void SetSelectedOutputTarget(OutputTarget targetType)
        {
            var target= Ribbon.ResultsTargets.FirstOrDefault(t => t.Icon == targetType);
            Ribbon.SelectedTarget = target;
        }


        private void StartServerTimings()
        {
            var serverTimings = Ribbon.TraceWatchers.FirstOrDefault(tw => tw.GetType() == typeof(ServerTimesViewModel));
            if (serverTimings.IsChecked)
            {
                // if the server timings trace is already active start running queries
                RunNextQuery();
            }
            else
            {
                ProgressMessage = "Waiting for Server Timings trace to start";
                serverTimings.IsChecked = true;
            }
        }

        private void RunNextQuery()
        {
            if (_currentColdRun < ColdCacheRuns)
            {
                _currentColdRun++;
                _currentRunStyle = ColdRunStyle;
            }
            else
            {
                _currentWarmRun++;
                _currentRunStyle = WarmRunStyle;
            }

            RefreshProgress();

            if (!_benchmarkingComplete)
            {
                EventAggregator.PublishOnUIThread(new RunQueryEvent(TimerRunTarget, _currentRunStyle));
            }

            if (ProgressPercentage == 1)
            {
                _benchmarkingComplete = true;
            }
            
        }

        private void RefreshProgress()
        {
            ProgressPercentage = (double)(_currentColdRun + _currentWarmRun) / (ColdCacheRuns + WarmCacheRuns);
            if (_currentColdRun <= ColdCacheRuns && _currentWarmRun == 0)
            {
                ProgressMessage = $"Running Cold Cache Query {_currentColdRun} of {ColdCacheRuns}";
                return;
            }
            if (_currentWarmRun <= WarmCacheRuns && !_benchmarkingComplete)
            {
                ProgressMessage = $"Running Warm Cache Query {_currentWarmRun} of {WarmCacheRuns}";
                return;
            }

            
            

        }

        private void BenchmarkingComplete()
        {
            // Stop listening to events
            EventAggregator.Unsubscribe(this);

            ProgressMessage = "Compiling Results";

            // todo - should we add an option to output directly to a file
            SetSelectedOutputTarget(OutputTarget.Grid);

            CalculateBenchmarkSummary();

            Document.ResultsDataSet = BenchmarkDataSet;


            ProgressSpin = false;
            ProgressIcon = FontAwesomeIcon.CheckCircle;
            ProgressMessage = "Benchmark Complete";
            ProgressColor = "Green";

            // todo - activeate results

            // close the Benchmarking dialog
            this.TryClose(true);
        }

        private void CalculateBenchmarkSummary()
        {
            var dt = BenchmarkDataSet.Tables["Details"];
            string[] statistics = { "Average", "StdDev", "Min", "Max" };
            var newDt2 = from d in dt.AsEnumerable()
                         from stat in statistics
                         select new { Cache = d["Cache"], Statistic = stat, TotalDuration = (int)d["TotalDuration"], StorageEngineDuration = (int)d["StorageEngineDuration"] };

            var newGrp = newDt2.GroupBy(x => new { Cache = x.Cache, Statistic = x.Statistic });

            DataTable summary = BenchmarkDataSet.Tables["Summary"]; 

            foreach (var grp in newGrp)
            {
                DataRow newRow = summary.Rows.Add();
                newRow["Cache"] = grp.Key.Cache;
                newRow["Statistic"] = grp.Key.Statistic;

                switch (grp.Key.Statistic)
                {
                    case "Average":
                        newRow["TotalDuration"] = grp.Average(x => x.TotalDuration);
                        newRow["StorageEngineDuration"] = grp.Average(x => x.StorageEngineDuration);
                        break;
                    case "StdDev":
                        newRow["TotalDuration"] = grp.StdDev(x => x.TotalDuration);
                        newRow["StorageEngineDuration"] = grp.StdDev(x => x.StorageEngineDuration);
                        break;
                    case "Min":
                        newRow["TotalDuration"] = grp.Min(x => x.TotalDuration);
                        newRow["StorageEngineDuration"] = grp.Min(x => x.StorageEngineDuration);
                        break;
                    case "Max":
                        newRow["TotalDuration"] = grp.Max(x => x.TotalDuration);
                        newRow["StorageEngineDuration"] = grp.Max(x => x.StorageEngineDuration);
                        break;
                    default:

                        break;
                }

            }

        }

        private void CreateSummaryOutputTable()
        {
            DataTable summary = new DataTable("Summary");
            summary.Columns.Add("Cache", typeof(string));
            summary.Columns.Add("Statistic", typeof(string));
            summary.Columns.Add("TotalDuration", typeof(double));
            summary.Columns["TotalDuration"].ExtendedProperties[Constants.FormatString] = "#,##0.00";
            summary.Columns.Add("StorageEngineDuration", typeof(double));
            summary.Columns["StorageEngineDuration"].ExtendedProperties[Constants.FormatString] = "#,##0.00";

            BenchmarkDataSet.Tables.Add(summary);
        }

        private void CreateDetailOutputTable()
        {
            var details = new DataTable("Details");
            details.Columns.Add("Sequence", typeof(int));
            details.Columns.Add("Cache", typeof(string));
            details.Columns.Add("TotalDuration", typeof(int));
            details.Columns.Add("TotalCpuDuration", typeof(int));
            details.Columns.Add("TotalCpuFactor", typeof(double));
            details.Columns.Add("StorageEngineDuration", typeof(int));
            details.Columns.Add("StorageEngineQueryCount", typeof(int));
            details.Columns.Add("StorageEngineCpuDuration", typeof(int));
            details.Columns.Add("StorageEngineCpuFactor", typeof(double));
            details.Columns.Add("FormulaEngineDuration", typeof(int));
            details.Columns.Add("VertipaqCacheMatches", typeof(int));
            
            BenchmarkDataSet.Tables.Add(details);
        }

        #endregion




        #region Event Handlers
        int _sequence;
        public void Handle(ServerTimingsEvent message)
        {
            _sequence++;
            // TODO - catch servertimings from query 
            AddTimingsToDetailsTable(_sequence, _currentRunStyle, message);

            System.Diagnostics.Debug.WriteLine($"TimingEvent Recieved: {message.TotalDuration}ms");


            if (_currentColdRun < ColdCacheRuns
                || _currentWarmRun < WarmCacheRuns)
            {
                RunNextQuery();
            } 
            else
            {
                BenchmarkingComplete();
            }

        }

        private void AddTimingsToDetailsTable(int sequence, RunStyle runStyle, ServerTimingsEvent message)
        {
            var dt = BenchmarkDataSet.Tables["Details"];
            var dr = dt.NewRow();
            dr["Sequence"] = sequence;
            dr["Cache"] = runStyle.ClearCache ? "Cold" : "Warm";
            dr["TotalDuration"] = message.TotalDuration;
            dr["TotalCpuDuration"] = message.TotalCpuDuration;
            dr["TotalCpuFactor"] = message.TotalCpuFactor;
            dr["StorageEngineDuration"] = message.StorageEngineDuration;
            dr["StorageEngineQueryCount"] = message.StorageEngineQueryCount;
            dr["StorageEngineCpuDuration"] = message.StorageEngineCpu;
            dr["StorageEngineCpuFactor"] = message.StorageEngineCpuFactor;
            dr["FormulaEngineDuration"] = message.FormulaEngineDuration;
            dr["VertipaqCacheMatches"] = message.VertipaqCacheMatches;
            dt.Rows.Add(dr);
        }

        public void Handle(TraceChangedEvent message)
        {
            if (message.TraceStatus == QueryTrace.Interfaces.QueryTraceStatus.Started) RunNextQuery();
            // TODO - need to handle trace errors
        }

        #endregion

        private bool _benchmarkingComplete;
        private int _currentColdRun;
        private int _currentWarmRun;

        #region Properties
        private int _coldCacheRuns = 5;
        public int ColdCacheRuns { 
            get { return _coldCacheRuns; } 
            set { _coldCacheRuns = value;
                NotifyOfPropertyChange(() => ColdCacheRuns);
                if (RunSameWarmAndCold)
                {
                    WarmCacheRuns = ColdCacheRuns;
                    NotifyOfPropertyChange(() => WarmCacheRuns);
                }
            } 
        }
        public int WarmCacheRuns { get; set; } = 5;

        private double _progressPercentage;
        public double ProgressPercentage { get => _progressPercentage;
            set {
                _progressPercentage = value;
                NotifyOfPropertyChange(nameof(ProgressPercentage));
            }
        }

        private string _progressColor = "lightgray";
        public string ProgressColor { get => _progressColor;
            set {
                _progressColor = value;
                NotifyOfPropertyChange(nameof(ProgressColor));
            }
        }

        public bool RunSameWarmAndCold { get; set; }

        private string _progressMessage;
        public string ProgressMessage { get => _progressMessage;
            set {
                _progressMessage = value;
                NotifyOfPropertyChange(nameof(ProgressMessage));
            }
        }

        private FontAwesomeIcon _progressIcon;

        public IEventAggregator EventAggregator { get; }
        public DocumentViewModel Document { get; }
        public RibbonViewModel Ribbon { get; }

        private readonly RunStyle ColdRunStyle;
        private readonly RunStyle WarmRunStyle;
        private readonly IResultsTarget TimerRunTarget;

        public FontAwesomeIcon ProgressIcon { get => _progressIcon;
            set {
                _progressIcon = value;
                NotifyOfPropertyChange(nameof(ProgressIcon));
            } 
        }
        private bool _progressSpin;
        private RunStyle _currentRunStyle;

        public bool ProgressSpin { get => _progressSpin;
            set {
                _progressSpin = value;
                NotifyOfPropertyChange(() => ProgressSpin);
            } 
        }

        public DataSet BenchmarkDataSet { get; } = new DataSet("BenchmarkResults");
        public bool IsCancelled { get; internal set; }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                EventAggregator.Unsubscribe(this);
            }
        }

        #endregion
        
    }
}
