using System.Collections.Generic;
using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Interfaces;
using DaxStudio.QueryTrace;
using DaxStudio.Interfaces;
using DaxStudio.UI.Model;
using System.IO;
using Newtonsoft.Json;
using System.Text;
using DaxStudio.Controls.DataGridFilter;
using System.Linq;
using System.ComponentModel;
using System.Windows.Data;
using System.Collections.ObjectModel;
using System;
using System.IO.Packaging;
using System.Windows.Media;
using DaxStudio.UI.Extensions;
using DaxStudio.Common;
using DaxStudio.UI.Utils;
using DaxStudio.Common.Enums;
using System.Windows;
using Serilog;
using System.Threading.Tasks;
using System.Threading;
using System.Linq.Dynamic;
using SharpCompress;

namespace DaxStudio.UI.ViewModels
{

    class AllServerQueriesViewModel
        : TraceWatcherBaseViewModel, 
        ISaveState, 
        IViewAware,
        IHandle<ConnectionChangedEvent>
        
    {
        private readonly Dictionary<string, AggregateRewriteSummary> _rewriteEventCache = new Dictionary<string, AggregateRewriteSummary>();
        private readonly Dictionary<string, QueryBeginEvent> _queryBeginCache = new Dictionary<string, QueryBeginEvent>();
        private readonly RibbonViewModel Ribbon;

        [ImportingConstructor]
        public AllServerQueriesViewModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions, IWindowManager windowManager, RibbonViewModel ribbon) : base(eventAggregator, globalOptions,windowManager)
        {
            _queryEvents = new BindableCollection<QueryEvent>();
            queryEventsView = CollectionViewSource.GetDefaultView(QueryEvents);
            Ribbon = ribbon;
            CanCaptureDiagnostics = Ribbon?.ActiveDocument?.IsConnected??false;
            this.ViewAttached += AllServerQueriesViewModel_ViewAttached;
            QueryTypes = new ObservableCollection<string>
            {
                "DAX",
                "DMX",
                "MDX",
                "SQL",
                "Xmla" // Intentionally lowercase to reduce width
            };
        }

        private readonly ICollectionView queryEventsView;

        public ICollectionView QueryEventsView
        {
            get { return queryEventsView; }
        }

        private void AllServerQueriesViewModel_ViewAttached(object sender, ViewAttachedEventArgs e)
        {
            CanCaptureDiagnostics = Ribbon?.ActiveDocument?.IsConnected ?? false;
        }

        public ObservableCollection<string> QueryTypes { get; set; }

        protected override List<DaxStudioTraceEventClass> GetMonitoredEvents()
        {
            var monitoredEvents = new List<DaxStudioTraceEventClass>
                { DaxStudioTraceEventClass.QueryEnd,
                  DaxStudioTraceEventClass.QueryBegin,
                  DaxStudioTraceEventClass.Error,
                  DaxStudioTraceEventClass.AggregateTableRewriteQuery
                  
            };

            if (GlobalOptions.ShowXmlaInAllQueries)
            {
                monitoredEvents.Add(DaxStudioTraceEventClass.CommandEnd);
            }

            return monitoredEvents;
        }

