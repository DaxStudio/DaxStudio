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

namespace DaxStudio.UI.ViewModels
{

    public class RefreshTraceViewModel
        : TraceWatcherBaseViewModel,
        ISaveState,
        IViewAware

    {

        [ImportingConstructor]
        public RefreshTraceViewModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions, IWindowManager windowManager) : base(eventAggregator, globalOptions,windowManager)
        {
            _refreshEvents = new BindableCollection<TraceEvent>();
            Commands = new Dictionary<string, RefreshCommand>();

        }

        protected override List<DaxStudioTraceEventClass> GetMonitoredEvents()
        {
            return new List<DaxStudioTraceEventClass>
                { 
                  DaxStudioTraceEventClass.CommandBegin,
                  DaxStudioTraceEventClass.CommandEnd,
                  DaxStudioTraceEventClass.JobGraph,
                  DaxStudioTraceEventClass.ProgressReportBegin,
                  //DaxStudioTraceEventClass.ProgressReportCurrent,
                  DaxStudioTraceEventClass.ProgressReportEnd,
                  DaxStudioTraceEventClass.ProgressReportError,
                  DaxStudioTraceEventClass.Error
            };
        }


        protected override void ProcessSingleEvent(DaxStudioTraceEventArgs traceEvent)
        {
            if (IsPaused) return;

            base.ProcessSingleEvent(traceEvent);
            var newEvent = new TraceEvent(traceEvent);
        
            try
            {

                RefreshEvents.Add(newEvent);

                // TODO - fix capturing progress events for treeview
                AddEventToCommand(newEvent);
            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(RefreshTraceViewModel), nameof(ProcessSingleEvent), ex.Message);
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"The following error occurred while processing trace events:\n{ex.Message}"));
            }
        }

        public Dictionary<string, RefreshCommand> Commands { get; set; }
        private void AddEventToCommand(TraceEvent newEvent)
        {
            RefreshCommand cmd;
            var reqId = newEvent.RequestID;

            switch (newEvent.EventClass)
            {
                case DaxStudioTraceEventClass.CommandBegin:
                    if (!newEvent.Text.Contains("<Refresh")) return;
                    
                    if (string.IsNullOrEmpty(reqId))
                    {
                        cmd = Commands.Values.FirstOrDefault(c => c.Spid == newEvent.SPID && c.RequestId != null);
                        reqId = cmd?.RequestId;
                    }

                    Commands.Add(reqId, new RefreshCommand() { Message = newEvent.Text, 
                        StartDateTime = newEvent.StartTime, 
                        ActivityId = newEvent.ActivityID ?? string.Empty,
                        RequestId = newEvent.RequestID ?? string.Empty,
                        Spid = newEvent.SPID });

                    break;
                case DaxStudioTraceEventClass.CommandEnd:
                    if (!newEvent.Text.Contains("<Refresh")) return;

                    cmd = Commands[newEvent.RequestID];
           
                    cmd.EndDateTime = newEvent.EndTime;                    

                    cmd.Duration = newEvent.Duration;

                    break;
                case DaxStudioTraceEventClass.ProgressReportBegin:
                    if (string.IsNullOrEmpty(newEvent.RequestID))
                    {

                        cmd = Commands.Values.FirstOrDefault(c => c.Spid == newEvent.SPID);
                    }
                    else
                    {
                        if (Commands.ContainsKey(reqId))
                        {
                            cmd = Commands[newEvent.RequestID];
                        }
                        else
                        {
                            cmd = new RefreshCommand()
                            {
                                ActivityId = newEvent.ActivityID ?? string.Empty,
                                RequestId = newEvent.RequestID ?? string.Empty,
                                Spid = newEvent.SPID
                            }; 
                            Commands.Add(reqId, cmd);
                        }
                    }

                    cmd?.CreateItem(newEvent);
                    break;
                case DaxStudioTraceEventClass.ProgressReportCurrent:
                case DaxStudioTraceEventClass.ProgressReportEnd:
                case DaxStudioTraceEventClass.ProgressReportError:
                    // TODO cmd.UpdateItem(newEvent);
                    break;
            }

            // if this is a CommandBegin for a Refresh we should create a new session

            // If this is a progress event get the event by activityid, if activityid is null use the spid

            // if this is a CommandEnd for a Refresh we should mark the session as completed

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


            NotifyOfPropertyChange(() => RefreshEvents);
            NotifyOfPropertyChange(() => CanClearAll);
            NotifyOfPropertyChange(() => CanCopyAll);
            NotifyOfPropertyChange(() => CanExport);
        }


        private readonly BindableCollection<TraceEvent> _refreshEvents;

        public override bool CanHide => true;
        public override string ContentId => "refresh-trace";
        public IObservableCollection<TraceEvent> RefreshEvents => _refreshEvents;

        public string DefaultQueryFilter => "cat";

        // IToolWindow interface
        public override string Title => "Refresh Trace";
        public override string TraceSuffix => "refresh";
        public override string KeyTip => "RT";
        public override string ToolTipText => "Runs a server trace to record data refresh details";
        public override int SortOrder => 40;
        public override bool FilterForCurrentSession => false;
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
            return false; // this trace should keep running until manually stopped
        }

        protected override bool ShouldStartCapturing(DaxStudioTraceEventArgs traceEvent)
        {
            // we should wait for a CommandBegin with a Refresh before capturing begins
            return traceEvent.EventClass == DaxStudioTraceEventClass.CommandBegin &&
                   traceEvent.TextData.Contains("<Refresh ");
        }

        public override void ClearAll()
        {
            RefreshEvents.Clear();
            NotifyOfPropertyChange(() => CanClearAll);
            NotifyOfPropertyChange(() => CanCopyAll);
            NotifyOfPropertyChange(() => CanExport);
        }


        public bool CanClearAll => RefreshEvents.Count > 0;

        public override void OnReset()
        {
            //IsBusy = false;
            Events.Clear();
            Commands.Clear();
            ProcessResults();
        }

        public new bool IsBusy => false;

        public TraceEvent SelectedQuery { get; set; }

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
            _eventAggregator.PublishOnUIThreadAsync(new SendTextToEditor(sb.ToString()));
        }

        public override void CopyResults()
        {
            // not supported by AllQueries
            throw new NotImplementedException();
        }

        public override void CopyEventContent()
        {
            Log.Warning("CopyEventContent not implemented for RefreshTraceViewModel");
            throw new NotImplementedException();
        }

        public override void ClearFilters()
        {
            var vw = GetView() as Views.RefreshTraceView;
            if (vw == null) return;
            var controller = DataGridExtensions.GetDataGridFilterQueryController(vw.RefreshEvents);
            controller.ClearFilter();
        }

        public void TextDoubleClick()
        {
            TextDoubleClick(SelectedQuery);
        }

