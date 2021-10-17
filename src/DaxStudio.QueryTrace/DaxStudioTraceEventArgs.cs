using Microsoft.AnalysisServices;
using System;

namespace DaxStudio.QueryTrace
{
    public class DaxStudioTraceEventArgs
    {
        private string _eventClassName;
        private string _eventSubclassName;
        private DaxStudioTraceEventClass _eventClass = DaxStudioTraceEventClass.NotAvailable;
        private DaxStudioTraceEventSubclass _eventSubclass = DaxStudioTraceEventSubclass.NotAvailable;
        
        public DaxStudioTraceEventArgs(Microsoft.AnalysisServices.TraceEventArgs e, string powerBiFileName)
        {
            StartTime = DateTime.Now;
            EventClassName = e.EventClass.ToString();
            EventSubclassName = e.EventSubclass.ToString();
            Enum.TryParse<DaxStudioTraceEventClass>(EventClassName, out _eventClass);
            Enum.TryParse<DaxStudioTraceEventSubclass>(EventSubclassName, out _eventSubclass);

            TextData = e.TextData;
            RequestID = e[TraceColumn.RequestID];
            DatabaseName = e.DatabaseName;
            DatabaseFriendlyName = !string.IsNullOrEmpty(powerBiFileName)? powerBiFileName : DatabaseName;
            
            switch (e.EventClass)
            {
                case TraceEventClass.QueryBegin:
                    RequestProperties = e.RequestProperties;
                    RequestParameters = e.RequestParameters;
                    NTUserName = e.NTUserName;
                    StartTime = e.StartTime;
                    break;
                case TraceEventClass.QueryEnd:
                    Duration = e.Duration;
                    StartTime = e.StartTime;
                    NTUserName = e.NTUserName;
                    EndTime = e.EndTime;
                    CpuTime = e.CpuTime;
                    break;
                case TraceEventClass.DirectQueryEnd:
                    Duration = e.Duration;
                    EndTime = e.EndTime;
                    CpuTime = e.CpuTime;
                    StartTime = e.StartTime;
                    break;
                case TraceEventClass.VertiPaqSEQueryEnd:
                    StartTime = e.StartTime;
                    CpuTime = e.CpuTime;
                    Duration = e.Duration;
                    NTUserName = e.NTUserName;
                    break;
                case TraceEventClass.AggregateTableRewriteQuery:
                case TraceEventClass.VertiPaqSEQueryCacheMatch:
                    StartTime = e.CurrentTime;
                    NTUserName = e.NTUserName;
                    break;
                case TraceEventClass.CommandBegin:
                    string s = e[TraceColumn.StartTime] ?? e[TraceColumn.CurrentTime] ?? string.Empty;
                    DateTime.TryParse(s, out var startTime);
                    StartTime = startTime;
                    NTUserName = e.NTUserName;
                    break;
                case TraceEventClass.DiscoverBegin:
                case TraceEventClass.VertiPaqSEQueryBegin:
                case TraceEventClass.DAXQueryPlan:
                    // no additional properties captured, the plan is stored in the text field
                    break;
                case TraceEventClass.Error:
                    StartTime = e.StartTime;
                    NTUserName = e.NTUserName;
                    break;
                case TraceEventClass.CommandEnd:
                    // no additional properties captured, the plan is stored in the text field
                    Duration = e.Duration;
                    StartTime = e.CurrentTime;
                    NTUserName = e.NTUserName;
                    break;
                default:
                    throw new ArgumentException($"No mapping for the event class {e.EventClass.ToString()} was found");

            }
            
            //if (e.EventClass != TraceEventClass.CommandBegin)
            //{
            //    // not all events have CpuTime
            //    try
            //    {
            //        CpuTime = e.CpuTime;
            //    }
            //    catch (ArgumentNullException)
            //    {
            //        CpuTime = 0;
            //    }
            //    // not all events have a duration
            //    try
            //    {
            //        Duration = e.Duration;
            //    }
            //    catch (ArgumentNullException)
            //    {
            //        Duration = 0;
            //    }
            //}

            //if (e.EventClass == TraceEventClass.QueryBegin)
            //{
            //    RequestParameters = e.RequestParameters;
            //    RequestProperties = e.RequestProperties;
            //}

            //if (e.NTUserName != null)
            //    NTUserName = e.NTUserName;

            //if (e.DatabaseName != null)
            //{
            //    DatabaseName = e.DatabaseName;
            //    if (!string.IsNullOrEmpty(powerBIFileName)) DatabaseFriendlyName = powerBIFileName;
            //    else DatabaseFriendlyName = DatabaseName;
            //}
            //try
            //{
            //    StartTime = e.CurrentTime;
            //    StartTime = e.StartTime;
            //}
            //catch (NullReferenceException)
            //{
                
            //}

            //try
            //{
            //    RequestID = e[TraceColumn.RequestID];
            //}
            //catch 
            //{ }
        }

        // This default constructor is required to allow deserializing from JSON when tracing PowerPivot
        public DaxStudioTraceEventArgs() { }

        // This constructor is only called from Excel
        public DaxStudioTraceEventArgs(string eventClass, string eventSubclass, long duration, long cpuTime, string textData, string xlsxFile, DateTime startTime) {
            CpuTime = cpuTime;
            Duration = duration;
            TextData = textData;
            EventClassName = eventClass;
            EventSubclassName = eventSubclass;
            NTUserName = "n/a";
            DatabaseName = "<PowerPivot>";
            DatabaseFriendlyName = xlsxFile;
            StartTime = startTime;
        }
        
        // HACK: properties must have public setters so that we can deserialize from JSON when tracing against PowerPivot
        public string EventClassName { 
            get => _eventClassName;
            set { _eventClassName = value;
            Enum.TryParse<DaxStudioTraceEventClass>(_eventClassName, out _eventClass);
            } }
        public string EventSubclassName {
            get => _eventSubclassName;
            set
            {
                _eventSubclassName = value;
                Enum.TryParse<DaxStudioTraceEventSubclass>(_eventSubclassName, out _eventSubclass);
            }
        }

        public string TextData { get; set; }
        public long Duration { get; set; }
        public long CpuTime { get; set; }
        public double? CpuFactor
        {
            get { return (Duration == 0 || CpuTime == 0) ? (double?)null : (double)CpuTime / (double)Duration; }
        }

        public bool InternalBatchEvent { get; set; }

        public DaxStudioTraceEventClass EventClass => _eventClass;
        public DaxStudioTraceEventSubclass EventSubclass => _eventSubclass;
        // ReSharper disable once InconsistentNaming
        public string NTUserName { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime StartTime { get; set; }
        public string DatabaseName { get; set; }

        public string DatabaseFriendlyName { get; set; }
        // ReSharper disable once InconsistentNaming
        public string RequestID { get; set; }
        public string RequestProperties { get; set; }
        public string RequestParameters { get; set; }
    }
}
