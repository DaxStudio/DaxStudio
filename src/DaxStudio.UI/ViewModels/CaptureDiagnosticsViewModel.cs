using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DaxStudio.UI.ViewModels
{

    public class CaptureDiagnosticsViewModel:Screen,
        IHandle<ViewMetricsCompleteEvent>,
        IHandle<TraceChangedEvent>,
        IHandle<QueryTraceCompletedEvent>,
        IHandle<NoQueryTextEvent>
    {

        public enum OperationStatus
        {
            Waiting,
            Succeeded,
            Failed
        }

        public CaptureDiagnosticsViewModel(RibbonViewModel ribbon, IEventAggregator eventAggregator)
        {
            Ribbon = ribbon;
            EventAggregator = eventAggregator;
            
            // capture TraceWatcher checked status
            _serverTimingsTrace = Ribbon.TraceWatchers.First(tw => tw.GetType() == typeof(ServerTimesViewModel));
            _serverTimingsChecked = _serverTimingsTrace?.IsChecked ?? false;
            _queryPlanTrace = Ribbon.TraceWatchers.First(tw => tw.GetType() == typeof(QueryPlanTraceViewModel));
            _queryPlanChecked = _queryPlanTrace?.IsChecked ?? false;
            
            // start capturing
            Run();
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

        public bool IsMetricsRunning { get => _isMetricsRunning;
            set { 
                _isMetricsRunning = value;
                NotifyOfPropertyChange(() => IsMetricsRunning);
            } 
        }
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
        private IEventAggregator EventAggregator { get; }

        private ITraceWatcher _serverTimingsTrace;
        private ITraceWatcher _queryPlanTrace;
        #endregion


        public void Run()
        {
            IsMetricsRunning = true;
            CaptureMetrics();        
        }

        private bool _canClose = false;
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
            ResetTraces();
        }

        public void CaptureMetrics()
        {
            Ribbon.ViewAnalysisData();
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
            
            StartTraces();
            return Task.CompletedTask;
        }

        public Task HandleAsync(TraceChangedEvent message, CancellationToken cancellationToken)
        {
            bool _tracesStarted;
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
                    default:
                        // ignore any other status change events
                        return Task.CompletedTask;
                }
                _tracesStarted = QueryPlanStatus == OperationStatus.Succeeded 
                                && ServerTimingsStatus == OperationStatus.Succeeded ;
            }
            if (_tracesStarted)
            {
                RunQuery();
            }

            return Task.CompletedTask;
        }

        private void RunQuery()
        {
            IsQueryRunning = true;
            Ribbon.RunQuery();
        }


        public Task HandleAsync(QueryTraceCompletedEvent message, CancellationToken cancellationToken)
        {
            if (message.Trace is ServerTimesViewModel) _serverTimingsComplete = true;
            if (message.Trace is QueryPlanTraceViewModel) _queryPlanComplete = true;
            if (_serverTimingsComplete && _queryPlanComplete) SaveAndExit();
            return Task.CompletedTask;
        }

        public void SaveAndExit()
        {
            IsQueryRunning = false;
            QueryStatus = OperationStatus.Succeeded;
            IsSaveAsRunning = true;
            if(_queryPlanTrace != null) _queryPlanTrace.IsChecked = false;
            if(_serverTimingsTrace != null) _serverTimingsTrace.IsChecked =false;
            Ribbon.SaveAsDaxx();
            IsSaveAsRunning = false;
            SaveAsStatus = OperationStatus.Succeeded;
            CanClose = true;
            CanCancel = false;
            ResetTraces();
        }

        public Task HandleAsync(NoQueryTextEvent message, CancellationToken cancellationToken)
        {
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
                default:
                    return string.Empty;
            }
        }

        private void ResetTraces()
        {
            if (_serverTimingsTrace != null) _serverTimingsTrace.IsChecked = _serverTimingsChecked;
            if (_queryPlanTrace != null) _queryPlanTrace.IsChecked = _queryPlanChecked;
        }
    }
}
