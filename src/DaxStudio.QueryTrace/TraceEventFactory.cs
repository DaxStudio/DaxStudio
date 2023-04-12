//extern alias ExcelAmo;

using Microsoft.AnalysisServices;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;
namespace DaxStudio.QueryTrace
{


    public  static partial class TraceEventFactory
    {

        public static List<TraceColumn> RequiredColumns = new List<TraceColumn>(){
            TraceColumn.EventClass,
            TraceColumn.EventSubclass,
            TraceColumn.TextData,
            TraceColumn.CurrentTime,
            TraceColumn.Spid,
            TraceColumn.SessionID,
            TraceColumn.ActivityID,
            TraceColumn.RequestID,
            TraceColumn.DatabaseName,
            TraceColumn.StartTime,
            TraceColumn.NTUserName,
            TraceColumn.ApplicationName,
            TraceColumn.ObjectPath,
            TraceColumn.ObjectName,
            TraceColumn.ObjectReference,
            TraceColumn.RequestParameters,
            TraceColumn.RequestProperties,
            TraceColumn.Duration,
            TraceColumn.CpuTime,
            TraceColumn.EndTime,
            TraceColumn.Error,
            TraceColumn.IntegerData,
            TraceColumn.ProgressTotal
        };


        public static TraceEvent Create(TraceEventClass eventClass, List<int> columns)
        {
            var trc = new TraceEvent(eventClass);
            var columnsToRecord = columns.Where(c => RequiredColumns.Contains((TraceColumn)c));
            foreach (var column in columnsToRecord)
            {
                trc.Columns.Add((TraceColumn)column);
            }
            return trc;
        }

    }
}
