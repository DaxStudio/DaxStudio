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

namespace DaxStudio.UI.ViewModels
{

    class PowerBIPerformanceDataViewModel: ToolWindowBase,
        IViewAware ,
        IDataGridWindow
        
    {
        private Dictionary<string, AggregateRewriteSummary> _rewriteEventCache = new Dictionary<string, AggregateRewriteSummary>();
        private IGlobalOptions _globalOptions;
        private IEventAggregator _eventAggregator;

        [ImportingConstructor]
        public PowerBIPerformanceDataViewModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions) : base()
        {
            PerformanceData = new BindableCollection<PowerBIPerformanceData>();
            _globalOptions = globalOptions;
            _eventAggregator = eventAggregator;
        }


        public new bool CanHide => true;
        public override string ContentId => "pbi-performance-data";
        public override ImageSource IconSource
        {
            get
            {
                var imgSourceConverter = new ImageSourceConverter();
                return imgSourceConverter.ConvertFromInvariantString(
                    @"pack://application:,,,/DaxStudio.UI;component/images/icon-pbi-tachometer@2x.png") as ImageSource;

            }
        }

        public IObservableCollection<PowerBIPerformanceData> PerformanceData { get; } 
       

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
                            Component = o2["component"].Value<string>(),
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
                                perfLine.RowCount = o2["metrics"]["RowCount"].Value<long>();
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
                        if (!perfDataDict.Keys.Contains(perfLine.Id))   perfDataDict.Add(perfLine.Id, perfLine);
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

                    _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Information, $"Power BI Performance Data Loaded"));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "{class} {method} {message}", "PowerBIPerformanceDataViewModel", "FileName.set", ex.Message);
                    _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error Loading Power BI Performance Data: {ex.Message}"));
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
            ICollectionView view = CollectionViewSource.GetDefaultView(PerformanceData);
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
            _eventAggregator.PublishOnUIThread(new SendTextToEditor(sb.ToString()));
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
            _eventAggregator.PublishOnUIThread(new SendTextToEditor(queryHeader + "\n"));
        }

        private bool _showFilters;
        public bool ShowFilters { get { return _showFilters; } set { if (value != _showFilters) { _showFilters = value; NotifyOfPropertyChange(() => ShowFilters); } } }

    }
}
