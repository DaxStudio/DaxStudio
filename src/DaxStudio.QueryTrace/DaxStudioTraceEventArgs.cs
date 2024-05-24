using DaxStudio.Common.Enums;
using Microsoft.AnalysisServices;
using System;
using System.Globalization;

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
            ActivityId = e[TraceColumn.ActivityID];
            RequestId = e[TraceColumn.RequestID];
            SessionId = e.SessionID;
            CurrentTime = e.CurrentTime;

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
                    CpuTime = e.Duration; // CPU duration is the same as Duration in DirectQuery mode
                    StartTime = e.StartTime;
                    break;
                case TraceEventClass.VertiPaqSEQueryEnd:
                    StartTime = e.StartTime;
                    EndTime = e.EndTime;    
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

                    string s3 = e[TraceColumn.StartTime] ?? e[TraceColumn.CurrentTime] ?? string.Empty;
                    DateTime.TryParse(s3, CultureInfo.CurrentUICulture, DateTimeStyles.AssumeUniversal, out var startTime3);
                    StartTime = startTime3;
                    NTUserName = e.NTUserName;
                    SPID = e.Spid;
                    break;
                case TraceEventClass.ProgressReportBegin:
                    string s = e[TraceColumn.StartTime] ?? e[TraceColumn.CurrentTime] ?? string.Empty;
                    DateTime.TryParse(s, CultureInfo.CurrentUICulture, DateTimeStyles.AssumeUniversal, out var startTime);
                    StartTime = startTime;
                    NTUserName = e.NTUserName;
                    SPID = e.Spid;
                    ObjectName = e.ObjectName;
                    ObjectPath = e.ObjectPath;
                    ObjectReference = e.ObjectReference;
                    break;
                case TraceEventClass.ProgressReportCurrent:
                    string s2 = e[TraceColumn.StartTime] ?? e[TraceColumn.CurrentTime] ?? string.Empty;
                    DateTime.TryParse(s2, CultureInfo.CurrentUICulture, DateTimeStyles.AssumeUniversal, out var startTime2);
                    StartTime = startTime2;
                    NTUserName = e.NTUserName;
                    ObjectName = e.ObjectName;
                    ObjectPath = e.ObjectPath;
                    ObjectReference = e.ObjectReference;
                    SPID = e.Spid;
                                       

                    try
                    {
                        IntegerData = e.IntegerData;
                    }
                    catch 
                    {
                        // suppress all errors
                    }

                    try
                    {
                        ProgressTotal = e.ProgressTotal;
                    }
                    catch
                    {
                        // suppress all errors
                    }
                    break;
                case TraceEventClass.ProgressReportEnd:
                    StartTime = e.StartTime;
                    EndTime = e.EndTime;

                    try { CpuTime = e.CpuTime; } catch { }
                    
                    Duration = e.Duration;
                    NTUserName = e.NTUserName;
                    //ProgressTotal = e.ProgressTotal;
                    ObjectName = e.ObjectName;
                    ObjectPath = e.ObjectPath;
                    ObjectReference = e.ObjectReference;
                    SPID = e.Spid;                    
                    ObjectId = e.ObjectID;
                    
                    try
                    {
                        IntegerData = e.IntegerData;
                    }
                    catch
                    {
                        // suppress all errors
                    }

                    break;
                case TraceEventClass.DiscoverBegin:
                case TraceEventClass.VertiPaqSEQueryBegin:
                case TraceEventClass.DAXQueryPlan:
                case TraceEventClass.JobGraph:
                case TraceEventClass.DAXEvaluationLog:
                case TraceEventClass.ExecutionMetrics:
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
                    EndTime = e.EndTime;
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
            DatabaseName = "<Power Pivot>";
            DatabaseFriendlyName = xlsxFile;
            StartTime = startTime;
        }
        
        // HACK: properties must have public setters so that we can deserialize from JSON when tracing against PowerPivot
        public string EventClassName { 
            get => _eventClassName;
            set { _eventClassName = value;
            Enum.TryParse<DaxStudioTraceEventClass>(_eventClassName, out _eventClass);
            } 
        }
        public string EventSubclassName {
            get => _eventSubclassName;
            set
            {
                _eventSubclassName = value;
                Enum.TryParse<DaxStudioTraceEventSubclass>(_eventSubclassName, out _eventSubclass);
            }
        }

        public string TextData { get; set; }
        private long _duration = 0;
        public long Duration { get => _duration;
            set { _duration = value;
                NetParallelDuration = value; // default this to the same as duration
            } 
        }
        // Records any additional, non-overlapped duration
        public long NetParallelDuration { get; set; }
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

        public string SPID { get; set; }
        public string ObjectName { get; set; }
        public string ObjectPath { get; set; }
        public string ObjectReference { get; set; }
        public long ProgressTotal { get; set; }
        public string ActivityId { get; set; }
        public string RequestId { get; private set; }

        public DateTime CurrentTime { get; set; }
        public long IntegerData { get; set; }
        public string SessionId { get; set; }
        public string ObjectType { get; set; }
        public string ObjectId { get; set; }

    }
}
