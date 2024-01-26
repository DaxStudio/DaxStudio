using System.Collections.Generic;
using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Interfaces;
using DaxStudio.QueryTrace;
using DaxStudio.UI.Model;
using System.IO;
using Newtonsoft.Json;
using System.Text;
using DaxStudio.Controls.DataGridFilter;
using System.Linq;
using System.ComponentModel;
using System.Windows.Data;
using System;
using System.IO.Packaging;
using System.Windows.Media;
using System.Xml;
using DaxStudio.Interfaces.Enums;
using DaxStudio.Interfaces;
using DaxStudio.UI.Utils;
using Formatting = Newtonsoft.Json.Formatting;
using Serilog;
using DaxStudio.Common.Enums;
using DaxStudio.Controls.PropertyGrid;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.JsonConverters;
using ControlzEx.Standard;
using Newtonsoft.Json.Linq;
using System.Windows.Navigation;
using Fluent;

namespace DaxStudio.UI.ViewModels
{

    public class EvaluateAndLogTraceViewModel
        : TraceWatcherBaseViewModel,
        ISaveState,
        IViewAware

    {


        [ImportingConstructor]
        public EvaluateAndLogTraceViewModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions, IWindowManager windowManager) : base(eventAggregator, globalOptions,windowManager)
        {
            _debugEvents = new BindableCollection<EvaluateAndLogEvent>();
        }

        protected override List<DaxStudioTraceEventClass> GetMonitoredEvents()
        {
            return new List<DaxStudioTraceEventClass>
                { DaxStudioTraceEventClass.QueryBegin,
                  DaxStudioTraceEventClass.QueryEnd,
                  DaxStudioTraceEventClass.DAXEvaluationLog,
                  DaxStudioTraceEventClass.Error
            };
        }


