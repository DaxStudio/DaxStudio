//extern alias ExcelAmo;

using Microsoft.AnalysisServices;
//using xlAmo = ExcelAmo.Microsoft.AnalysisServices;
namespace DaxStudio.QueryTrace
{
    public  static partial class TraceEventFactory
    {
        public static TraceEvent Create(TraceEventClass eventClass)
        {
            var trc = new TraceEvent(eventClass);
            trc.Columns.Add(TraceColumn.EventClass);
            trc.Columns.Add(TraceColumn.TextData);
            trc.Columns.Add(TraceColumn.CurrentTime);
            trc.Columns.Add(TraceColumn.Spid);
            trc.Columns.Add(TraceColumn.SessionID);
            trc.Columns.Add(TraceColumn.ActivityID);
            trc.Columns.Add(TraceColumn.RequestID);
            trc.Columns.Add(TraceColumn.DatabaseName);

            if (eventClass == TraceEventClass.QueryEnd)
            {
                trc.Columns.Add(TraceColumn.EndTime);
                trc.Columns.Add(TraceColumn.NTUserName);
            }

            if (eventClass != TraceEventClass.DirectQueryEnd && eventClass != TraceEventClass.Error) {
                // DirectQuery doesn't have subclasses
                trc.Columns.Add(TraceColumn.EventSubclass);
            }

            if (eventClass != TraceEventClass.VertiPaqSEQueryCacheMatch)
            {
                trc.Columns.Add(TraceColumn.StartTime);
            }

            if (eventClass == TraceEventClass.QueryEnd 
                || eventClass == TraceEventClass.CommandEnd
                || eventClass == TraceEventClass.DAXQueryPlan)
            {
                trc.Columns.Add(TraceColumn.ApplicationName);
            }
            
            if (eventClass == TraceEventClass.QueryBegin)
            {
                trc.Columns.Add(TraceColumn.RequestParameters);
                trc.Columns.Add(TraceColumn.RequestProperties);
            }

            switch (eventClass)
            {
                case TraceEventClass.CommandEnd:
                case TraceEventClass.CalculateNonEmptyEnd:
                case TraceEventClass.DirectQueryEnd:
                case TraceEventClass.DiscoverEnd:
                case TraceEventClass.ExecuteMdxScriptEnd:
                case TraceEventClass.FileSaveEnd:
                case TraceEventClass.ProgressReportEnd:
                case TraceEventClass.QueryCubeEnd:
                case TraceEventClass.QueryEnd:
                case TraceEventClass.QuerySubcube:
                case TraceEventClass.QuerySubcubeVerbose:
                case TraceEventClass.VertiPaqSEQueryEnd:
                    trc.Columns.Add(TraceColumn.Duration);
                    trc.Columns.Add(TraceColumn.CpuTime);
                    break;
                case TraceEventClass.Error:
                    trc.Columns.Add(TraceColumn.Error);
                    break;

            }
            return trc;
        }

    }
}
