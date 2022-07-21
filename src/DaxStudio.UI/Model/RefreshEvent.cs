using System;
using DaxStudio.Common.Enums;
using DaxStudio.QueryTrace;


namespace DaxStudio.UI.Model
{
    public class RefreshEvent
    {
        public long CpuDuration { get; set; }
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
        public DaxStudioTraceEventClass EventClass { get; set; }
        public DaxStudioTraceEventSubclass EventSubClass { get; set; }
    }
}