        protected override void ProcessSingleEvent(DaxStudioTraceEventArgs singleEvent)
        {
            base.ProcessSingleEvent(singleEvent);

            //if (IsPaused) return; // exit here if we are paused

            //if (Events != null)
            //{
            //    while (!Events.IsEmpty)
            //    {
                    Events.TryDequeue(out var traceEvent);

            
                    var newEvent = new QueryEvent()
                    {
                        QueryType = traceEvent.EventSubclassName.Substring(0, 3).ToUpper(),
                        StartTime = traceEvent.StartTime,
                        Username = traceEvent.NTUserName,
                        Query = traceEvent.TextData,
                        Duration = traceEvent.Duration,
                        DatabaseName = traceEvent.DatabaseFriendlyName,
                        RequestID = traceEvent.RequestID,
                        RequestParameters = traceEvent.RequestParameters,
                        RequestProperties = traceEvent.RequestProperties,
                        ActivityID = traceEvent.ActivityId
                    };

                    switch (traceEvent.EventClass)
                    {
                        case DaxStudioTraceEventClass.QueryEnd:

                            // if this is the blank query after a "clear cache and run" then skip it
                            if (newEvent.Query == Constants.RefreshSessionQuery) return;

                            // look for any cached rewrite events
                            if (traceEvent.RequestID != null && _rewriteEventCache.ContainsKey(traceEvent.RequestID))
                            {
                                var summary = _rewriteEventCache[traceEvent.RequestID];
                                newEvent.AggregationMatchCount = summary.MatchCount;
                                newEvent.AggregationMissCount = summary.MissCount;
                                _rewriteEventCache.Remove(traceEvent.RequestID);
                            }

                            // check if we have a queryBegin event cached
                            _queryBeginCache.TryGetValue(traceEvent.RequestID ?? "", out var beginEvent);
                            if (beginEvent != null)
                            {

                                //// Add the parameters XML after the query text
                                //if (beginEvent.RequestParameters != null)
                                //    newEvent.Query += Environment.NewLine +
                                //                      Environment.NewLine +
                                //                      beginEvent.RequestParameters +
                                //                      Environment.NewLine;

                                //// overwrite the username with the effective user if it's present
                                //var effectiveUser = beginEvent.ParseEffectiveUsername();
                                //if (!string.IsNullOrEmpty(effectiveUser)) newEvent.Username = effectiveUser;

                                _queryBeginCache.Remove(traceEvent.RequestID);

                                // copy end event properties to the begin event
                                beginEvent.QueryEvent.Duration = newEvent.Duration;
                                beginEvent.QueryEvent.EndTime = newEvent.EndTime;
                                beginEvent.QueryEvent.AggregationMatchCount = newEvent.AggregationMatchCount;
                                beginEvent.QueryEvent.AggregationMissCount = newEvent.AggregationMissCount;
                            }
                            else
                            {

                                QueryEvents.Insert(0, newEvent);
                            }
                            break;
                        case DaxStudioTraceEventClass.Error:
                            newEvent.QueryType = "ERR";
                            QueryEvents.Insert(0, newEvent);
                            break;
                        case DaxStudioTraceEventClass.CommandEnd:
                            newEvent.QueryType = "Xmla";
                            QueryEvents.Insert(0, newEvent);
                            break;
                        case DaxStudioTraceEventClass.AggregateTableRewriteQuery:
                            // cache rewrite events
                            var rewriteSummary = new AggregateRewriteSummary(traceEvent.RequestID, traceEvent.TextData);
                            if (_rewriteEventCache.ContainsKey(traceEvent.RequestID))
                            {
                                var summary = _rewriteEventCache[key: traceEvent.RequestID];
                                summary.MatchCount += rewriteSummary.MatchCount;
                                summary.MissCount += rewriteSummary.MissCount;
                                _rewriteEventCache[key: traceEvent.RequestID] = summary;
                            }
                            else
                            {
                                _rewriteEventCache.Add(traceEvent.RequestID, rewriteSummary);
                            }

                            break;

                        case DaxStudioTraceEventClass.QueryBegin:

                            // if the requestID is null we are running against PowerPivot which does
                            // not seem to expose the RequestID property
                            if (traceEvent.RequestID == null) return;

                            // if this is a session refresh query then skip it
                            if (newEvent.Query == Constants.RefreshSessionQuery) return;

                            // cache rewrite events
                            if (_queryBeginCache.ContainsKey(traceEvent.RequestID))
                            {
                                // TODO - this should not happen
                                // we should not get 2 begin events for the same request
                                System.Diagnostics.Debug.Assert(true, "we should not have multiple QueryBegin events for the same request");
                            }
                            else
                            {
                        var newBeginEvent = new QueryBeginEvent()
                        {
                            RequestID = traceEvent.RequestID,
                            Query = traceEvent.TextData,
                            RequestProperties = traceEvent.RequestProperties,
                            RequestParameters = traceEvent.RequestParameters,
                            ActivityID = traceEvent.ActivityId,
                            QueryEvent = newEvent
                                };
                                _queryBeginCache.Add(traceEvent.RequestID, newBeginEvent);

                                // Add the parameters XML after the query text
                                if (newEvent.RequestParameters != null)
                                    newEvent.Query += Environment.NewLine +
                                                      Environment.NewLine +
                                                      newEvent.RequestParameters +
                                                      Environment.NewLine;
                                newEvent.Duration = -1;
                                // overwrite the username with the effective user if it's present
                                var effectiveUser = newEvent.ParseEffectiveUsername();
                                if (!string.IsNullOrEmpty(effectiveUser)) newEvent.Username = effectiveUser;

                                QueryEvents.Insert(0, newEvent);
                            }

                            break;
                    }
                //}

                //Events.Clear();

                // Clear out any cached rewrite events older than 10 minutes
                var toRemoveFromCache = _rewriteEventCache.Where((kvp) => kvp.Value.UtcCurrentTime > DateTime.UtcNow.AddMinutes(10)).Select(c => c.Key).ToList();
                foreach (var requestId in toRemoveFromCache)
                {
                    _rewriteEventCache.Remove(requestId);
                }

                NotifyOfPropertyChange(() => QueryEvents);
                NotifyOfPropertyChange(() => CanClearAll);
                NotifyOfPropertyChange(() => CanCopyAll);
                NotifyOfPropertyChange(() => CanExport);
            //}

        }

