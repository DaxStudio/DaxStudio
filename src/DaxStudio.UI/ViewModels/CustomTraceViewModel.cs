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
using DaxStudio.Interfaces;
using DaxStudio.UI.Utils;
using Serilog;
using DaxStudio.Common.Enums;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Enums;

namespace DaxStudio.UI.ViewModels
{
    public class CustomTraceViewModel
        : TraceWatcherBaseViewModel,
        ISaveState,
        IViewAware

    {
        private const int maxEvents = 8;
        private const string NoFile = "<N/A>";

        [ImportingConstructor]
        public CustomTraceViewModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions, IWindowManager windowManager) : base(eventAggregator, globalOptions,windowManager)
        {
            _traceEvents = new BindableCollection<TraceEvent>();
        }

        public override bool ShouldStartTrace()
        {
            var dialog = new CustomTraceDialogViewModel(_globalOptions);
            WindowManager.ShowDialogBoxAsync(dialog).Wait();

            // if the dialog result is not OK then exit here
            if (dialog.Result != DialogResult.OK) return false;
            
            // set the template
            Template = dialog.SelectedTraceTemplate;

            // set the trace output
            SetTraceOutput(dialog.SelectedTraceOutput);
       
            OutputFile= dialog.OutputFile;
            EventCount = 0;
            return true;
        }

        public void SetTraceOutput(CustomTraceOutput selectedTraceOutput)
        {
            switch (selectedTraceOutput)
            {
                case CustomTraceOutput.Grid:
                    OutputEvent = OutputToGrid;
                    IsGridOutput= true;
                    OutputFile = NoFile;
                    break;
                case CustomTraceOutput.File:
                    OutputEvent = OutputToFile;
                    IsFileOutput = true;
                    break; 
                case CustomTraceOutput.FileAndGrid:
                    OutputEvent = OutputToGridAndFile;
                    IsFileOutput = true;
                    IsGridOutput= true;
                    break;
            }
            NotifyOfPropertyChange(nameof(IsGridOutput));
            NotifyOfPropertyChange(nameof(IsFileOutput));
        }

        public bool IsFileOutput { get; private set; }
        public bool IsGridOutput { get; private set; }      

        public CustomTraceTemplate Template { get; set; }

        protected override List<DaxStudioTraceEventClass> GetMonitoredEvents()
        {
            return Template.Events;
        }

        public Action<TraceEvent> OutputEvent { get; set; }

        public void OutputToGrid(TraceEvent @event) {
            _traceEvents.Add(@event);
            if (OutputEvent == OutputToGridAndFile )
            {
                while (_traceEvents.Count > maxEvents)
                {
                    _traceEvents.RemoveAt(0);
                }
            }
            NotifyOfPropertyChange(nameof(TraceEvents));
        }

