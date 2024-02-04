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
using System.Collections.Generic;
using Microsoft.AnalysisServices.AdomdClient;
using DaxStudio.UI.ResultsTargets;

namespace DaxStudio.UI.ViewModels
{
    public class BenchmarkServerFEViewModel : Screen, IDisposable, ICancellable
        , IHandle<ServerTimingsEvent>
        , IHandle<TraceChangedEvent>
        , IHandle<UpdateGlobalOptions>
    {
        public class BenchmarkFEQuery : IQueryTextProvider
        {
            const string _benchmarkQuery = @"EVALUATE
ROW (
    ""x"",
        COUNTROWS (
            FILTER (
                CROSSJOIN (
                    SELECTCOLUMNS ( CALENDAR ( 1, 3800 ), ""Num1"", INT ( [Date] ) ),
                    SELECTCOLUMNS ( CALENDAR ( 1, 3800 ), ""Num2"", INT ( [Date] ) )
                ),
                ( [Num1] + [Num2] + LEN ( """" & [Num1] ) )
                    * ( 1.0 * [Num1] - CURRENCY ( [Num2] ) )
                > 0
            )
        )
)";

            public string EditorText => _benchmarkQuery;
            public string QueryText => _benchmarkQuery;
            public List<AdomdParameter> ParameterCollection => new List<AdomdParameter>();
            public QueryInfo QueryInfo { get; set; }
        }


        private Stopwatch _stopwatch;
        private int _totalRuns = 0;
        private int _viewAsRuns = 0;

        [ImportingConstructor]
        public BenchmarkServerFEViewModel(IEventAggregator eventAggregator, DocumentViewModel document, RibbonViewModel ribbon, IGlobalOptions options)
        {
            EventAggregator = eventAggregator;
            EventAggregator.SubscribeOnPublishedThread(this);
            Document = document;
            Ribbon = ribbon;
            Options = options;
            SetDefaultsFromOptions();

            TimerRunTarget = Ribbon.ResultsTargets.FirstOrDefault(t => t.GetType() == typeof(ResultsTargetTimer));
            ProgressSpin = false;
            ProgressMessage = "Ready";
            ProgressPercentage = 0;
            IsViewAsActive = document.IsViewAsActive;

            Log.Information(Constants.LogMessageTemplate, nameof(BenchmarkServerFEViewModel), "ctor", $"BenchmarkServerFE Dialog Opened - IsViewAsActive={IsViewAsActive}");
        }

        private void SetDefaultsFromOptions()
        {
            // Set Runs in case we provide the parameter in the dialog box - even though it's probably not necessary
            // Runs = Options.BenchmarkRuns;
        }

        #region Public Methods
        public void Run()
        {
            try
            {
                Log.Information(Constants.LogMessageTemplate, nameof(BenchmarkServerFEViewModel), nameof(Run), $"Running Benchmark - Runs:{Runs}");
                _stopwatch = new Stopwatch();
                _stopwatch.Start();
                _totalRuns = Runs;
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
            catch (Exception ex)
            {
                Log.Error(ex, DaxStudio.Common.Constants.LogMessageTemplate, nameof(BenchmarkServerFEViewModel), nameof(Run), ex.Message);
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
            var target = Ribbon.ResultsTargets.FirstOrDefault(t => t.Icon == targetType);
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
            _currentRun++;
            
            RefreshProgress();

            if (!_benchmarkingPassComplete)
            {
                var queryEvent = new RunQueryEvent(TimerRunTarget);
                queryEvent.QueryProvider = new BenchmarkFEQuery();
                EventAggregator.PublishOnUIThreadAsync(queryEvent);
            }

            if (_currentRun >= CalculatedRuns)
            {
                _benchmarkingPassComplete = true;
                SetSelectedOutputTarget(_currentResultsTarget);
            }
        }

        private void RefreshProgress()
        {
            ProgressPercentage = ((double)((_viewAsRuns + _currentRun)) / _totalRuns) * 100;
            var viewAsState = string.Empty;
            ProgressMessage = $"Running Query {_currentRun} of {CalculatedRuns}";
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
            string[] statistics = { "FE Benchmark", "Average", "StdDev", "Min", "Max" };
            var newDt2 = from d in dt.AsEnumerable()
                         from stat in statistics
                         select new
                         {
                             Statistic = stat,
                             FormulaEngineDuration = (int)d["FormulaEngineDuration"],
                         };

            var newGrp = newDt2.GroupBy(x => new { Statistic = x.Statistic });

            DataTable summary = BenchmarkDataSet.Tables["Summary"];

            foreach (var grp in newGrp)
            {
                DataRow newRow = summary.Rows.Add();
                newRow["Statistic"] = grp.Key.Statistic;

                switch (grp.Key.Statistic)
                {
                    case "FE Benchmark":
                        // Benchmark is 100 * (10000 / time elapsed) --> simplified to 1000000 / time_elapsed
                        newRow["Formula Engine"] = 1000000.0 / grp.Min(x => x.FormulaEngineDuration);
                        break;
                    case "Average":
                        newRow["Formula Engine"] = grp.Average(x => x.FormulaEngineDuration);
                        break;
                    case "StdDev":
                        newRow["Formula Engine"] = grp.StdDev(x => x.FormulaEngineDuration);
                        break;
                    case "Min":
                        newRow["Formula Engine"] = grp.Min(x => x.FormulaEngineDuration);
                        break;
                    case "Max":
                        newRow["Formula Engine"] = grp.Max(x => x.FormulaEngineDuration);
                        break;
                    default:

                        break;
                }
            }
        }

        private void CreateSummaryOutputTable()
        {
            DataTable summary = new DataTable("Summary");
            summary.Columns.Add("Statistic", typeof(string));
            summary.Columns.Add("Formula Engine", typeof(double));
            summary.Columns["Formula Engine"].ExtendedProperties[Constants.FormatString] = "#,##0.00";

            BenchmarkDataSet.Tables.Add(summary);
        }

        private void CreateDetailOutputTable()
        {
            var details = new DataTable("Details");
            details.Columns.Add("Sequence", typeof(int));
            details.Columns.Add("TotalDuration", typeof(int));
            details.Columns.Add("TotalCpuDuration", typeof(int));
            details.Columns.Add("TotalDirectQueryDuration", typeof(int));
            //details.Columns.Add("TotalCpuFactor", typeof(double));
            //details.Columns.Add("StorageEngineDuration", typeof(int));
            //details.Columns.Add("StorageEngineQueryCount", typeof(int));
            //details.Columns.Add("StorageEngineCpuDuration", typeof(int));
            //details.Columns.Add("StorageEngineCpuFactor", typeof(double));
            details.Columns.Add("FormulaEngineDuration", typeof(int));
            //details.Columns.Add("VertipaqCacheMatches", typeof(int));

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
                AddTimingsToDetailsTable(_sequence, message);
            }
            catch (Exception ex)
            {
                Log.Error(ex, nameof(BenchmarkServerFEViewModel), "HandleAsync<ServerTimingsEvent>", "Error Adding timings to details table");
                await EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error Adding timings to details table\n{ex.Message}"));
            }

            Debug.WriteLine($"TimingEvent Received: {message.TotalDuration}ms");


            if (_currentRun < _totalRuns)
            {
                RunNextQuery();
            }
            else
            {
                BenchmarkingComplete();
            }

        }

        private void AddTimingsToDetailsTable(int sequence, ServerTimingsEvent message)
        {
            var dt = BenchmarkDataSet.Tables["Details"];
            var dr = dt.NewRow();
            dr["Sequence"] = sequence;
            dr["TotalDuration"] = message.TotalDuration;
            dr["TotalCpuDuration"] = message.TotalCpuDuration;
            dr["TotalDirectQueryDuration"] = message.TotalDirectQueryDuration;
            //dr["TotalCpuFactor"] = message.TotalCpuFactor;
            //dr["StorageEngineDuration"] = message.StorageEngineDuration;
            //dr["StorageEngineQueryCount"] = message.StorageEngineQueryCount;
            //dr["StorageEngineCpuDuration"] = message.StorageEngineCpu;
            //dr["StorageEngineCpuFactor"] = message.StorageEngineCpuFactor;
            dr["FormulaEngineDuration"] = message.FormulaEngineDuration;
            //dr["VertipaqCacheMatches"] = message.VertipaqCacheMatches;
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
        private int _currentRun;

        #region Properties
       
        public int CalculatedRuns => Runs;

        private int _executions = 3;
        public int Runs
        {
            get => _executions;
            set
            {
                _executions = value;
                NotifyOfPropertyChange();
            }
        }

        private double _progressPercentage;
        public double ProgressPercentage
        {
            get => _progressPercentage;
            set
            {
                _progressPercentage = value;
                NotifyOfPropertyChange();
            }
        }

        private OutputTarget _currentResultsTarget;

        public bool IsViewAsActive { get; }

        private string _progressMessage;
        public string ProgressMessage
        {
            get => _progressMessage;
            set
            {
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

        public bool ProgressSpin
        {
            get => _progressSpin;
            set
            {
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

        public Task HandleAsync(UpdateGlobalOptions message, CancellationToken cancellationToken)
        {
            SetDefaultsFromOptions();
            return Task.CompletedTask;
        }

        #endregion

    }
}