        // This method is called after the WaitForEvent is seen (usually the QueryEnd event)
        // This is where you can do any processing of the events before displaying them to the UI
        protected override void ProcessResults() {

            return;
        }
        
 
        private readonly BindableCollection<QueryEvent> _queryEvents;
        
        public override bool CanHide { get { return true; } }
        public override string ContentId => "all-queries-trace";
        public override string TraceSuffix => "all";
        public override int SortOrder => 10;
        public IObservableCollection<QueryEvent> QueryEvents 
        {
            get {
                return _queryEvents;
            }
        }

        

        public string DefaultQueryFilter => "cat";

        // IToolWindow interface
        public override string Title => "All Queries";
        public override string ImageResource => "all_queriesDrawingImage";
        public override string ToolTipText => "Runs a server trace to record all queries from all users for the current connection";
        public override string KeyTip => "AQ";
        public override bool FilterForCurrentSession { get { return false; } }

        protected override bool IsFinalEvent(DaxStudioTraceEventArgs traceEvent)
        {
            return traceEvent.EventClass == DaxStudioTraceEventClass.QueryEnd ||
                   traceEvent.EventClass == DaxStudioTraceEventClass.Error;
        }

        public override void ClearAll()
        {
            QueryEvents.Clear();
            NotifyOfPropertyChange(() => CanClearAll);
            NotifyOfPropertyChange(() => CanCopyAll);
            NotifyOfPropertyChange(() => CanExport);
        }

        
        public bool CanClearAll => QueryEvents.Count > 0;

        public override void OnReset() {
            IsBusy = false;
            Events.Clear();
            ProcessResults();
        }

