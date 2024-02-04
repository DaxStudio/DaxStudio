using Caliburn.Micro;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using DaxStudio.UI.Interfaces;
using System.Data;
using System.Diagnostics;
using DaxStudio.UI.Extensions;
using DaxStudio.Common;
using Serilog;
using DaxStudio.Interfaces;
using System.Threading.Tasks;
using System.Threading;
using DaxStudio.UI.ResultsTargets;

namespace DaxStudio.UI.ViewModels
{
    public class BenchmarkViewModel : Screen, IDisposable, ICancellable
        , IHandle<ServerTimingsEvent>
        ,IHandle<TraceChangedEvent>
        ,IHandle<UpdateGlobalOptions>
    {

        private Stopwatch _stopwatch;
        private string _viewAsStatus = "On";
        private int _totalRuns = 0;
        private int _viewAsRuns = 0;

        [ImportingConstructor]
        public BenchmarkViewModel(IEventAggregator eventAggregator, DocumentViewModel document, RibbonViewModel ribbon, IGlobalOptions options)
        {
            EventAggregator = eventAggregator;
            EventAggregator.SubscribeOnPublishedThread(this);
            Document = document;
            Ribbon = ribbon;
            Options = options;
            SetDefaultsFromOptions();

            _currentRunStyle = Ribbon.RunStyles.FirstOrDefault(rs => rs.Icon == RunStyleIcons.RunOnly);
            TimerRunTarget = Ribbon.ResultsTargets.FirstOrDefault(t => t.GetType() == typeof(ResultsTargetTimer));
            ProgressSpin = false;
            ProgressMessage = "Ready";
            ProgressPercentage = 0;
            IsViewAsActive = document.IsViewAsActive;
            RepeatRunWithoutViewAs = document.IsViewAsActive;

            Log.Information(Constants.LogMessageTemplate, nameof(BenchmarkViewModel), "ctor", $"Benchmark Dialog Opened - IsViewAsActive={IsViewAsActive}");
        }

        private void SetDefaultsFromOptions()
        {
            EnableColdCacheExecutions = Options.BenchmarkColdCacheSwitchedOn;
            EnableWarmCacheExecutions = Options.BenchmarkWarmCacheSwitchedOn;
            ColdCacheRuns = Options.BenchmarkColdCacheRuns;
            WarmCacheRuns = Options.BenchmarkWarmCacheRuns;
        }


        #region Public Methods
        public void Run()
        {
            try
            {
                Log.Information(Constants.LogMessageTemplate, nameof(BenchmarkViewModel), nameof(Run), $"Running Benchmark - Cold:{CalculatedColdCacheRuns} Warm: {CalculatedWarmCacheRuns} RepeatWithoutViewAs: {RepeatRunWithoutViewAs}");
                _stopwatch = new Stopwatch();
                _stopwatch.Start();
                _totalRuns = CalculatedColdCacheRuns + CalculatedWarmCacheRuns;
                if (RepeatRunWithoutViewAs) _totalRuns = _totalRuns * 2;
                ProgressSpin = true;
                ProgressMessage = "Starting Server Timings trace...";
                _currentResultsTarget = this.Ribbon.SelectedTarget.Icon;
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
                EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"An error occurred while attempting to run the benchmark: {ex.Message}"));
                _stopwatch?.Stop();
            }
        }

        public void Cancel()
        {
            _stopwatch?.Stop();
            IsCancelled = true;
            //await TryCloseAsync(true);
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
            if (_currentColdRun < CalculatedColdCacheRuns)
            {
                // perform a cold cache run
                _currentColdRun++;
                _currentRunStyle.ClearCache = true;
            }
            else
            {
                // perform a warm cache run
                _currentWarmRun++;
                _currentRunStyle.ClearCache = false;
            }

            RefreshProgress();

            if (!_benchmarkingPassComplete)
            {
                EventAggregator.PublishOnUIThreadAsync(new RunQueryEvent(TimerRunTarget, _currentRunStyle));
            }

            // if we have completed the runs with ViewAs On
            if (_benchmarkingPassComplete && _viewAsStatus == "On" && RepeatRunWithoutViewAs)
            {
                _viewAsStatus = "Off";
                _benchmarkingPassComplete = false;
                _currentColdRun = 0;
                _currentWarmRun = 0;
                _viewAsRuns = CalculatedColdCacheRuns + CalculatedWarmCacheRuns;
                ProgressMessage = "Stopping View As and restarting Trace";
                Document.StopViewAs();
            }

            
            

            // if we have completed all the cold and warm runs
            // with the ViewAs pass if required then set completed to true
            if (_currentColdRun == CalculatedColdCacheRuns 
                && _currentWarmRun == CalculatedWarmCacheRuns) 
            {
                _benchmarkingPassComplete = true;
                SetSelectedOutputTarget(_currentResultsTarget);
            }
            
        }

        private void RefreshProgress()
        {
            ProgressPercentage = ((double)((_viewAsRuns + _currentColdRun + _currentWarmRun)) / _totalRuns) * 100;
            var viewAsState = string.Empty;
            if (RepeatRunWithoutViewAs) viewAsState = $"(with ViewAs {_viewAsStatus}) ";
            if (_currentColdRun <= CalculatedColdCacheRuns && _currentWarmRun == 0)
            {
                ProgressMessage = $"Running Cold Cache Query {viewAsState}{_currentColdRun} of {CalculatedColdCacheRuns}";
                return;
            }
            if (_currentWarmRun <= CalculatedWarmCacheRuns && !_benchmarkingPassComplete)
            {
                ProgressMessage = $"Running Warm Cache Query {viewAsState}{_currentWarmRun} of {CalculatedWarmCacheRuns}";
                return;
            }

        }

        private async void BenchmarkingComplete()
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
            ProgressMessage = "Benchmark Complete";
            var duration = _stopwatch.ElapsedMilliseconds;
            Options.PlayLongOperationSound((int)(duration / 1000));
            
            Document.OutputMessage("Benchmark Complete", duration);
            // todo - activate results

            // close the Benchmarking dialog
            await this.TryCloseAsync(true);
        }

        private void CalculateBenchmarkSummary()
        {
            var dt = BenchmarkDataSet.Tables["Details"];
            if (dt == null) {
                var msg = "Unable to calculate the benchmark summary as the details table is empty";
                Log.Error(Common.Constants.LogMessageTemplate, nameof(BenchmarkViewModel), nameof(CalculateBenchmarkSummary), msg);
                EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, msg));
                return; 
            }

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
            details.Columns.Add("TotalDirectQueryDuration", typeof(int));
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
        public async Task HandleAsync(ServerTimingsEvent message, CancellationToken cancellationToken)
        {
            _sequence++;
            // catch servertimings from query 
            try
            {
                AddTimingsToDetailsTable(_sequence, _currentRunStyle, message);
            }
            catch (Exception ex)
            {
                Log.Error(ex, nameof(BenchmarkViewModel), "HandleAsync<ServerTimingsEvent>", "Error Adding timings to details table");
                await EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error Adding timings to details table\n{ex.Message}"));
            }

            Debug.WriteLine($"TimingEvent Received: {message.TotalDuration}ms");


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
            dr["TotalDirectQueryDuration"] = message.TotalDirectQueryDuration;
            dr["TotalCpuFactor"] = message.TotalCpuFactor;
            dr["StorageEngineDuration"] = message.StorageEngineDuration;
            dr["StorageEngineQueryCount"] = message.StorageEngineQueryCount;
            dr["StorageEngineCpuDuration"] = message.StorageEngineCpu;
            dr["StorageEngineCpuFactor"] = message.StorageEngineCpuFactor;
            dr["FormulaEngineDuration"] = message.FormulaEngineDuration;
            dr["VertipaqCacheMatches"] = message.VertipaqCacheMatches;
            dt.Rows.Add(dr);
        }

        public Task HandleAsync(TraceChangedEvent message, CancellationToken cancellationToken)
        {
            if (message.TraceStatus == QueryTrace.Interfaces.QueryTraceStatus.Started 
                && message.Sender is ServerTimesViewModel) RunNextQuery();
            // TODO - need to handle trace errors
            return Task.CompletedTask;
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
                NotifyOfPropertyChange();

            } 
        }

        public int CalculatedColdCacheRuns => EnableColdCacheExecutions ? ColdCacheRuns : 0;

        private int _warmCacheExecutions = 5;
        public int WarmCacheRuns { get => _warmCacheExecutions;
            set {
                _warmCacheExecutions = value;
                NotifyOfPropertyChange();
            } 
        }

        public int CalculatedWarmCacheRuns => EnableWarmCacheExecutions ? WarmCacheRuns : 0;

        private double _progressPercentage;
        public double ProgressPercentage { get => _progressPercentage;
            set {
                _progressPercentage = value;
                NotifyOfPropertyChange();
            }
        }


        private OutputTarget _currentResultsTarget;

        public bool IsViewAsActive { get; }

        private string _progressMessage;
        public string ProgressMessage { get => _progressMessage;
            set {
                _progressMessage = value;
                NotifyOfPropertyChange();
            }
        }

        public IEventAggregator EventAggregator { get; }
        public DocumentViewModel Document { get; }
        public RibbonViewModel Ribbon { get; }
        public IGlobalOptions Options { get; }

        private readonly IResultsTarget TimerRunTarget;

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

        private bool _enableColdCacheExecutions = true;
        public bool EnableColdCacheExecutions { get => _enableColdCacheExecutions;
            set { 
                _enableColdCacheExecutions = value;
                NotifyOfPropertyChange();
            } 
        }

        private bool _enableWarmCacheExecutions = true;
        public bool EnableWarmCacheExecutions { get => _enableWarmCacheExecutions;
            set { 
                _enableWarmCacheExecutions = value;
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

        public Task HandleAsync(UpdateGlobalOptions message, CancellationToken cancellationToken)
        {
            SetDefaultsFromOptions();
            return Task.CompletedTask;
        }

        #endregion

    }
}