        public void OutputToFile(TraceEvent @event)
        {
            // write event to file
            using (var textWriter = new StreamWriter(OutputFile,append:true))
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                var ser = new JsonSerializer();
                ser.Serialize(jsonWriter, @event);
                jsonWriter.WriteRaw("\n");
            }
        }

        public void OutputToGridAndFile(TraceEvent @event)
        { 
            OutputToGrid(@event); 
            OutputToFile(@event);
        }

        private string _outputFile = NoFile;
        public string OutputFile
        {
            get => _outputFile;
            set
            {
                _outputFile = value;
                NotifyOfPropertyChange();
            }
        }
        protected override void ProcessSingleEvent(DaxStudioTraceEventArgs traceEvent)
        {
            if (IsPaused) return;

            base.ProcessSingleEvent(traceEvent);
            var newEvent = new TraceEvent(traceEvent);
            EventCount++;
            try
            {
                OutputEvent(newEvent);
            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(CustomTraceViewModel), nameof(ProcessSingleEvent), ex.Message);
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"The following error occurred while processing trace events:\n{ex.Message}"));
            }
        }

        // This method is called after the WaitForEvent is seen (usually the QueryEnd event)
        // This is where you can do any processing of the events before displaying them to the UI
        protected override void ProcessResults()
        {

            if (IsPaused) return; // exit here if we are paused

            if (Events == null) return;

            // todo summarize events
            while (!Events.IsEmpty)
            {
                Events.TryDequeue(out var traceEvent);
                // todo - produce summary
            }

            Events.Clear();


            NotifyOfPropertyChange(() => TraceEvents);
            NotifyOfPropertyChange(() => CanClearAll);
            NotifyOfPropertyChange(() => CanCopyAll);
            NotifyOfPropertyChange(() => CanExport);
        }


        private readonly BindableCollection<TraceEvent> _traceEvents;

        public override bool CanHide => true;
        public override string ContentId => "custom-trace";
        public BindableCollection<TraceEvent> TraceEvents => _traceEvents;

        // IToolWindow interface
        public override string Title => "Custom Trace";
        public override string TraceSuffix => "custom";
        public override string KeyTip => "CT";
        public override string ToolTipText => "Runs a custom server trace to record events from the server";
        public override int SortOrder => 50;
        public override bool FilterForCurrentSession => Template.FilterForCurrentSession;
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


        public override void ClearAll()
        {
            _traceEvents.Clear();
            NotifyOfPropertyChange(nameof(TraceEvents));
            NotifyOfPropertyChange(nameof(CanClearAll));
            NotifyOfPropertyChange(nameof(CanCopyAll));
            NotifyOfPropertyChange(nameof(CanExport));
        }


        public bool CanClearAll => _traceEvents.Count > 0;

        public override void OnReset()
        {
            //IsBusy = false;
            Events.Clear();
            ProcessResults();
        }

        public new bool IsBusy => false;

        public TraceEvent SelectedQuery { get; set; }

        public override bool IsCopyAllVisible => true;
        public override bool IsFilterVisible => true;

        public bool CanCopyAll => _traceEvents.Count > 0;

        public override void CopyAll()
        {
            //We need to get the default view as that is where any filtering is done
            ICollectionView view = CollectionViewSource.GetDefaultView(TraceEvents);

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
            Log.Warning("CopyEventContent not implemented for CustomTraceViewModel");
            throw new NotImplementedException();
        }

        public override void ClearFilters()
        {
            var vw = GetView() as Views.CustomTraceView;
            if (vw == null) return;
            var controller = DataGridExtensions.GetDataGridFilterQueryController(vw.TraceEvents);
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
            File.WriteAllText(filename + ".customTrace", json);
        }

        public string GetJson()
        {
            // write event to file
            var sb = new StringBuilder();
            using (var textWriter = new StringWriter(sb))
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                foreach (var @event in TraceEvents)
                {
                    var ser = new JsonSerializer();
                    ser.Serialize(jsonWriter, @event);
                    jsonWriter.WriteRaw("\n");
                }
            }

            return sb.ToString();
        }

        void ISaveState.Load(string filename)
        {
            filename = filename + ".customTrace";
            if (!File.Exists(filename)) return;

            _eventAggregator.PublishOnUIThreadAsync(new ShowTraceWindowEvent(this));
            string data = File.ReadAllText(filename);
            LoadJson(data);
        }

        public void LoadJson(string data)
        {
            StringReader reader = new StringReader(data);
            var line = string.Empty;
            TraceEvents.Clear();
            TraceEvents.IsNotifying = false;
            do {
                line = reader.ReadLine();
                if (line != null)
                {
                    TraceEvents.Add(JsonConvert.DeserializeObject<TraceEvent>(line));
                }

            } while (line != null);

            if( TraceEvents.Count> 0 )
            {
                EventCount = TraceEvents.Count;
                OutputFile = NoFile;
                OutputEvent = OutputToGrid;
                IsGridOutput= true;
                NotifyOfPropertyChange(nameof(IsFileOutput));
                NotifyOfPropertyChange(nameof(IsGridOutput));
            }
            TraceEvents.IsNotifying = true;
            TraceEvents.Refresh();
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
            var uri = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.CustomTrace, UriKind.Relative));
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

        public override bool CanExport => _traceEvents.Count > 0;

        // TODO - change to custom trace image
        public override string ImageResource => "custom_traceDrawingImage";
        
        private int _eventCount;
        public int EventCount { 
            get => _eventCount; 
            private set { 
                _eventCount = value; 
                NotifyOfPropertyChange(); 
            } 
        }

        public override void ExportTraceDetails(string filePath)
        {
            // output trace events as json lines format
            using (var textWriter = new StreamWriter(filePath, append: false))
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                var ser = new JsonSerializer();
                foreach (var @event in TraceEvents)
                {
                    ser.Serialize(jsonWriter, @event);
                    jsonWriter.WriteRaw("\n");
                }
            }
        }

        public void TextDoubleClick(TraceEvent refreshEvent)
        {
            if (refreshEvent == null) return; // if the user clicked on an empty query exit here
            _eventAggregator.PublishOnUIThreadAsync(new SendTextToEditor($"// {refreshEvent.EventClass} - {refreshEvent.EventSubClass}\n{refreshEvent.Text}"));
        }
#endregion

    }

}
