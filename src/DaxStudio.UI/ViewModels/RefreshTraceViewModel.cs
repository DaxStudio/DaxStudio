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
using DaxStudio.UI.Utils;

namespace DaxStudio.UI.ViewModels
{

    class RefreshTraceViewModel
        : TraceWatcherBaseViewModel, 
        ISaveState, 
        IViewAware 
        
    {

        private readonly IGlobalOptions _globalOptions;

        [ImportingConstructor]
        public RefreshTraceViewModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions) : base(eventAggregator, globalOptions)
        {
            _queryEvents = new BindableCollection<QueryEvent>();
            _globalOptions = globalOptions;
            QueryTypes = new ObservableCollection<string>
            {
                "DAX",
                "Dmx",
                "Mdx",
                "Sql"
            };
        }


        public ObservableCollection<string> QueryTypes { get; set; }

        protected override List<DaxStudioTraceEventClass> GetMonitoredEvents()
        {
            return new List<DaxStudioTraceEventClass>
                { DaxStudioTraceEventClass.CommandBegin,
                  DaxStudioTraceEventClass.CommandEnd,
                  //DaxStudioTraceEventClass.JobGraph,
                  DaxStudioTraceEventClass.ProgressReportBegin,
                  DaxStudioTraceEventClass.ProgressReportCurrent,
                  DaxStudioTraceEventClass.ProgressReportEnd,
                  DaxStudioTraceEventClass.Error
            };
        }

        // This method is called after the WaitForEvent is seen (usually the QueryEnd event)
        // This is where you can do any processing of the events before displaying them to the UI
        protected override void ProcessResults()
        {

            //if (IsPaused) return; // exit here if we are paused

            if (Events == null) return;
            
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

                    
                RefreshEvents.Add(newEvent);
                        
                    
            }
                
            Events.Clear();


            NotifyOfPropertyChange(() => RefreshEvents);
            NotifyOfPropertyChange(() => CanClearAll);
            NotifyOfPropertyChange(() => CanCopyAll);
            NotifyOfPropertyChange(() => CanExport);
        }
        
 
        private readonly BindableCollection<QueryEvent> _queryEvents;
        
        public override bool CanHide => true; 
        public override string ContentId => "refresh-trace";
        public override ImageSource IconSource
        {
            get
            {
                var imgSourceConverter = new ImageSourceConverter();
                return imgSourceConverter.ConvertFromInvariantString(
                    @"pack://application:,,,/DaxStudio.UI;component/images/icon-all-queries@17px.png") as ImageSource;

            }
        }
        public IObservableCollection<QueryEvent> RefreshEvents => _queryEvents;


        public string DefaultQueryFilter => "cat";

        // IToolWindow interface
        public override string Title => "Refresh Trace";

        public override string ToolTipText => "Runs a server trace to record data refresh details";

        public override bool FilterForCurrentSession => false;

        public override void ClearAll()
        {
            RefreshEvents.Clear();
            NotifyOfPropertyChange(() => CanClearAll);
            NotifyOfPropertyChange(() => CanCopyAll);
            NotifyOfPropertyChange(() => CanExport);
        }

        
        public bool CanClearAll => RefreshEvents.Count > 0;

        public override void OnReset() {
            IsBusy = false;
            Events.Clear();
            ProcessResults();
        }

        public QueryEvent SelectedQuery { get; set; }

        public override bool IsCopyAllVisible => true;
        public override bool IsFilterVisible => true; 

        public bool CanCopyAll => RefreshEvents.Count > 0;

        public override void CopyAll()
        {
            //We need to get the default view as that is where any filtering is done
            ICollectionView view = CollectionViewSource.GetDefaultView(RefreshEvents);

            var sb = new StringBuilder();
            foreach (var itm in view)
            {
                if (itm is QueryEvent q)
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
            var vw = GetView() as Views.RefreshTraceView;
            if (vw == null) return;
            var controller = DataGridExtensions.GetDataGridFilterQueryController(vw.RefreshEvents);
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
            string json = GetJsonString();
            File.WriteAllText(filename + ".refreshTrace", json);
        }

        private string GetJsonString()
        {
            return JsonConvert.SerializeObject(RefreshEvents, Formatting.Indented);
        }

        void ISaveState.Load(string filename)
        {
            filename = filename + ".refreshTrace";
            if (!File.Exists(filename)) return;

            _eventAggregator.PublishOnUIThread(new ShowTraceWindowEvent(this));
            string data = File.ReadAllText(filename);
            LoadJsonString(data);
        }

        private void LoadJsonString(string data)
        {
            List<QueryEvent> qe = JsonConvert.DeserializeObject<List<QueryEvent>>(data);

            _queryEvents.Clear();
            _queryEvents.AddRange(qe);
            NotifyOfPropertyChange(() => RefreshEvents);
        }

        public void SavePackage(Package package)
        {

            Uri uriTom = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.AllQueries, UriKind.Relative));
            using (TextWriter tw = new StreamWriter(package.CreatePart(uriTom, "application/json", CompressionOption.Maximum).GetStream(), Encoding.UTF8))
            {
                tw.Write(GetJsonString());
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
                LoadJsonString(data);
                
            }

        }



        public void SetDefaultFilter(string column, string value)
        {
            var vw = this.GetView() as Views.RefreshTraceView;
            if (vw == null) return;
            var controller = DataGridExtensions.GetDataGridFilterQueryController(vw.RefreshEvents);
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
            File.WriteAllText(filePath, GetJsonString());
        }


        #endregion

    }
}
