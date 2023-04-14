using DaxStudio.Common.Enums;
using Microsoft.AnalysisServices;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Documents;
using Group = System.Text.RegularExpressions.Group;

namespace DaxStudio.QueryTrace
{
    public partial class DaxStudioTraceEventArgs
    {
        private string _eventClassName;
        private string _eventSubclassName;
        private DaxStudioTraceEventClass _eventClass = DaxStudioTraceEventClass.NotAvailable;
        private DaxStudioTraceEventSubclass _eventSubclass = DaxStudioTraceEventSubclass.NotAvailable;
        private static Regex textDataRegex = new Regex("(?:TableTMID=')(?<TableID>\\d+)(?:')|(?:PartitionTMID=')(?<PartitionID>\\d+)(?:')|(?:MashupCPUTime:\\s+)(?<MashupCPUTime>\\d+)(?:\\s+ms)|(?:MashupPeakMemory:\\s+)(?<MashupPeakMemory>\\d+)(?:\\s+KB)");

        public static Dictionary<TraceColumn, Action<DaxStudioTraceEventArgs, TraceEventArgs>> ColumnMap = new Dictionary<TraceColumn, Action<DaxStudioTraceEventArgs, TraceEventArgs>>()
        {
            { TraceColumn.StartTime, (e, a) => {
                    string s = a[TraceColumn.StartTime] ?? a[TraceColumn.CurrentTime] ?? string.Empty;
                    DateTime.TryParse(s, CultureInfo.CurrentUICulture, DateTimeStyles.AssumeUniversal, out var startTime);
                    e.StartTime = startTime;
                } 
            },


            //{ TraceColumn.EventClass, (e,a ) => {e.EventClassName = a.EventClass.ToString(); } },
            { TraceColumn.EventSubclass, (e,a ) => {e.EventSubclassName =  a.EventSubclass.ToString(); } },
            { TraceColumn.TextData, (e,a) => { ProcessTextData( e, a); } },
            { TraceColumn.RequestID, (e,a) => {e.RequestId = a[TraceColumn.RequestID]; } },
            { TraceColumn.DatabaseName, (e,a) => {e.DatabaseName= a.DatabaseName; } },
            //{ TraceColumn.DatabaseFriendlyName, (e,a) => {e.DatabaseFriendlyName = a[TraceColumn.RequestID]; } },
            { TraceColumn.ActivityID, (e,a) => {e.ActivityId = a[TraceColumn.ActivityID]; } },
            { TraceColumn.SessionID, (e,a) => {e.SessionId = a.SessionID; } },
            { TraceColumn.CurrentTime, (e,a) => {e.CurrentTime = a.CurrentTime; } },
            { TraceColumn.RequestProperties, (e,a) => {e.RequestProperties = a.RequestProperties; } },
            { TraceColumn.RequestParameters, (e,a) => {e.RequestParameters = a.RequestParameters; } },
            { TraceColumn.NTUserName, (e,a) => {e.NTUserName = a.NTUserName; } },
            { TraceColumn.Duration, (e,a) => {e.Duration = a.Duration; } },
            { TraceColumn.CpuTime, (e,a) => {e.CpuTime = a.CpuTime; } },
            { TraceColumn.EndTime, (e,a) => {e.EndTime = a.EndTime; } },
            { TraceColumn.Spid, (e,a) => {e.SPID = a.Spid; } },
            { TraceColumn.ObjectID, (e,a) => {e.ObjectId = a.ObjectID; } },
            { TraceColumn.ObjectName, (e,a) => {e.ObjectName = a.ObjectName; } },
            { TraceColumn.ObjectPath, (e,a) => {e.ObjectPath = a.ObjectPath; } },
            { TraceColumn.ObjectReference, (e,a) => {e.ObjectReference = a.ObjectReference; } },
            { TraceColumn.IntegerData, (e,a) => { try {e.IntegerData = a.IntegerData; } catch { } } },
            { TraceColumn.ProgressTotal, (e,a) => { try {e.ProgressTotal = a.ProgressTotal; } catch { } } },
        };

        public static Dictionary<string, Action<DaxStudioTraceEventArgs, string>> TextDataMap = new Dictionary<string, Action<DaxStudioTraceEventArgs, string>>()
        {
            {"TableID", (args, v) => { int.TryParse(v, out var l); args.TableID = l; } },
            {"PartitionID", (args,v) => { int.TryParse(v, out var l); args.PartitionID = l; } },
            {"MashupCPUTime",(args,v)=> { long.TryParse(v, out var l); args.MashupCPUTimeMs = l; } },
            {"MashupPeakMemory",(args,v)=> { long.TryParse(v, out var l); args.MashupPeakMemoryKb = l; } }

        };

        public DaxStudioTraceEventArgs(Microsoft.AnalysisServices.TraceEventArgs e, string powerBiFileName, List<int> eventColumns)
        {
            StartTime = DateTime.Now;
            EventClassName = e.EventClass.ToString();
            DatabaseName = e.DatabaseName;
            
            ActivityId = e[TraceColumn.ActivityID];
            RequestId = e[TraceColumn.RequestID];
            SessionId = e.SessionID;
            CurrentTime = e.CurrentTime;

            foreach (var col in eventColumns)
            {
                if( ColumnMap.TryGetValue((TraceColumn)col,out var mappingFunc)){

                    try
                    {
                        mappingFunc(this, e);
                    }
                    catch {
                        // skip over any failed mappings
                    }
                }
            }
            DatabaseFriendlyName = !string.IsNullOrEmpty(powerBiFileName) ? powerBiFileName : DatabaseName;

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
        //public string RequestID { get; set; }
        public string RequestProperties { get; set; }
        public string RequestParameters { get; set; }

        public string SPID { get; set; }
        public string ObjectName { get; set; }
        public string ObjectPath { get; set; }
        public string ObjectReference { get; set; }
        public long ProgressTotal { get; set; }
        public string ActivityId { get; set; }
        public string RequestId { get; set; }

        public DateTime CurrentTime { get; set; }
        public long IntegerData { get; set; }
        public string SessionId { get; set; }
        public string ObjectType { get; set; }
        public string ObjectId { get; set; }

        public int TableID { get; set; }
        public int PartitionID { get; set; }
        public long MashupCPUTimeMs { get; set; }
        public long MashupPeakMemoryKb { get; set; }

        private static void ProcessTextData(DaxStudioTraceEventArgs dsArgs, TraceEventArgs amoArgs)
        {
            dsArgs.TextData = amoArgs.TextData;

            if (amoArgs.EventClass != TraceEventClass.ProgressReportEnd || amoArgs.EventSubclass != TraceEventSubclass.TabularRefresh) return;

            // try parsing extra information out of the TextData for ProgressReportEnd Events
            var result = textDataRegex.Matches(amoArgs.TextData);

            foreach (Match match in result)
            {
                for (int i = 1; i < match.Groups.Count; i++)
                {
                    Group grp = match.Groups[i];
                    if (grp.Success)
                    {
                        //System.Diagnostics.Debug.WriteLine(grp + " : " + grp.Value);
                        TextDataMap[grp.Name].Invoke(dsArgs,grp.Value);
                    }
                }

            }

        }

    }
}
