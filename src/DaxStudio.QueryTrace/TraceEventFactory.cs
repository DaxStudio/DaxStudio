//extern alias ExcelAmo;

using Microsoft.AnalysisServices;
using System;
using System.Collections.Generic;
//using xlAmo = ExcelAmo.Microsoft.AnalysisServices;
namespace DaxStudio.QueryTrace
{
    public  static partial class TraceEventFactory
    {
       

        private static List<TraceColumn> desiredColumns = new List<TraceColumn>() {
            TraceColumn.ActivityID,
            TraceColumn.ApplicationName,
            TraceColumn.CpuTime,
            TraceColumn.CurrentTime,
            TraceColumn.DatabaseName,
            TraceColumn.Duration,
            TraceColumn.EndTime,
            TraceColumn.Error,
            TraceColumn.EventClass,
            TraceColumn.EventSubclass,
            TraceColumn.IntegerData,
            TraceColumn.NTUserName,
            TraceColumn.ObjectPath,
            TraceColumn.ObjectName,
            TraceColumn.ObjectReference,
            TraceColumn.RequestID,
            TraceColumn.RequestParameters,
            TraceColumn.RequestProperties,
            TraceColumn.SessionID,
            TraceColumn.Spid,
            TraceColumn.StartTime,
            TraceColumn.TextData
        };

        internal static TraceEvent Create(TraceEventClass eventClass, HashSet<TraceColumn> traceColumns)
        {
            var trcEvent = new TraceEvent(eventClass);
            foreach (var col in desiredColumns)
            {
                if (traceColumns.Contains(col)) 
                    trcEvent.Columns.Add(col);
            }
            return trcEvent;
        }
    }
}
