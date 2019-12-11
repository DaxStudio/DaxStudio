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
        }


        #region Public Methods
        public void Run()
        {
            ProgressIcon = FontAwesomeIcon.Refresh;
            ProgressSpin = true;
            ProgressMessage = "Starting Server Timings trace...";

            SetSelectedOutputTarget(OutputTarget.Timer);

            CreateOutputTable();
            
            // start server timings
            // once the server timings starts it will trigger the first query
            StartServerTimings();

        }

        public void Cancel()
        {
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
            if (_currentWarmRun < WarmCacheRuns)
            {
                ProgressMessage = $"Running Warm Cache Query {_currentWarmRun} of {WarmCacheRuns}";
                return;
            }

            BenchmarkingComplete();
            

        }

        private void BenchmarkingComplete()
        {
            ProgressMessage = "Compiling Results";

            // todo - should we add an option to output directly to a file
            SetSelectedOutputTarget(OutputTarget.Grid);

            Document.ResultsDataSet = BenchmarkDataSet;

            ProgressSpin = false;
            ProgressIcon = FontAwesomeIcon.CheckCircle;

            ProgressMessage = "Benchmark Complete";


        }

        private void CreateOutputTable()
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
        public int ColdCacheRuns { get; set; } = 5;
        public int WarmCacheRuns { get; set; } = 5;

        private double _progressPercentage;
        public double ProgressPercentage { get => _progressPercentage;
            set {
                _progressPercentage = value;
                NotifyOfPropertyChange(nameof(ProgressPercentage));
            }
        }

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
        private bool _progressSpin = false;
        private RunStyle _currentRunStyle;

        public bool ProgressSpin { get => _progressSpin;
            set {
                _progressSpin = value;
                NotifyOfPropertyChange(() => ProgressSpin);
            } 
        }

        public DataSet BenchmarkDataSet { get; } = new DataSet("BenchmarkResults");
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
