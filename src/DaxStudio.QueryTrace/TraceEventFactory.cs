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
            trc.Columns.Add(TraceColumn.EventSubclass);
            trc.Columns.Add(TraceColumn.TextData);
            trc.Columns.Add(TraceColumn.CurrentTime);
            trc.Columns.Add(TraceColumn.Spid);
            trc.Columns.Add(TraceColumn.SessionID);

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
