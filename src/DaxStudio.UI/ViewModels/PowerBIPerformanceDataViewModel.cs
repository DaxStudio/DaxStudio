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
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Threading.Tasks;
using DaxStudio.UI.Extensions;
using System.Threading;

namespace DaxStudio.UI.ViewModels
{
    [Export]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    class PowerBIPerformanceDataViewModel: ToolWindowBase,
        IViewAware ,
        IDataGridWindow,
        IHandle<ConnectionChangedEvent>
        
    {
        private Dictionary<string, AggregateRewriteSummary> _rewriteEventCache = new Dictionary<string, AggregateRewriteSummary>();
        private IGlobalOptions Options { get; }
        private IEventAggregator _eventAggregator;
        private IWindowManager _windowManager;
        private RibbonViewModel Ribbon { get; }

        [ImportingConstructor]
        public PowerBIPerformanceDataViewModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions, IWindowManager windowManager, RibbonViewModel ribbon) : base()
        {
            PerformanceData = new BindableCollection<PowerBIPerformanceData>();
            performanceDataView = CollectionViewSource.GetDefaultView(PerformanceData);
            Options = globalOptions;
            _eventAggregator = eventAggregator;
            _windowManager = windowManager;
            Ribbon = ribbon;
            CanCaptureDiagnostics = Ribbon.ActiveDocument.IsConnected;
        }

        public override bool CanHide => true;
        //public new bool CanHide => true;
        public override string ContentId => "pbi-performance-data";

        public IObservableCollection<PowerBIPerformanceData> PerformanceData { get; }

        private readonly ICollectionView performanceDataView;

        public ICollectionView PerformanceDataView
        {
            get { return performanceDataView; }
        }



        // IToolWindow interface
        public override string Title => "PBI Performance";
        public override string DefaultDockingPane => "DockBottom";