        protected override void ProcessSingleEvent(DaxStudioTraceEventArgs traceEvent)
        {
            if (IsPaused) return;
            if (traceEvent == null) return;
            if (traceEvent.EventClass != DaxStudioTraceEventClass.DAXEvaluationLog) return;
            base.ProcessSingleEvent(traceEvent);
            var newEvent = new EvaluateAndLogEvent()
            {

                StartTime = traceEvent.StartTime,
                //Username = traceEvent.NTUserName,
                Text = traceEvent.TextData,
                //CpuDuration = traceEvent.CpuTime,
                Duration = traceEvent.Duration,
                //DatabaseName = traceEvent.DatabaseFriendlyName,
                //RequestID = traceEvent.RequestID,
                //RequestParameters = traceEvent.RequestParameters,
                //RequestProperties = traceEvent.RequestProperties,
                //ObjectName = traceEvent.ObjectName,
                //ObjectPath = traceEvent.ObjectPath,
                //ObjectReference = traceEvent.ObjectReference,
                EventClass = traceEvent.EventClass,
                EventSubClass = traceEvent.EventSubclass,
                //ProgressTotal = traceEvent.ProgressTotal,
                //ActivityID = traceEvent.ActivityId,
                //SPID = traceEvent.SPID

            };
            try
            {

                DebugEvents.Add(newEvent);


            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(RefreshTraceViewModel), nameof(ProcessSingleEvent), ex.Message);
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"The following error occurred while processing trace events:\n{ex.Message}"));
            }
        }


        // This method is called after the WaitForEvent is seen (usually the QueryEnd event)
        // This is where you can do any processing of the events before displaying them to the UI
        protected override void ProcessResults()
        {

            //if (IsPaused) return; // exit here if we are paused

            if (Events == null) return;

            // todo summarize events
            while (!Events.IsEmpty)
            {
                Events.TryDequeue(out var traceEvent);
                // todo - produce summary
            }

            Events.Clear();


            NotifyOfPropertyChange(() => DebugEvents);
            NotifyOfPropertyChange(() => CanClearAll);
            NotifyOfPropertyChange(() => CanCopyAll);
            NotifyOfPropertyChange(() => CanExport);
        }


        private readonly BindableCollection<EvaluateAndLogEvent> _debugEvents;

        public override bool CanHide => true;
        public override string ContentId => "debug-log-trace";
        public IObservableCollection<EvaluateAndLogEvent> DebugEvents => _debugEvents;


        public string DefaultQueryFilter => "cat";

        // IToolWindow interface
        public override string Title => "Evaluate & Log";
        public override string TraceSuffix => "debug-log";
        public override string KeyTip => "EL";
        public override string ToolTipText => "Runs a server trace to capture the output from the EvaluateAndLog() DAX Function";
        public override int SortOrder => 50;
        public override bool FilterForCurrentSession => true;
        public override bool IsPreview
        {
            get
            {
                // only show this in debug builds
#if DEBUG
                return false;
#else
                return true;
#endif
            }
        }
        protected override bool IsFinalEvent(DaxStudioTraceEventArgs traceEvent)
        {
            return traceEvent.EventClass == DaxStudioTraceEventClass.CommandEnd &&
                   traceEvent.TextData.Contains("<Refresh ");
        }

        public override void ClearAll()
        {
            DebugEvents.Clear();
            NotifyOfPropertyChange(() => CanClearAll);
            NotifyOfPropertyChange(() => CanCopyAll);
            NotifyOfPropertyChange(() => CanExport);
        }


        public bool CanClearAll => DebugEvents.Count > 0;

        public override void OnReset()
        {
            //IsBusy = false;
            Events.Clear();

            ProcessResults();
        }

        public new bool IsBusy => false;

        private EvaluateAndLogEvent _selectedEvent;
        public EvaluateAndLogEvent SelectedEvent { get =>_selectedEvent; 
            set { 
                _selectedEvent = value; 
                NotifyOfPropertyChange(); 
            } 
        }

        public override bool IsCopyAllVisible => true;
        public override bool IsFilterVisible => true;

        public bool CanCopyAll => DebugEvents.Count > 0;

        public override void CopyAll()
        {
            //We need to get the default view as that is where any filtering is done
            ICollectionView view = CollectionViewSource.GetDefaultView(DebugEvents);

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
            _eventAggregator.PublishOnUIThreadAsync(new SendTextToEditor(sb.ToString()));
        }

        public override void CopyResults()
        {
            // not supported by AllQueries
            throw new NotImplementedException();
        }
        public override void CopyEventContent()
        {
            Log.Warning("CopyEventContent not implemented for DebugAndLogTraceViewModel");
            throw new NotImplementedException();
        }

        public override void ClearFilters()
        {
            var vw = GetView() as Views.EvaluateAndLogTraceView;
            if (vw == null) return;
            var controller = DataGridExtensions.GetDataGridFilterQueryController(vw.DebugEvents);
            controller.ClearFilter();
        }

        public void TextDoubleClick()
        {
            TextDoubleClick(SelectedEvent);
        }

        //public override RibbonControlSizeDefinition SizeDefinition { get; } = new RibbonControlSizeDefinition() { Large = RibbonControlSize.Middle, Middle = RibbonControlSize.Middle, Small = RibbonControlSize.Middle };

        #region ISaveState methods
        void ISaveState.Save(string filename)
        {
            string json = GetJson();
            File.WriteAllText(filename + ".debugTrace", json);
        }

        public string GetJson()
        {
            return JsonConvert.SerializeObject(DebugEvents, Formatting.Indented);
        }

        void ISaveState.Load(string filename)
        {
            filename = filename + ".debugTrace";
            if (!File.Exists(filename)) return;

            _eventAggregator.PublishOnUIThreadAsync(new ShowTraceWindowEvent(this));
            string data = File.ReadAllText(filename);
            LoadJson(data);
        }

        public void LoadJson(string data)
        {
            List<EvaluateAndLogEvent> re = JsonConvert.DeserializeObject<List<EvaluateAndLogEvent>>(data);

            _debugEvents.Clear();
            _debugEvents.AddRange(re);
            NotifyOfPropertyChange(() => DebugEvents);
        }

        public void SavePackage(Package package)
        {

            Uri uriTom = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.DebugLog, UriKind.Relative));
            using (TextWriter tw = new StreamWriter(package.CreatePart(uriTom, "application/json", CompressionOption.Maximum).GetStream(), Encoding.UTF8))
            {
                tw.Write(GetJson());
                tw.Close();
            }
        }

        public void LoadPackage(Package package)
        {
            var uri = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.DebugLog, UriKind.Relative));
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
            var vw = this.GetView() as Views.EvaluateAndLogTraceView;
            if (vw == null) return;
            var controller = DataGridExtensions.GetDataGridFilterQueryController(vw.DebugEvents);
            var filters = controller.GetFiltersForColumns();

            var columnFilter = filters.FirstOrDefault(w => w.Key == column);
            if (columnFilter.Key != null)
            {
                columnFilter.Value.QueryString = value;

                controller.SetFiltersForColumns(filters);
            }
        }

        public override bool CanExport => _debugEvents.Count > 0;

        public override string ImageResource => "evaluate_logDrawingImage"; 

        public override void ExportTraceDetails(string filePath)
        {
            File.WriteAllText(filePath, GetJson());
        }

        public void TextDoubleClick(EvaluateAndLogEvent refreshEvent)
        {
            if (refreshEvent == null) return; // it the user clicked on an empty query exit here
            _eventAggregator.PublishOnUIThreadAsync(new SendTextToEditor($"// {refreshEvent.EventClass} - {refreshEvent.EventSubClass}\n{refreshEvent.Text}"));
        }
#endregion

    }

