using Caliburn.Micro;
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
        private const string TickImage = "successDrawingImage";
        private const string CrossImage = "failDrawingImage";
        private object traceEventLock = new object();
        #region Properties
        public bool IsMetricsRunning { get; set; }
        public bool IsServerTimingsStarting { get; set; }
        public bool IsQueryPlanStarting { get; set; }
        public bool IsQueryRunning { get; set; }

        public string MetricsStatusImage => _metricsSucceeded?TickImage:CrossImage;
        public string ServerTimingsStatusImage => _serverTimingsSucceeded?TickImage:CrossImage;
        public string QueryPlanStatusImage => _queryPlanSucceeded?TickImage:CrossImage;
        public string QueryStatusImage => _querySucceeded?TickImage:CrossImage;

        private bool _metricsSucceeded;
        private bool _serverTimingsSucceeded;
        private bool _queryPlanSucceeded;
        private bool _querySucceeded;
        private bool _serverTimingsComplete;
        private bool _queryPlanComplete;

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
        #endregion
        public CaptureDiagnosticsViewModel(RibbonViewModel ribbon, IEventAggregator eventAggregator)
        {
            Ribbon = ribbon;
            EventAggregator = eventAggregator;
        }

        public void Run()
        {
            CaptureMetrics();        
        }

        public void Cancel()
        {

        }

        public void CaptureMetrics()
        {
            Ribbon.ViewAnalysisData();
        }

        private void StartTraces()
        {
            var serverTimings = Ribbon.TraceWatchers.First(tw => tw.GetType() == typeof(ServerTimesViewModel));
            var queryPlan = Ribbon.TraceWatchers.First(tw => tw.GetType() == typeof(QueryPlanTraceViewModel));

            EnsureTraceIsStarted(serverTimings);
            EnsureTraceIsStarted(queryPlan);
            

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
            StartTraces();
            return Task.CompletedTask;
        }

        public Task HandleAsync(TraceChangedEvent message, CancellationToken cancellationToken)
        {
            bool _tracesStarted;
            lock (traceEventLock)
            {
                if (message.TraceStatus == QueryTrace.Interfaces.QueryTraceStatus.Started)
                {
                    if (message.Sender is QueryPlanTraceViewModel)
                    {
                        _queryPlanSucceeded = true;
                        IsQueryPlanStarting = false;
                    }
                    if (message.Sender is ServerTimesViewModel)
                    {
                        _serverTimingsSucceeded = true;
                        IsServerTimingsStarting = false;
                    }
                }
                if (message.TraceStatus == QueryTrace.Interfaces.QueryTraceStatus.Error)
                {
                    if (message.Sender is QueryPlanTraceViewModel)
                    {
                        _queryPlanSucceeded = false;
                        IsQueryPlanStarting = false;
                    }
                    if (message.Sender is ServerTimesViewModel)
                    {
                        _serverTimingsSucceeded = false;
                        IsServerTimingsStarting = false;
                    }
                }
                _tracesStarted = _queryPlanSucceeded && _serverTimingsSucceeded;
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

        private void QueryComplete()
        {
            _ = TryCloseAsync(true);
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
            Ribbon.SaveAsDaxx();
            _ = TryCloseAsync(true);
        }

        public Task HandleAsync(NoQueryTextEvent message, CancellationToken cancellationToken)
        {
            Cancel();
            return Task.CompletedTask;
        }
    }
}
