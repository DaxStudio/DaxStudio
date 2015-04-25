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
            if (eventClass != TraceEventClass.VertiPaqSEQueryCacheMatch)
            {
                trc.Columns.Add(TraceColumn.StartTime);
            }
            trc.Columns.Add(TraceColumn.Spid);
            trc.Columns.Add(TraceColumn.SessionID);
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