#region Data Objects
    public class EvaluateAndLogEvent
    {
        const string columnSource = "ColumnSource";
        const string shortName = "ShortName";

        //TODO
        private string _text;
        public string Text { get => _text; set { _text = value;
                ParseJson(_text);
            } 
        }
        public DateTime StartTime { get; set; }
        public long Duration { get; set; }
        public DaxStudioTraceEventClass EventClass { get; set; }
        public DaxStudioTraceEventSubclass EventSubClass { get; set; }

        #region "Values parsed from Text property
        public List<string> Inputs { get; set; }
        public List<string> Outputs { get; set; } = new List<string>() { "Value" };
        public System.Data.DataTable Table { get; set; } = new System.Data.DataTable("data");
        public string Expression { get; set; }
        public string Label { get; set; }
        public long RowCount {  get; set; }
        #endregion

        internal void ParseJson(string json)
        {
            var item = JsonConvert.DeserializeObject<EvaluateAndLogItem>(json);

            Label = item.Label;
            Expression = item.Expression;
            var row1input = item.Data[0].Input;
            var row1output = item.Data[0].Output;
            var inputIdx = 0;
            var outputIdx = 0;
            bool isScalar = false;
            foreach (var col in item.Inputs) { 
                var newCol = Table.Columns.Add(col, row1input[inputIdx].GetType());
                newCol.ExtendedProperties.Add(columnSource, "Input");
                inputIdx++;
            }

            if (item.Outputs != null)
            {
                foreach (var col in item.Outputs)
                {
                    var newCol = Table.Columns.Add(col, GetOutputColumnType(row1output , outputIdx));
                    newCol.ExtendedProperties.Add(columnSource, "Output");
                    //newCol.ExtendedProperties.Add(shortName, );
                    outputIdx++;
                }
            }
            else
            {
                var newCol = Table.Columns.Add("Value", row1output[outputIdx].GetType());
                newCol.ExtendedProperties.Add(columnSource, "Output");
                isScalar = true;
            }

            foreach( var data in item.Data)
            {
                if (isScalar) {
                    int colIdx = 0;
                    var row = Table.NewRow();
                    foreach (var input in data.Input)
                    {
                        row[colIdx] = input;
                        colIdx++;
                    }


                        row[colIdx] = data.Output[0];
                        colIdx++;

                    Table.Rows.Add(row);

                }
                else
                {
                    foreach (JArray output in data.Output)
                    {
                        int colIdx = 0;
                        var row = Table.NewRow();
                        foreach (var input in data.Input)
                        {
                            row[colIdx] = input;
                            colIdx++;
                        }

                        for (outputIdx = 0; outputIdx < output.Count; outputIdx++)
                        {


                            row[colIdx] = output[outputIdx];
                            colIdx++;
                        }
                        Table.Rows.Add(row);
                    }
                }
            }
        }

        private Type GetOutputColumnType(List<object> row1output, int outputIdx)
        {
            try
            {
                if (row1output[0] is JArray outputArray) return ((JValue)(outputArray[0].Root.ToList()[outputIdx])).Value?.GetType()??typeof(string);
                return row1output[outputIdx].GetType();
            }
            catch {
                return typeof(string);
            } 
        }
    }

    public class EvaluateAndLogItem
    {
        public string Expression { get; set; }
        public string Label { get; set; }
        public long RowCount { get; set; }
        public List<String> Inputs { get; set; }
        public List<string> Outputs { get; set; }
        public List<EvaluateAndLogData> Data { get; set; }
    }

    public class EvaluateAndLogData
    {
        [JsonConverter(typeof(SingleOrArrayConverter<object>))]
        public List<object> Input {  get; set; }

        [JsonConverter(typeof(SingleOrArrayConverter<object>))]
        public List<object> Output { get; set; }
        public long RowCount { get; set; }  
    }

    public abstract class DebugItem
    {
        public string Name { get; set; }
        public string Message { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public long Duration { get; set; }
        public long CpuDuration { get; set; }
        public long ProgressTotal { get; internal set; }
    }

    public class DebugCommand : DebugItem
    {
        public DebugCommand()
        {

        }
        public string ActivityId { get; set; }
        public string RequestId { get; set; }
        public string Spid { get; set; }

 

 

        private void UpdateDatabase(TraceEvent newEvent, Dictionary<string, string> reference)
        {
            // TODO
        }

        private void UpdateHierarchy(TraceEvent newEvent, Dictionary<string, string> reference)
        {
            // TODO
        }

        private void UpdateRelationship(TraceEvent newEvent, Dictionary<string, string> reference)
        {
            // TODO
        }

        private void UpdateColumn(TraceEvent newEvent, Dictionary<string, string> reference)
        {
            // TODO update column info
        }

 

        public void UpdateItem(TraceEvent newEvent)
        {
            // TODO parse ObjectReference XML
            //      then update
        }

    }



#endregion
}