        string _fileName = "";
        public string FileName { get { return _fileName; }
            internal set {
                _fileName = value;
                Log.Debug("{class} {method} {messge}", "PowerBIPerformanceDataViewModel", "FileName.Set", "About to load PowerBI Performance data from " + _fileName);
                try
                {
                    this.IsBusy = true;
                    JObject o1 = JObject.Parse(File.ReadAllText(_fileName));

                    // clear any existing Performance Data
                    PerformanceData.Clear();

                    // Load new data
                    int sequence = 1;
                    var perfDataDict = new Dictionary<string, PowerBIPerformanceData>();
                    foreach (var o2 in o1["events"].Children())
                    {
                        var perfLine = new PowerBIPerformanceData() {
                            Id = o2["id"].Value<string>(),
                            Name = o2["name"].Value<string>(),
                            Component = o2["component"]?.Value<string>()??"<unknown>",
                            ParentId = o2["parentId"]?.Value<string>()
                        };
                        switch (o2["name"].Value<string>()) {
                            //case "Query":
                                //perfLine.QueryStartTime = o2["start"].Value<DateTime>().ToLocalTime();
                                //perfLine.QueryEndTime = o2["end"]?.Value<DateTime>().ToLocalTime();
                                
                                //var parentLine = perfDataDict[perfLine.ParentId];
                                //parentLine.QueryStartTime = perfLine.QueryStartTime;
                                //parentLine.QueryEndTime = perfLine.QueryEndTime;
                                //break;

                            case "Execute DAX Query":
                                perfLine.QueryText = o2["metrics"]["QueryText"].Value<string>();
                                var rowCnt = o2["metrics"]["RowCount"];
                                var err = o2["metrics"]["Error"];
                                perfLine.RowCount = rowCnt?.Value<long>()??0;
                                perfLine.Error = err?.Value<bool>();
                                perfLine.QueryStartTime = o2["start"].Value<DateTime>().ToLocalTime();
                                perfLine.QueryEndTime = o2["end"]?.Value<DateTime>().ToLocalTime();

                                var semanticQueryLine = perfDataDict[perfLine.ParentId];
                                var queryLine = perfDataDict[semanticQueryLine.ParentId];
                                var vizLine = perfDataDict[queryLine.ParentId];
                                vizLine.QueryText = perfLine.QueryText;
                                vizLine.RowCount = perfLine.RowCount;
                                vizLine.QueryStartTime = perfLine.QueryStartTime;
                                vizLine.QueryEndTime = perfLine.QueryEndTime;
                                break;

                            //PerformanceData.Add(perfLine);
                            case "Visual Container Lifecycle":
                                perfLine.VisualName = o2["metrics"]["visualTitle"].Value<string>();
                                break;

                            case "Render":
                                perfLine.RenderStartTime = o2["start"].Value<DateTime>().ToLocalTime();
                                perfLine.RenderEndTime = o2["end"].Value<DateTime>().ToLocalTime();
                                var line2 = perfDataDict[perfLine.ParentId];
                                line2.RenderStartTime = perfLine.RenderStartTime;
                                line2.RenderEndTime = perfLine.RenderEndTime;
                                break;
                        }
                        if (!perfDataDict.ContainsKey(perfLine.Id))   perfDataDict.Add(perfLine.Id, perfLine);
                    }
                    foreach (var line in perfDataDict.Values)
                    {
                        if (line.Name == "Visual Container Lifecycle" && line.QueryText != null)
                        {
                            line.Sequence = sequence;
                            sequence++;
                            PerformanceData.Add(line);
                        }
                    }
                    CanCaptureDiagnostics = Ribbon?.ActiveDocument?.Connection?.IsConnected ?? false;
                    _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Information, $"Power BI Performance Data Loaded"));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "{class} {method} {message}", "PowerBIPerformanceDataViewModel", "FileName.set", ex.Message);
                    _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error Loading Power BI Performance Data: {ex.Message}"));
                }
                finally
                {
                    this.IsBusy = false;
                }
            }
        }

        public bool IsBusy { get; private set; }

        public bool IsCopyAllVisible { get { return true; } }
        public bool IsFilterVisible { get { return true; } }

        public bool CanCopyAll { get { return PerformanceData.Count > 0; } }

        public void CopyAll()
        {
            //We need to get the default view as that is where any filtering is done
            ICollectionView view = CollectionViewSource.GetDefaultView(PerformanceDataView);
            int sequence = 1;
            var sb = new StringBuilder();
            foreach (var itm in view)
            {
                var q = itm as PowerBIPerformanceData;
                if (q != null)
                {
                    sb.AppendLine();
                    sb.AppendLine($"// DAX query {sequence}");
                    sb.AppendLine($"{q.QueryText}");
                    sequence++;
                }

            }
            sb.AppendLine();
            _eventAggregator.PublishOnUIThreadAsync(new SendTextToEditor(sb.ToString()));
        }

        public void ClearFilters()
        {
            var vw = GetView() as Views.PowerBIPerformanceDataView;
            var controller = DataGridExtensions.GetDataGridFilterQueryController(vw.PerformanceData);
            controller.ClearFilter();
        }

        public void ClearAll()
        {
            this.PerformanceData.Clear();
        }

        public void QueryDoubleClick()
        {
            QueryDoubleClick(SelectedPerfData);
        }

        public PowerBIPerformanceData SelectedPerfData { get; set; }

        public void QueryDoubleClick(PowerBIPerformanceData perfData)
        {
            if (perfData == null) return; // it the user clicked on an empty query exit here
            string queryHeader = $"// =================\n";
            queryHeader += $"// Operation       : {perfData.Sequence} \n";
            queryHeader += $"// Visual          : {perfData.VisualName} \n";
            queryHeader += $"// Query Start     : {perfData.QueryStartTime}\n";
            queryHeader += $"// Query End       : {perfData.QueryEndTime}\n";
            queryHeader += $"// Render Start    : {perfData.RenderStartTime} \n";
            queryHeader += $"// Render End      : {perfData.RenderEndTime}\n";
            queryHeader += $"// Query Duration  : {perfData.QueryDuration} ms\n";
            queryHeader += $"// Render Duration : {perfData.RenderDuration} ms\n";
            queryHeader += $"// Total Duration  : {perfData.TotalDuration} ms\n";
            queryHeader += $"// Row Count       : {perfData.RowCount}\n";
            queryHeader += $"// =================\n";
            queryHeader += perfData.QueryText;
            _eventAggregator.PublishOnUIThreadAsync(new SendTextToEditor(queryHeader + "\n"));
        }

        private bool _showFilters;
        public bool ShowFilters { get { return _showFilters; } set { if (value != _showFilters) { _showFilters = value; NotifyOfPropertyChange(() => ShowFilters); } } }

        private bool _canCaptureDiagnostics;
        public bool CanCaptureDiagnostics { 
            get => _canCaptureDiagnostics; 
            set { 
                _canCaptureDiagnostics = value;
                NotifyOfPropertyChange();
            } 
        }
        public async Task CaptureDiagnostics()
        {
            // get the list of queries with any user filters applied
            var list = this.PerformanceDataView.Cast<IQueryTextProvider>();

            var capdiagDialog = new CaptureDiagnosticsViewModel(Ribbon, Options, _eventAggregator, list);
            _eventAggregator.SubscribeOnPublishedThread(capdiagDialog);
            await _windowManager.ShowDialogBoxAsync(capdiagDialog);
            _eventAggregator.Unsubscribe(capdiagDialog);
        }

        public Task HandleAsync(ConnectionChangedEvent message, CancellationToken cancellationToken)
        {
            CanCaptureDiagnostics = message.Document.Connection.IsConnected;
            return Task.CompletedTask;
        }

        public override Task TryCloseAsync(bool? dialogResult = null)
        {
            _eventAggregator.Unsubscribe(this);
            return base.TryCloseAsync(dialogResult);
        }
    }
}
