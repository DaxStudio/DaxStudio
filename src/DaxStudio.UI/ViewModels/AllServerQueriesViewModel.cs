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

namespace DaxStudio.UI.ViewModels
{

    class AllServerQueriesViewModel
        : TraceWatcherBaseViewModel, 
        ISaveState, 
        IViewAware 
        
    {
        private readonly Dictionary<string, AggregateRewriteSummary> _rewriteEventCache = new Dictionary<string, AggregateRewriteSummary>();
        private readonly Dictionary<string, QueryBeginEvent> _queryBeginCache = new Dictionary<string, QueryBeginEvent>();


        [ImportingConstructor]
        public AllServerQueriesViewModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions) : base(eventAggregator, globalOptions)
        {
            _queryEvents = new BindableCollection<QueryEvent>();
            QueryTypes = new ObservableCollection<string>
            {
                "DAX",
                "DMX",
                "MDX",
                "SQL",
                "Xmla" // Intentionally lowercase to reduce width
            };
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

        // This method is called after the WaitForEvent is seen (usually the QueryEnd event)
        // This is where you can do any processing of the events before displaying them to the UI
        protected override void ProcessResults() {

            //if (IsPaused) return; // exit here if we are paused

            if (Events != null) {
                foreach (var traceEvent in Events) {
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
                        RequestProperties = traceEvent.RequestProperties
                    };

                    switch (traceEvent.EventClass) {
                        case DaxStudioTraceEventClass.QueryEnd:

                            // if this is the blank query after a "clear cache and run" then skip it
                            if (newEvent.Query == Constants.RefreshSessionQuery) continue;

                            // look for any cached rewrite events
                            if (traceEvent.RequestID != null && _rewriteEventCache.ContainsKey(traceEvent.RequestID))
                            {
                                var summary = _rewriteEventCache[traceEvent.RequestID];
                                newEvent.AggregationMatchCount = summary.MatchCount;
                                newEvent.AggregationMissCount = summary.MissCount;
                                _rewriteEventCache.Remove(traceEvent.RequestID);
                            }

                            // check if we have a queryBegin event cached
                            _queryBeginCache.TryGetValue(traceEvent.RequestID??"", out var beginEvent);
                            if (beginEvent != null)
                            {

                                // Add the parameters XML after the query text
                                if (beginEvent.RequestParameters != null)
                                    newEvent.Query += Environment.NewLine + 
                                                      Environment.NewLine + 
                                                      beginEvent.RequestParameters + 
                                                      Environment.NewLine;

                                // overwrite the username with the effective user if it's present
                                var effectiveUser = beginEvent.ParseEffectiveUsername();
                                if (!string.IsNullOrEmpty(effectiveUser)) newEvent.Username = effectiveUser;

                                _queryBeginCache.Remove(traceEvent.RequestID);
                            }


                            

                            QueryEvents.Insert(0, newEvent);
                            break;
                        case DaxStudioTraceEventClass.Error:
                            newEvent.QueryType = "ERR";
                            QueryEvents.Insert(0,newEvent);
                            break;
                        case DaxStudioTraceEventClass.CommandEnd:
                            newEvent.QueryType = "Xmla";
                            QueryEvents.Insert(0, newEvent);
                            break;
                        case DaxStudioTraceEventClass.AggregateTableRewriteQuery:
                            // cache rewrite events
                            var rewriteSummary = new AggregateRewriteSummary(traceEvent.RequestID, traceEvent.TextData);
                            if (_rewriteEventCache.ContainsKey(traceEvent.RequestID)) {
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
                            if (traceEvent.RequestID == null) break;

                            // cache rewrite events
                            if (_queryBeginCache.ContainsKey(traceEvent.RequestID))
                            {
                                // TODO - this should not happen
                                // we should not get 2 begin events for the same request
                            }
                            else
                            {
                                var newBeginEvent = new QueryBeginEvent()
                                {
                                    RequestID = traceEvent.RequestID,
                                    Query = traceEvent.TextData,
                                    RequestProperties = traceEvent.RequestProperties,
                                    RequestParameters = traceEvent.RequestParameters
                                };
                                _queryBeginCache.Add(traceEvent.RequestID, newBeginEvent);
                            }

                            break;
                    }
                }
                
                Events.Clear();

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
            }
        }
        
 
        private readonly BindableCollection<QueryEvent> _queryEvents;
        
        public override bool CanHide { get { return true; } }
        public override string ContentId => "all-queries-trace";
        public override ImageSource IconSource
        {
            get
            {
                var imgSourceConverter = new ImageSourceConverter();
                return imgSourceConverter.ConvertFromInvariantString(
                    @"pack://application:,,,/DaxStudio.UI;component/images/icon-all-queries@17px.png") as ImageSource;

            }
        }
        public IObservableCollection<QueryEvent> QueryEvents 
        {
            get {
                return _queryEvents;
            }
        }

        

        public string DefaultQueryFilter => "cat";

        // IToolWindow interface
        public override string Title => "All Queries";

        public override string ToolTipText => "Runs a server trace to record all queries from all users for the current connection";

        public override bool FilterForCurrentSession { get { return false; } }

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

        public QueryEvent SelectedQuery { get; set; }

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
            _eventAggregator.PublishOnUIThread(new SendTextToEditor(sb.ToString()));
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
            _eventAggregator.PublishOnUIThread(new SendTextToEditor(query.Query + "\n", query.DatabaseName));
        }

        #region ISaveState methods
        void ISaveState.Save(string filename)
        {
            string json = GetJson();
            File.WriteAllText(filename + ".allQueries", json);
        }

        public string GetJson()
        {
            return JsonConvert.SerializeObject(QueryEvents, Formatting.Indented);
        }

        void ISaveState.Load(string filename)
        {
            filename = filename + ".allQueries";
            if (!File.Exists(filename)) return;

            _eventAggregator.PublishOnUIThread(new ShowTraceWindowEvent(this));
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

            _eventAggregator.PublishOnUIThread(new ShowTraceWindowEvent(this));
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


        #endregion

    }
}