        private QueryEvent _selectedQuery; 
        public QueryEvent SelectedQuery { get => _selectedQuery;
            set { 
                _selectedQuery = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(CanShowTraceDiagnostics));
            }
        }
        public override void CopyEventContent()
        {
            Log.Warning("CopyEventContent not implemented for AllServerQueriesViewModel");
            throw new NotImplementedException();
        }

        public override bool IsCopyAllVisible { get { return true; } }
        public override bool IsFilterVisible { get { return true; } }

        public bool CanCopyAll { get { return QueryEvents.Count > 0; } }

        public override void CopyAll()
        {
            //We need to get the default view as that is where any filtering is done
            ICollectionView view = CollectionViewSource.GetDefaultView(QueryEvents);

            var sb = new StringBuilder();
            foreach (var itm in view)
            {
                var q = itm as QueryEvent;
                if (q != null)
                {
                    sb.AppendLine();
                    sb.AppendLine($"// {q.QueryType} query against Database: {q.DatabaseName} ");
                    sb.AppendLine($"{q.Query}");
                }

            }
            sb.AppendLine();
            _eventAggregator.PublishOnUIThreadAsync(new SendTextToEditor(sb.ToString()));
        }


        public override void CopyResults()
        {
            // not supported by AllQueries
            throw new NotImplementedException();
        }

        public override void ClearFilters()
        {
            var vw = GetView() as Views.AllServerQueriesView;
            var controller = DataGridExtensions.GetDataGridFilterQueryController(vw.QueryEvents);
            controller.ClearFilter();
        }

        public void QueryDoubleClick()
        {
            QueryDoubleClick(SelectedQuery);
        }

        public void QueryDoubleClick(QueryEvent query)
        {
            if (query == null) return; // it the user clicked on an empty query exit here
            _eventAggregator.PublishOnUIThreadAsync(new SendTextToEditor(query.Query + "\n", query.DatabaseName));
        }

        #region ISaveState methods
        void ISaveState.Save(string filename)
        {
            string json = GetJson();
            File.WriteAllText(filename + ".allQueries", json);
        }

        public string GetJson()
        {
            var json =  JsonConvert.SerializeObject(QueryEvents, Formatting.Indented);
            return json;
        }

        void ISaveState.Load(string filename)
        {
            filename = filename + ".allQueries";
            if (!File.Exists(filename)) return;

            _eventAggregator.PublishOnUIThreadAsync(new ShowTraceWindowEvent(this));
            string data = File.ReadAllText(filename);
            LoadJson(data);
        }

        public void LoadJson(string data)
        {
            List<QueryEvent> qe = JsonConvert.DeserializeObject<List<QueryEvent>>(data);

            _queryEvents.Clear();
            _queryEvents.AddRange(qe);
            NotifyOfPropertyChange(() => QueryEvents);
        }

        public void SavePackage(Package package)
        {

            Uri uriTom = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.AllQueries, UriKind.Relative));
            using (TextWriter tw = new StreamWriter(package.CreatePart(uriTom, "application/json", CompressionOption.Maximum).GetStream(), Encoding.UTF8))
            {
                tw.Write(GetJson());
                tw.Close();
            }
        }

        public void LoadPackage(Package package)
        {
            var uri = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.AllQueries, UriKind.Relative));
            if (!package.PartExists(uri)) return;

            _eventAggregator.PublishOnUIThreadAsync(new ShowTraceWindowEvent(this));
            var part = package.GetPart(uri);
            using (TextReader tr = new StreamReader(part.GetStream()))
            {
                string data = tr.ReadToEnd();
                LoadJson(data);
                
            }

        }



        public void SetDefaultFilter(string column, string value)
        {
            var vw = this.GetView() as Views.AllServerQueriesView;
            var controller = DataGridExtensions.GetDataGridFilterQueryController(vw.QueryEvents);
            var filters = controller.GetFiltersForColumns();

            var columnFilter = filters.FirstOrDefault(w => w.Key == column);
            if (columnFilter.Key != null)
            {
                columnFilter.Value.QueryString = value;

                controller.SetFiltersForColumns(filters);
            }
        }

        public override bool CanExport => _queryEvents.Count > 0;

        public override void ExportTraceDetails(string filePath)
        {
            File.WriteAllText(filePath, GetJson());
        }


        public bool CanShowTraceDiagnostics => SelectedQuery != null;
        public async void ShowTraceDiagnostics()
        {
            var traceDiagnosticsViewModel = new RequestInformationViewModel(SelectedQuery);
            await WindowManager.ShowDialogBoxAsync(traceDiagnosticsViewModel, settings: new Dictionary<string, object>
            {
                { "WindowStyle", WindowStyle.None},
                { "ShowInTaskbar", false},
                { "ResizeMode", ResizeMode.NoResize},
                { "Background", Brushes.Transparent},
                { "AllowsTransparency",true}

            });
        }

        private bool _canCaptureDiagnostics;
        public bool CanCaptureDiagnostics
        {
            get => _canCaptureDiagnostics;
            set
            {
                _canCaptureDiagnostics = value;
                NotifyOfPropertyChange();
            }
        }
        public async Task CaptureDiagnostics()
        {
            // Get the list of events with any user filters applied
            var list = this.QueryEventsView.Cast<QueryEvent>().ToList();
            // only profile DAX/MDX queries (so exclude DMX and errors)
            var daxQueries = list.Where(qe => qe.QueryType == "DAX" || qe.QueryType == "MDX").Cast<IQueryTextProvider>();

            var capdiagDialog = new CaptureDiagnosticsViewModel(Ribbon, _globalOptions, _eventAggregator, daxQueries);
            _eventAggregator.SubscribeOnPublishedThread(capdiagDialog);
            await _windowManager.ShowDialogBoxAsync(capdiagDialog);
            _eventAggregator.Unsubscribe(capdiagDialog);
        }

        public Task HandleAsync(ConnectionChangedEvent message, CancellationToken cancellationToken)
        {
            CanCaptureDiagnostics = message.Document?.Connection?.IsConnected??false;
            return Task.CompletedTask;
        }

        #endregion

    }
}