#region ISaveState methods
        void ISaveState.Save(string filename)
        {
            string json = GetJson();
            File.WriteAllText(filename + ".refreshTrace", json);
        }

        public string GetJson()
        {
            return JsonConvert.SerializeObject(RefreshEvents, Formatting.Indented);
        }

        void ISaveState.Load(string filename)
        {
            filename = filename + ".refreshTrace";
            if (!File.Exists(filename)) return;

            _eventAggregator.PublishOnUIThreadAsync(new ShowTraceWindowEvent(this));
            string data = File.ReadAllText(filename);
            LoadJson(data);
        }

        public void LoadJson(string data)
        {
            List<TraceEvent> re = JsonConvert.DeserializeObject<List<TraceEvent>>(data);

            _refreshEvents.Clear();
            _refreshEvents.AddRange(re);
            NotifyOfPropertyChange(() => RefreshEvents);
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
            var uri = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.RefreshTrace, UriKind.Relative));
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

        public override bool CanExport => _refreshEvents.Count > 0;

        public override string ImageResource => "refresh_traceDrawingImage";

        public override void ExportTraceDetails(string filePath)
        {
            File.WriteAllText(filePath, GetJson());
        }

        public void TextDoubleClick(TraceEvent refreshEvent)
        {
            if (refreshEvent == null) return; // it the user clicked on an empty query exit here
            _eventAggregator.PublishOnUIThreadAsync(new SendTextToEditor($"// {refreshEvent.EventClass} - {refreshEvent.EventSubClass}\n{refreshEvent.Text}"));
        }
#endregion

    }

