using System;
using DaxStudio.Common.Enums;
using DaxStudio.QueryTrace;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DaxStudio.UI.Model
{
    public class TraceEvent
    {
        public TraceEvent() { }
        public TraceEvent(DaxStudioTraceEventArgs traceEvent)
        {
            StartTime = traceEvent.StartTime;
            EndTime = traceEvent.EndTime;
            Username = traceEvent.NTUserName;
            Text = traceEvent.TextData;
            CpuTime = traceEvent.CpuTime;
            Duration = traceEvent.Duration;
            DatabaseName = traceEvent.DatabaseFriendlyName;
            RequestID = traceEvent.RequestID;
            RequestParameters = traceEvent.RequestParameters;
            RequestProperties = traceEvent.RequestProperties;
            ObjectName = traceEvent.ObjectName;
            ObjectPath = traceEvent.ObjectPath;
            ObjectReference = traceEvent.ObjectReference;
            EventClass = traceEvent.EventClass;
            EventSubClass = traceEvent.EventSubclass;
            ProgressTotal = traceEvent.ProgressTotal;
            ActivityID = traceEvent.ActivityId;
            SPID = traceEvent.SPID;                
            SessionId = traceEvent.SessionId;
            IntegerData = traceEvent.IntegerData;
            CurrentTime = traceEvent.CurrentTime;
        }

        public long CpuTime { get; set; }
        public long Duration { get; set; }
        public string Text { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Username { get; set; }
        public string DatabaseName { get; internal set; }
        public string RequestID { get; set; }
        public string ActivityID { get; set; }
        public string SPID { get; set; }
        public string RequestProperties { get; set; }
        public string RequestParameters { get; set; }
        public long ProgressTotal { get; set; }
        public string ObjectName { get; set; }
        public string ObjectPath { get; set; }
        public string ObjectReference { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public DaxStudioTraceEventClass EventClass { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public DaxStudioTraceEventSubclass EventSubClass { get; set; }

        public DateTime CurrentTime { get; set; }
        public string SessionId { get; set; }
        public long IntegerData { get; set; }
    }
}