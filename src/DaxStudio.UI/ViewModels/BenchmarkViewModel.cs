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
using System.Diagnostics;
using DaxStudio.UI.Extensions;
using DaxStudio.Common;
using Serilog;

namespace DaxStudio.UI.ViewModels
{
    public class BenchmarkViewModel : Screen, IDisposable
        ,IHandle<ServerTimingsEvent>
        ,IHandle<TraceChangedEvent>
    {

        private Stopwatch _stopwatch;
        private string _viewAsStatus = "On";
        private int _totalRuns = 0;
        private int _viewAsRuns = 0;

        [ImportingConstructor]
        public BenchmarkViewModel(IEventAggregator eventAggregator, DocumentViewModel document, RibbonViewModel ribbon, IGlobalOptions options)
        {
            EventAggregator = eventAggregator;
            EventAggregator.Subscribe(this);
            Document = document;
            Ribbon = ribbon;
            Options = options;
            ColdRunStyle = Ribbon.RunStyles.FirstOrDefault(rs => rs.Icon == RunStyleIcons.ClearThenRun);
            WarmRunStyle = Ribbon.RunStyles.FirstOrDefault(rs => rs.Icon == RunStyleIcons.RunOnly);
            TimerRunTarget = Ribbon.ResultsTargets.FirstOrDefault(t => t.GetType() == typeof(ResultsTargetTimer));
            ProgressIcon = FontAwesomeIcon.ClockOutline;
            ProgressSpin = false;
            ProgressMessage = "Ready";
            ProgressPercentage = 0;
            ProgressColor = "LightGray";
            RunSameWarmAndCold = true;
            IsViewAsActive = document.IsViewAsActive;
            RepeatRunWithoutViewAs = document.IsViewAsActive;

            Log.Information(Constants.LogMessageTemplate, nameof(BenchmarkViewModel), "ctor", $"Benchmark Dialog Opened - IsViewAsActive={IsViewAsActive}");
        }


        #region Public Methods
        public void Run()
        {
            try
            {
                Log.Information(Constants.LogMessageTemplate, nameof(BenchmarkViewModel), nameof(Run), $"Running Benchmark - Cold:{ColdCacheRuns} Warm: {WarmCacheRuns} RepeatWithoutViewAs: {RepeatRunWithoutViewAs}");
                _stopwatch = new Stopwatch();
                _stopwatch.Start();
                _totalRuns = ColdCacheRuns + WarmCacheRuns;
                if (RepeatRunWithoutViewAs) _totalRuns = _totalRuns * 2;
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
                _stopwatch?.Stop();
            }
        }

        public void Cancel()
        {
            _stopwatch?.Stop();
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
            var serverTimings = Ribbon.TraceWatchers.First(tw => tw.GetType() == typeof(ServerTimesViewModel));
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

            if (!_benchmarkingPassComplete)
            {
                EventAggregator.PublishOnUIThread(new RunQueryEvent(TimerRunTarget, _currentRunStyle));
            }

            // if we have completed the runs with ViewAs On
            if (_benchmarkingPassComplete && _viewAsStatus == "On" && RepeatRunWithoutViewAs)
            {
                _viewAsStatus = "Off";
                _benchmarkingPassComplete = false;
                _currentColdRun = 0;
                _currentWarmRun = 0;
                _viewAsRuns = ColdCacheRuns + WarmCacheRuns;
                ProgressMessage = "Stopping View As and restarting Trace";
                Document.StopViewAs();
            }

            
            

            // if we have completed all the cold and warm runs
            // with the ViewAs pass if required then set completed to true
            if (_currentColdRun == ColdCacheRuns 
                && _currentWarmRun == WarmCacheRuns)
            {
                _benchmarkingPassComplete = true;
            }
            
        }

        private void RefreshProgress()
        {
            ProgressPercentage = (double)(_viewAsRuns + _currentColdRun + _currentWarmRun) / _totalRuns;
            var viewAsState = string.Empty;
            if (RepeatRunWithoutViewAs) viewAsState = $"(with ViewAs {_viewAsStatus}) ";
            if (_currentColdRun <= ColdCacheRuns && _currentWarmRun == 0)
            {
                ProgressMessage = $"Running Cold Cache Query {viewAsState}{_currentColdRun} of {ColdCacheRuns}";
                return;
            }
            if (_currentWarmRun <= WarmCacheRuns && !_benchmarkingPassComplete)
            {
                ProgressMessage = $"Running Warm Cache Query {viewAsState}{_currentWarmRun} of {WarmCacheRuns}";
                return;
            }

        }

        private void BenchmarkingComplete()
        {
            _stopwatch?.Stop();
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
            var duration = _stopwatch.ElapsedMilliseconds;
            Options.PlayLongOperationSound((int)(duration / 1000));
            
            Document.OutputMessage("Benchmark Complete", duration);
            // todo - activate results

            // close the Benchmarking dialog
            this.TryClose(true);
        }

        private void CalculateBenchmarkSummary()
        {
            var dt = BenchmarkDataSet.Tables["Details"];
            string[] statistics = { "Average", "StdDev", "Min", "Max" };
            var newDt2 = from d in dt.AsEnumerable()
                         from stat in statistics
                         select new { Cache = d["Cache"], 
                                      Statistic = stat, 
                                      ViewAs = RepeatRunWithoutViewAs? d["RLS"].ToString():"Off",
                                      TotalDuration = (int)d["TotalDuration"], 
                                      StorageEngineDuration = (int)d["StorageEngineDuration"] };

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
                        newRow["TotalDuration"] = grp.Where(x => x.ViewAs == "Off").Average(x => x.TotalDuration);
                        newRow["SE Duration"] = grp.Where(x => x.ViewAs == "Off").Average(x => x.StorageEngineDuration);
                        if (RepeatRunWithoutViewAs)
                        {
                            newRow["TotalDuration (RLS)"] = grp.Where(x => x.ViewAs == "On").Average(x => x.TotalDuration);
                            newRow["SE Duration (RLS)"] = grp.Where(x => x.ViewAs == "On").Average(x => x.StorageEngineDuration);
                        }
                        break;
                    case "StdDev":
                        newRow["TotalDuration"] = grp.Where(x => x.ViewAs == "Off").StdDev(x => x.TotalDuration);
                        newRow["SE Duration"] = grp.Where(x => x.ViewAs == "Off").StdDev(x => x.StorageEngineDuration);
                        if (RepeatRunWithoutViewAs)
                        {
                            newRow["TotalDuration (RLS)"] = grp.Where(x => x.ViewAs == "On").StdDev(x => x.TotalDuration);
                            newRow["SE Duration (RLS)"] = grp.Where(x => x.ViewAs == "On").StdDev(x => x.StorageEngineDuration);
                        }
                        break;
                    case "Min":
                        newRow["TotalDuration"] = grp.Where(x => x.ViewAs == "Off").Min(x => x.TotalDuration);
                        newRow["SE Duration"] = grp.Where(x => x.ViewAs == "Off").Min(x => x.StorageEngineDuration);
                        if (RepeatRunWithoutViewAs)
                        {
                            newRow["TotalDuration (RLS)"] = grp.Where(x => x.ViewAs == "On").Min(x => x.TotalDuration);
                            newRow["SE Duration (RLS)"] = grp.Where(x => x.ViewAs == "On").Min(x => x.StorageEngineDuration);
                        }
                        break;
                    case "Max":
                        newRow["TotalDuration"] = grp.Where(x => x.ViewAs == "Off").Max(x => x.TotalDuration);
                        newRow["SE Duration"] = grp.Where(x => x.ViewAs == "Off").Max(x => x.StorageEngineDuration);
                        if (RepeatRunWithoutViewAs)
                        {
                            newRow["TotalDuration (RLS)"] = grp.Where(x => x.ViewAs == "On").Max(x => x.TotalDuration);
                            newRow["SE Duration (RLS)"] = grp.Where(x => x.ViewAs == "On").Max(x => x.StorageEngineDuration);
                        }
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

            if (RepeatRunWithoutViewAs)
            {
                summary.Columns.Add("TotalDuration (RLS)", typeof(double));
                summary.Columns["TotalDuration (RLS)"].ExtendedProperties[Constants.FormatString] = "#,##0.00";
                summary.Columns.Add("SE Duration (RLS)", typeof(double));
                summary.Columns["SE Duration (RLS)"].ExtendedProperties[Constants.FormatString] = "#,##0.00";
            }

            summary.Columns.Add("TotalDuration", typeof(double));
            summary.Columns["TotalDuration"].ExtendedProperties[Constants.FormatString] = "#,##0.00";
            summary.Columns.Add("SE Duration", typeof(double));
            summary.Columns["SE Duration"].ExtendedProperties[Constants.FormatString] = "#,##0.00";
            
            BenchmarkDataSet.Tables.Add(summary);
        }

        private void CreateDetailOutputTable()
        {
            var details = new DataTable("Details");
            details.Columns.Add("Sequence", typeof(int));
            details.Columns.Add("Cache", typeof(string));
            if (RepeatRunWithoutViewAs) details.Columns.Add("RLS", typeof(string));
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


            if (_viewAsRuns + _currentColdRun + _currentWarmRun < _totalRuns)
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
            if (RepeatRunWithoutViewAs) dr["RLS"] = _viewAsStatus;
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

        private bool _benchmarkingPassComplete;
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
        public bool IsViewAsActive { get; }

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
        public IGlobalOptions Options { get; }

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


        private bool _repeatRunWithoutViewAs = false;
        public bool RepeatRunWithoutViewAs { get => _repeatRunWithoutViewAs; 
            set
            {
                _repeatRunWithoutViewAs = value;
                NotifyOfPropertyChange();
            }
        }
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