#region Data Objects

    public enum RefreshStatus
    {
        Waiting,
        Successful,
        Failed,
        InProgress
    }

    public abstract class RefreshItem
    {
        public string Name { get; set; }
        public RefreshStatus Status { get; set; }
        public string Message { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public long Duration { get; set; }
        public long CpuTime { get; set; }
        public long ProgressTotal { get; internal set; }
    }

    public class RefreshCommand : RefreshItem
    {
        public RefreshCommand()
        {
            Tables = new Dictionary<string, RefreshTable>();
            Relationships = new Dictionary<string, RefreshRelationship>();
        }
        public string ActivityId { get; set; }
        public string RequestId { get; set; }
        public string Spid { get; set; }

        public Dictionary<string, RefreshTable> Tables { get; set; }
        public Dictionary<string, RefreshRelationship> Relationships { get; set; }

        public void CreateItem(TraceEvent newEvent)
        {
            // TODO parse ObjectReference XML
            //      then create
            var reference = ParseObjectReference(newEvent.ObjectReference);

            UpdateTable(newEvent, reference);

            // Partition
            // AttributeHierarchy / Column
            // Hierarchy
            // Relationship

        }

        private void UpdateTable(TraceEvent newEvent, Dictionary<string, string> reference)
        {
            string tableName = string.Empty;
            reference.TryGetValue("Table", out tableName);
            if (tableName.IsNullOrEmpty()) return; // we must be at the database level so exit here

            RefreshTable table;
            Tables.TryGetValue(tableName, out table);
            if (table == null)
            {
                table = new RefreshTable { Name = reference["Table"] };
                Tables.Add(table.Name, table);
            }
            else
            {
                if (reference.ContainsKey("Partition")) UpdatePartition(newEvent, reference, table);
                else if (reference.ContainsKey("AttributeHierarchy")) UpdateColumn(newEvent, reference);
                else if (reference.ContainsKey("Relationship")) UpdateRelationship(newEvent, reference);
                else if (reference.ContainsKey("Hierarchy")) UpdateHierarchy(newEvent, reference);
                else UpdateDatabase(newEvent, reference);

                // update status of current table
                if (table.Partitions.Values.Any(p => p.Status == RefreshStatus.InProgress)
                    || table.Columns.Values.Any(c => c.Status == RefreshStatus.InProgress))
                    table.Status = RefreshStatus.InProgress;
                if (table.Partitions.Values.Any(p => p.Status == RefreshStatus.Failed)
                    || table.Columns.Values.Any(c => c.Status == RefreshStatus.Failed))
                    table.Status = RefreshStatus.Failed;
                if (table.Partitions.Values.All(p => p.Status == RefreshStatus.Successful)
                    && table.Columns.Values.All(c => c.Status == RefreshStatus.Successful))
                    table.Status = RefreshStatus.Successful;
                else
                    table.Status = RefreshStatus.Waiting;
            }
        }

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

        private void UpdatePartition(TraceEvent newEvent, Dictionary<string, string> reference, RefreshTable table)
        {

            table.Partitions.TryGetValue(reference["Partition"], out var partition);
            if (partition == null)
                partition = new RefreshPartition() { Name = reference["Partition"] };
            partition.Message = newEvent.Text;
            switch (newEvent.EventClass)
            {
                case DaxStudioTraceEventClass.ProgressReportBegin:
                case DaxStudioTraceEventClass.ProgressReportCurrent:
                    partition.Status = RefreshStatus.InProgress;
                    break;
                case DaxStudioTraceEventClass.ProgressReportEnd:
                    partition.Status = RefreshStatus.Successful;
                    partition.Duration = newEvent.Duration;
                    partition.CpuTime = newEvent.CpuTime;
                    partition.ProgressTotal = newEvent.ProgressTotal;
                    break;
                case DaxStudioTraceEventClass.ProgressReportError:
                    partition.Status = RefreshStatus.Failed;

                    break;
            }

        }

        public void UpdateItem(TraceEvent newEvent)
        {
            // TODO parse ObjectReference XML
            //      then update
        }

        public static Dictionary<string, string> ParseObjectReference(string xml)
        {
            if (xml == null) return new Dictionary<string, string>();

            XmlReader reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings() { XmlResolver = null} );
            var result = new Dictionary<string, string>();
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (reader.Name == "Object") break; // don't add the outer <Object> tag to the dictionary
                        var key = reader.Name;
                        var val = reader.ReadString();
                        result.Add(key, val);
                        break;
                    default:
                        // do nothing
                        break;
                }
            }

            return result;
        }
    }

    public class RefreshTable : RefreshItem
    {
        public RefreshTable()
        {
            Columns = new Dictionary<string, RefreshColumn>();
            Partitions = new Dictionary<string, RefreshPartition>();
        }
        public Dictionary<string, RefreshPartition> Partitions { get; private set; }
        public Dictionary<string, RefreshColumn> Columns { get; private set; }
    }

    public class RefreshRelationship : RefreshItem
    { }

    public class RefreshPartition : RefreshItem
    {

    }
    public class RefreshColumn : RefreshItem
    { }

#endregion
}
