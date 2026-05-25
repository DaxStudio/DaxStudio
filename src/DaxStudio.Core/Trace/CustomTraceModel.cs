using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Text;
using Caliburn.Micro;
using DaxStudio.Common;
using DaxStudio.Common.Enums;
using DaxStudio.Core;
using DaxStudio.Core.Enums;
using DaxStudio.Core.Events;
using DaxStudio.Core.Extensions;
using DaxStudio.Core.Model;
using DaxStudio.Interfaces;
using DaxStudio.QueryTrace;
using Newtonsoft.Json;
using Serilog;

namespace DaxStudio.Core.Trace
{
    // UI-free base for the Custom Trace tool window. The thin
    // DaxStudio.UI.ViewModels.CustomTraceViewModel shell adds the WPF
    // bits (CustomTraceDialogViewModel, CollectionViewSource, the
    // DataGrid filter controller, ISaveState plumbing, SendTextToEditor
    // publishing and the SelectedQuery / TextDoubleClick handlers).
    public abstract class CustomTraceModel : TraceWatcherBaseModel
    {
        protected const int MaxGridEvents = 8;
        protected const string NoFile = "<N/A>";

        protected CustomTraceModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions, IWindowManager windowManager)
            : base(eventAggregator, globalOptions, windowManager)
        {
            _traceEvents = new BindableCollection<TraceEvent>();
        }

        public void SetTraceOutput(CustomTraceOutput selectedTraceOutput)
        {
            switch (selectedTraceOutput)
            {
                case CustomTraceOutput.Grid:
                    OutputEvent = OutputToGrid;
                    IsGridOutput = true;
                    OutputFile = NoFile;
                    break;
                case CustomTraceOutput.File:
                    OutputEvent = OutputToFile;
                    IsFileOutput = true;
                    break;
                case CustomTraceOutput.FileAndGrid:
                    OutputEvent = OutputToGridAndFile;
                    IsFileOutput = true;
                    IsGridOutput = true;
                    break;
            }
            NotifyOfPropertyChange(nameof(IsGridOutput));
            NotifyOfPropertyChange(nameof(IsFileOutput));
        }

        public bool IsFileOutput { get; protected set; }
        public bool IsGridOutput { get; protected set; }

        public CustomTraceTemplate Template { get; set; }

        protected override List<DaxStudioTraceEventClass> GetMonitoredEvents()
        {
            return Template.Events;
        }

        public Action<TraceEvent> OutputEvent { get; set; }

        public void OutputToGrid(TraceEvent @event)
        {
            _traceEvents.Add(@event);
            if (OutputEvent == OutputToGridAndFile)
            {
                while (_traceEvents.Count > MaxGridEvents)
                {
                    _traceEvents.RemoveAt(0);
                }
            }
            NotifyOfPropertyChange(nameof(TraceEvents));
        }

        public void OutputToFile(TraceEvent @event)
        {
            using (var textWriter = new StreamWriter(OutputFile, append: true))
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
                Log.Error(ex, Constants.LogMessageTemplate, GetType().Name, nameof(ProcessSingleEvent), ex.Message);
                _eventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"The following error occurred while processing trace events:\n{ex.Message}"));
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
                Events.TryDequeue(out var _);
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
            Events.Clear();
            ProcessResults();
        }

        public new bool IsBusy => false;

        public override bool IsCopyAllVisible => true;
        public override bool IsFilterVisible => true;

        public bool CanCopyAll => _traceEvents.Count > 0;

        public override void CopyResults()
        {
            // not supported by CustomTrace
            throw new NotImplementedException();
        }

        public override void CopyEventContent()
        {
            Log.Warning("CopyEventContent not implemented for CustomTraceModel");
            throw new NotImplementedException();
        }

        public string GetJson()
        {
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

        public void LoadJson(string data)
        {
            var reader = new StringReader(data);
            string line;
            TraceEvents.Clear();
            TraceEvents.IsNotifying = false;
            do
            {
                line = reader.ReadLine();
                if (line != null)
                {
                    TraceEvents.Add(JsonConvert.DeserializeObject<TraceEvent>(line));
                }

            } while (line != null);

            if (TraceEvents.Count > 0)
            {
                EventCount = TraceEvents.Count;
                OutputFile = NoFile;
                OutputEvent = OutputToGrid;
                IsGridOutput = true;
                NotifyOfPropertyChange(nameof(IsFileOutput));
                NotifyOfPropertyChange(nameof(IsGridOutput));
            }
            TraceEvents.IsNotifying = true;
            TraceEvents.Refresh();
        }

        public void SavePackage(Package package)
        {
            var uri = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.AllQueries, UriKind.Relative));
            using (TextWriter tw = new StreamWriter(package.CreatePart(uri, "application/json", CompressionOption.Maximum).GetStream(), Encoding.UTF8))
            {
                tw.Write(GetJson());
                tw.Close();
            }
        }

        public override bool CanExport => _traceEvents.Count > 0;

        // TODO - change to custom trace image
        public override string ImageResource => "custom_traceDrawingImage";

        private int _eventCount;
        public int EventCount
        {
            get => _eventCount;
            protected set
            {
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
    }
}
