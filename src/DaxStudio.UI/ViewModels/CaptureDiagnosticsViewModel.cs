using AsyncAwaitBestPractices;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using ICSharpCode.NRefactory.Ast;
using Microsoft.Win32;
using Mono.Cecil.Cil;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DaxStudio.UI.ResultsTargets;
using System.Windows;


namespace DaxStudio.UI.ViewModels
{
    public struct TimingRecord
    {
        public string QueryName { get; set; }
        public long TotalDurationMs { get; set; }
        public long FEDurationMs { get; set; }
        public long SEDurationMs { get; set; }  
        public long SEQueries { get; set; }
        public long CPUDuration { get; set; }
    }

    public class CaptureDiagnosticsViewModel:Screen,
        IHandle<ViewMetricsCompleteEvent>,
        IHandle<TraceChangedEvent>,
        IHandle<QueryTraceCompletedEvent>,
        IHandle<QueryFinishedEvent>,
        IHandle<NoQueryTextEvent>,
        IHandle<DocumentActivatedEvent>
    {

        public enum OperationStatus
        {
            Waiting,
            Succeeded,
            Failed,
            Skipped
        }

        public CaptureDiagnosticsViewModel(RibbonViewModel ribbon, IGlobalOptions options, IEventAggregator eventAggregator)
        {
            Ribbon = ribbon;
            Options = options;
            EventAggregator = eventAggregator;

            if (Ribbon.ActiveDocument.EditorText.Any())
                SelectedQuerySource = new CaptureDiagnosticsSource("Current Document", new List<IQueryTextProvider>() { this.Ribbon.ActiveDocument });
            else
                SelectedQuerySource = new CaptureDiagnosticsSource("Clipboard", new List<IQueryTextProvider>() { new ClipboardTextProvider() });

            // save the current results target
            _selectedResultsTarget = Ribbon.SelectedTarget;

            // Check if we have any query text or if the query builder is open and populated
            if(SelectedQuerySource.Queries.FirstOrDefault().EditorText.Any()
            || (SelectedQuerySource.Queries.FirstOrDefault().GetType() == typeof(DocumentViewModel) 
                && Ribbon.ActiveDocument.QueryBuilder.IsVisible 
                && Ribbon.ActiveDocument.QueryBuilder.Columns.Count > 0))
            {
                // start capturing
              RunAsync().SafeFireAndForget(onException: ex =>
              {
                  Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(CaptureDiagnosticsViewModel), "ctor", "error running diagnostic capture");
                  EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error starting diagnostic capture: {ex.Message}"));
              });
            }
            else
            {
                Log.Warning(Common.Constants.LogMessageTemplate, nameof(CaptureDiagnosticsViewModel), "CTOR", "No QueryText found");
                EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, "No query text found to execute"));
                MessageBox.Show( "No query text was found to execute","DAX Studio - Capture Diagnostics", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                FailAllOperations();
                CanClose = true;
                Execute.OnUIThread(() => {
                    Close();
                });
                
            }

        }

        public CaptureDiagnosticsViewModel(RibbonViewModel ribbon, IGlobalOptions options, IEventAggregator eventAggregator, IEnumerable<IQueryTextProvider> querySource )
        {
            Ribbon = ribbon;
            Options = options;
            EventAggregator = eventAggregator;

            SelectedQuerySource = new CaptureDiagnosticsSource("Performance Data", querySource);

            // save the current results target
            _selectedResultsTarget = Ribbon.SelectedTarget;

            if (SelectedQuerySource.Queries.Any())
            {
                // start capturing
                RunAsync().SafeFireAndForget(onException: ex =>
                {
                    Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(CaptureDiagnosticsViewModel), "ctor", "error running diagnostic capture");
                    EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error starting diagnostic capture: {ex.Message}"));
                });

            }
            else
            {
                Log.Warning(Common.Constants.LogMessageTemplate, nameof(CaptureDiagnosticsViewModel), "CTOR", "No Queries found to execute");
                EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, "No queries found to execute"));
                FailAllOperations();
                CanClose = true;
                Close();
            }
        }

        private void FailAllOperations()
        {
            MetricsStatus = OperationStatus.Failed;
            QueryPlanStatus = OperationStatus.Failed;
            ServerTimingsStatus = OperationStatus.Failed;
            QueryStatus = OperationStatus.Failed;
            SaveAsStatus = OperationStatus.Failed;
        }

        private const string TickImage = "successDrawingImage";
        private const string CrossImage = "failDrawingImage";
        private object traceEventLock = new object();
        #region Properties
        private bool _serverTimingsChecked;
        private bool _queryPlanChecked;

        private bool _isMetricsRunning;
        private bool _isServerTimingsStarting;
        private bool _isQueryPlanStarting;
        private bool _isQueryRunning;
        private bool _isSaveAsRunning;
        private bool _captureComplete;
        private DocumentViewModel _newDocument;
        private List<TimingRecord> _timingRecords = new List<TimingRecord>();

        public bool IsMetricsRunning { get => _isMetricsRunning;
            set { 
                _isMetricsRunning = value;
                NotifyOfPropertyChange(() => IsMetricsRunning);
            } 
        }
        public bool IsServerTimingsStopped { get; set; }
        public bool IsQueryPlanStopped { get; set; }
        public bool IsServerTimingsStarting { get => _isServerTimingsStarting;
            set { 
                _isServerTimingsStarting = value;
                NotifyOfPropertyChange(nameof(IsServerTimingsStarting));
            } 
        }
        public bool IsQueryPlanStarting { get => _isQueryPlanStarting;
            set { 
                _isQueryPlanStarting = value;
                NotifyOfPropertyChange(nameof(IsQueryPlanStarting));
            } 
        }
        public bool IsQueryRunning { get => _isQueryRunning;
            set { 
                _isQueryRunning = value;
                NotifyOfPropertyChange();
            } 
        }

        private IResultsTarget _selectedResultsTarget;

        public bool IsSaveAsRunning { get => _isSaveAsRunning;
            set { 
                _isSaveAsRunning = value;
                NotifyOfPropertyChange();
            } 
        }

        public string MetricsStatusImage => GetOperationStatusImage(MetricsStatus);
        public string ServerTimingsStatusImage => GetOperationStatusImage(ServerTimingsStatus);
        public string QueryPlanStatusImage => GetOperationStatusImage(QueryPlanStatus);
        public string QueryStatusImage => GetOperationStatusImage( QueryStatus );
        public string SaveAsStatusImage => GetOperationStatusImage(SaveAsStatus);

        private OperationStatus _metricsSucceeded;
        private OperationStatus _serverTimingsSucceeded;
        private OperationStatus _queryPlanSucceeded;
        private OperationStatus _querySucceeded;
        private OperationStatus _saveAsSucceeded;

        private bool _serverTimingsComplete;
        private bool _queryPlanComplete;

        public OperationStatus MetricsStatus { get => _metricsSucceeded;
            set { 
                _metricsSucceeded = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(MetricsStatusImage));
            }
        }
        public OperationStatus ServerTimingsStatus { get => _serverTimingsSucceeded;
            set { 
                _serverTimingsSucceeded = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(() => ServerTimingsStatusImage);
            }
        }
        public OperationStatus QueryPlanStatus
        {
            get => _queryPlanSucceeded; set
            {
                _queryPlanSucceeded = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(QueryPlanStatusImage));
            }
        }
        public OperationStatus QueryStatus
        {
            get => _querySucceeded;
            set
            {
                _querySucceeded = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(QueryStatusImage));
            }
        }
        public OperationStatus SaveAsStatus
        {
            get => _saveAsSucceeded;
            set
            {
                _saveAsSucceeded = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(SaveAsStatusImage));
            }
        }

        private string _progressMessage = string.Empty;
        public string ProgressMessage
        {
            get => _progressMessage;
            set
            {
                _progressMessage = value;
                NotifyOfPropertyChange();
            }
        }
        private RibbonViewModel Ribbon { get; }
        public IGlobalOptions Options { get; }
        private IEventAggregator EventAggregator { get; }

        private ITraceWatcher _serverTimingsTrace;
        private ITraceWatcher _queryPlanTrace;
        private bool hasNewDocument;
        #endregion


        public async Task RunAsync()
        {
            
            if (SelectedQuerySource.Queries.Count() == 1 && SelectedQuerySource.Name == "Active Document")
            {
                // Use the current document
                _newDocument = Ribbon.ActiveDocument;
                await CaptureMetricsAsync();

                // capture TraceWatcher checked status
                _serverTimingsTrace = _newDocument.TraceWatchers.First(tw => tw.GetType() == typeof(ServerTimesViewModel));
                _serverTimingsChecked = _serverTimingsTrace?.IsChecked ?? false;
                _queryPlanTrace = _newDocument.TraceWatchers.First(tw => tw.GetType() == typeof(QueryPlanTraceViewModel));
                _queryPlanChecked = _queryPlanTrace?.IsChecked ?? false;
            }
            else
            {
                // Clone the current window
                hasNewDocument = true;
                Ribbon.NewQueryWithCurrentConnection(copyContent: true);
            }
        }


        private bool _canClose;
        public bool CanClose { get => _canClose; set { 
                _canClose = value;
                NotifyOfPropertyChange();
            } 
        } 
        public async void Close()
        {
            await TryCloseAsync();
        }
        private bool _canCancel = true;
        private bool _includeTOM;

        public bool CanCancel { get => _canCancel;
            set { 
                _canCancel = value;
                NotifyOfPropertyChange();
            } 
        } 
        public void Cancel()
        {
            if (IsQueryRunning)
            {
                Ribbon.CancelQuery();
                IsQueryRunning = false;
                QueryStatus = OperationStatus.Failed;
            }
            ResetState();
        }

        public async Task CaptureMetricsAsync()
        {
            IsMetricsRunning = true;
            // store the current setting and turn on the capturing of TOM
            _includeTOM = Options.VpaxIncludeTom;
            Options.VpaxIncludeTom = true;

            await _newDocument.ViewAnalysisDataAsync();
        }

        private void StartTraces()
        {
            IsServerTimingsStarting = true;
            IsQueryPlanStarting = true;

            EnsureTraceIsStarted(_serverTimingsTrace);
            EnsureTraceIsStarted(_queryPlanTrace);
        }

        private void EnsureTraceIsStarted(ITraceWatcher trace)
        {
            if (trace == null) return;

            if (trace.IsChecked)
                EventAggregator.PublishOnUIThreadAsync(new TraceChangedEvent(trace, QueryTrace.Interfaces.QueryTraceStatus.Started));
            else
                trace.IsChecked = true;
        }

        public Task HandleAsync(ViewMetricsCompleteEvent message, CancellationToken cancellationToken)
        {
            IsMetricsRunning = false;
            MetricsStatus = OperationStatus.Succeeded;
            Options.VpaxIncludeTom = _includeTOM;  //reset the include TOM setting
            StartTraces();
            return Task.CompletedTask;
        }

        public async Task HandleAsync(TraceChangedEvent message, CancellationToken cancellationToken)
        {
            bool _tracesStarted;
            bool _tracesWaiting;

            lock (traceEventLock)
            {
                switch (message.TraceStatus)
                {
                    case QueryTrace.Interfaces.QueryTraceStatus.Started:

                        if (message.Sender is QueryPlanTraceViewModel)
                        {
                            IsQueryPlanStarting = false;
                            QueryPlanStatus = OperationStatus.Succeeded;
                        }
                        if (message.Sender is ServerTimesViewModel)
                        {
                            IsServerTimingsStarting = false;
                            ServerTimingsStatus = OperationStatus.Succeeded;
                        }

                        break;
                    case QueryTrace.Interfaces.QueryTraceStatus.Error:

                        if (message.Sender is QueryPlanTraceViewModel)
                        {
                            IsQueryPlanStarting = false;
                            QueryPlanStatus = OperationStatus.Failed;
                        }
                        if (message.Sender is ServerTimesViewModel)
                        {
                            IsServerTimingsStarting = false;
                            ServerTimingsStatus = OperationStatus.Failed;
                        }
                        break;
                    case QueryTrace.Interfaces.QueryTraceStatus.Stopped:

                        if (message.Sender is QueryPlanTraceViewModel)
                        {
                            IsQueryPlanStopped = true;
                            
                        }
                        if (message.Sender is ServerTimesViewModel)
                        {
                            IsServerTimingsStopped = true;
                            
                        }
                        break;
                    default:
                        // ignore any other status change events
                        return;
                }
                _tracesStarted = QueryPlanStatus == OperationStatus.Succeeded 
                                && ServerTimingsStatus == OperationStatus.Succeeded ;
                _tracesWaiting = QueryPlanStatus == OperationStatus.Waiting
                                || ServerTimingsStatus == OperationStatus.Waiting;

            }
            if (_tracesStarted && !_captureComplete)
            {
                RunQuery();
            }
            if (!_tracesStarted && !_tracesWaiting)
            {
                SkipQuery();
            }
            if (hasNewDocument && _captureComplete && IsServerTimingsStopped && IsQueryPlanStopped)
            {
                _newDocument.IsDirty = false;
                await _newDocument.TryCloseAsync(); 
                hasNewDocument = false;
            }
            
        }

        private void RunQuery()
        {
            IsQueryRunning = true;

            // set the run style to run and clear the cache
            var runStyle = new RunStyle("Run", RunStyleIcons.RunOnly, string.Empty);
            runStyle.ClearCache = true;

            var runQueryEvent = new RunQueryEvent(Ribbon.ResultsTargets.FirstOrDefault(rt => rt is ResultsTargetTimer), runStyle );
            runQueryEvent.QueryProvider = SelectedQuerySource.Queries.ElementAt(CurrentQueryNumber -1);

            // if this document is dedicated to capturing the diagnostics update it with the current query.
            if (hasNewDocument) _newDocument.EditorText = runQueryEvent.QueryProvider.QueryText;

            EventAggregator.PublishOnUIThreadAsync(runQueryEvent);

        }

        public void SkipQuery()
        {
            QueryStatus = OperationStatus.Skipped;
            SaveAsStatus = OperationStatus.Skipped;
            OverallStatus = "Failed to capture full diagnostics check the log window for errors";
            CanClose = true;
        }
        public async Task HandleAsync(QueryTraceCompletedEvent message, CancellationToken cancellationToken)
        {
            var trace = message.Trace as IHaveData;
            if (trace == null) { return ; }
            if (trace is ServerTimesViewModel serverTimings && trace.HasData) {
                _serverTimingsComplete = true;
                _timingRecords.Add(new TimingRecord()
                {
                    QueryName = $"Query{CurrentQueryNumber}",
                    TotalDurationMs = serverTimings.TotalDuration,
                    FEDurationMs = serverTimings.FormulaEngineDuration,
                    SEDurationMs = serverTimings.StorageEngineDuration,
                    SEQueries = serverTimings.StorageEngineQueryCount
                });

            }
            if (trace is QueryPlanTraceViewModel  && trace.HasData) _queryPlanComplete = true;
            if (_serverTimingsComplete && _queryPlanComplete)
            {
                if (TotalQueries > 1) { await SaveTempFileAsync(); }
                
                if (CurrentQueryNumber < TotalQueries) {
                    _serverTimingsComplete = false;
                    _queryPlanComplete = false;
                    CurrentQueryNumber++;
                    RunQuery();
                }
            };
            if (_serverTimingsComplete && _queryPlanComplete && CurrentQueryNumber >= TotalQueries) await SaveAndExitAsync();
            return;
        }

        string _tempFolder;
        private async Task SaveTempFileAsync()
        {
            if (_tempFolder == null) {
                
                _tempFolder = GetTemporaryDirectory();
                System.Diagnostics.Debug.WriteLine($"Saving to {_tempFolder}");
                var vpa = _newDocument.ToolWindows.FirstOrDefault(tw => tw is VertiPaqAnalyzerViewModel) as VertiPaqAnalyzerViewModel;
                if (vpa != null)
                {
                    await vpa.ExportAnalysisDataAsync(Path.Combine(_tempFolder, "Model.vpax"));
                    _newDocument.ToolWindows.Remove(vpa);
                }
            }

            _newDocument.FileName = Path.Combine(_tempFolder, $"Query{CurrentQueryNumber}.daxx");
            _newDocument.IsDiskFileName = true;
            _newDocument.Save(); 
        }

        public string GetTemporaryDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            if (Directory.Exists(tempDirectory))
            {
                return GetTemporaryDirectory();
            }
            else
            {
                Directory.CreateDirectory(tempDirectory);
                return tempDirectory;
            }
        }

        public Task SaveAndExitAsync()
        {
            IsQueryRunning = false;
            QueryStatus = OperationStatus.Succeeded;
            IsSaveAsRunning = true;
            
            if (TotalQueries == 1)
                Ribbon.SaveAsDaxx();
            else
                SaveZip();
            IsSaveAsRunning = false;
            SaveAsStatus = OperationStatus.Succeeded;
            CanClose = true;
            CanCancel = false;
            _captureComplete = true;
            if (_queryPlanTrace != null) _queryPlanTrace.IsChecked = false;
            if (_serverTimingsTrace != null) _serverTimingsTrace.IsChecked = false;
            ResetState();
            return Task.CompletedTask;
        }

        private void SaveZip()
        {
            // Configure save file dialog box
            var dlg = new SaveFileDialog
            {
                FileName = "Results",
                FilterIndex = 0,
                Filter = "Zip file |*.zip"
            };

            // Show save file dialog box
            var result = dlg.ShowDialog();

            // Process save file dialog box results 
            if (result == true)
            {
                WriteSummaryCsv();
                // delete target file if it already exists?
                if (File.Exists(dlg.FileName) ) { File.Delete(dlg.FileName); }
                // save the temporary folder to a zip file
                ZipFile.CreateFromDirectory(_tempFolder, dlg.FileName);
                Directory.Delete(_tempFolder, true);
            }
            
        }

        private void WriteSummaryCsv()
        {
            var csvFilePath = Path.Combine(_tempFolder, "Summary.csv");
            var encoding = Encoding.UTF8;
            StreamWriter textWriter = null;
            try
            {
                textWriter = new StreamWriter(csvFilePath, false, encoding);
                // configure csv delimiter and culture
                var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.CurrentCulture) { HasHeaderRecord = true };
                
                using (var csvWriter = new CsvHelper.CsvWriter(textWriter, config))
                {
                    csvWriter.WriteHeader<TimingRecord>();
                    csvWriter.NextRecord();
                    csvWriter.WriteRecords(_timingRecords);
                    csvWriter.Flush();
                }
            }
            catch (Exception ex) {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(CaptureDiagnosticsViewModel), nameof(WriteSummaryCsv), "Error writing summary.csv");
            }
        }

        public Task HandleAsync(NoQueryTextEvent message, CancellationToken cancellationToken)
        {
            ResetState();
            _ = TryCloseAsync();
            return Task.CompletedTask;
        }

        private string GetOperationStatusImage(OperationStatus status)
        {
            switch (status)
            {
                case OperationStatus.Succeeded:
                    return TickImage;
                case OperationStatus.Failed: 
                    return CrossImage;
                case OperationStatus.Skipped:
                    return "warningDrawingImage";
                default:
                    return string.Empty;
            }
        }

        private void ResetState()
        {
            if (hasNewDocument) return;
            // only reset the state if we are using the active document to capture diagnostics
            if (_serverTimingsTrace != null) _serverTimingsTrace.IsChecked = _serverTimingsChecked;
            if (_queryPlanTrace != null) _queryPlanTrace.IsChecked = _queryPlanChecked;
            if (_selectedResultsTarget != null) Ribbon.SelectedTarget = _selectedResultsTarget;
        }

        private string _overallStatus;
        public string OverallStatus { 
            get => _overallStatus; 
            set { _overallStatus = value; NotifyOfPropertyChange(); } 
        }

        public Task HandleAsync(QueryFinishedEvent message, CancellationToken cancellationToken)
        {
            if (!message?.Successful??false)
            {
                IsQueryRunning = false;
                QueryStatus = OperationStatus.Failed;
                SaveAsStatus = OperationStatus.Failed;
                ResetState();
                CanCancel = false;
                CanClose = true;
            }
            return Task.CompletedTask;
        }

        public async Task HandleAsync(DocumentActivatedEvent message, CancellationToken cancellationToken)
        {
            if (_captureComplete) return;
            // if a new window has been opened use that to capture the VPAX metrics
            if (message.Document.IsConnected)
            {
                _newDocument = message.Document;
                
                // capture TraceWatcher checked status
                _serverTimingsTrace = _newDocument.TraceWatchers.First(tw => tw.GetType() == typeof(ServerTimesViewModel));
                _serverTimingsChecked = _serverTimingsTrace?.IsChecked ?? false;
                _queryPlanTrace = _newDocument.TraceWatchers.First(tw => tw.GetType() == typeof(QueryPlanTraceViewModel));
                _queryPlanChecked = _queryPlanTrace?.IsChecked ?? false;

                await CaptureMetricsAsync();
            }

        }

        private CaptureDiagnosticsSource _selectedQuerySource;
        public CaptureDiagnosticsSource SelectedQuerySource { get => _selectedQuerySource; 
            set { 
                _selectedQuerySource = value; 
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(TotalQueries));
                NotifyOfPropertyChange(nameof(ShowQueryProgress));
            } 
        }
        public int TotalQueries => SelectedQuerySource?.Queries?.Count()??0;
        private int _currentQueryNumber = 1;
        public int CurrentQueryNumber { get => _currentQueryNumber;
            private set {
                _currentQueryNumber = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(ProgressPercentage));
            } 
        }
        public bool ShowQueryProgress => TotalQueries > 1;
        public override Task TryCloseAsync(bool? dialogResult = null)
        {
            return base.TryCloseAsync(dialogResult);
        }

        public double ProgressPercentage =>  ((double)CurrentQueryNumber / (double)TotalQueries) *100;
    }
}
