//extern alias ExcelAmo;

using Microsoft.AnalysisServices;
using System.Collections.Generic;
using System.Windows.Documents;
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

            //if (eventClass == TraceEventClass.QueryEnd)
            //{
            //    trc.Columns.Add(TraceColumn.EndTime);
            //    trc.Columns.Add(TraceColumn.NTUserName);
            //}

            if (eventClass != TraceEventClass.DirectQueryEnd 
                && eventClass != TraceEventClass.Error
                && eventClass != TraceEventClass.DAXEvaluationLog) {
                // DirectQuery doesn't have subclasses
                trc.Columns.Add(TraceColumn.EventSubclass);
            }

            if (eventClass != TraceEventClass.VertiPaqSEQueryCacheMatch
                && eventClass != TraceEventClass.JobGraph)
            {
                trc.Columns.Add(TraceColumn.StartTime);
            }

            if (eventClass == TraceEventClass.QueryEnd 
                || eventClass == TraceEventClass.CommandEnd)
            {
                trc.Columns.Add(TraceColumn.NTUserName);
                trc.Columns.Add(TraceColumn.ApplicationName);
            }

            if (eventClass == TraceEventClass.DAXQueryPlan)
            {
                trc.Columns.Add(TraceColumn.ApplicationName);
            }

            if (eventClass == TraceEventClass.ProgressReportBegin
                || eventClass == TraceEventClass.ProgressReportCurrent
                || eventClass == TraceEventClass.ProgressReportEnd
                || eventClass == TraceEventClass.ProgressReportError)
            {
                trc.Columns.Add(TraceColumn.ObjectPath);
                trc.Columns.Add(TraceColumn.ObjectName);
                trc.Columns.Add(TraceColumn.ObjectReference);                                    
            }

            switch(eventClass)
            {
                case TraceEventClass.ProgressReportCurrent:
                case TraceEventClass.ProgressReportEnd:
                    {
                        trc.Columns.Add(TraceColumn.IntegerData);
                    }
                    break;
            }

            
            if (eventClass == TraceEventClass.QueryBegin)
            {
                trc.Columns.Add(TraceColumn.RequestParameters);
                trc.Columns.Add(TraceColumn.RequestProperties);
                trc.Columns.Add(TraceColumn.ApplicationName);
                trc.Columns.Add(TraceColumn.NTUserName);
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
                    trc.Columns.Add(TraceColumn.EndTime);
                    break;
                case TraceEventClass.Error:
                    trc.Columns.Add(TraceColumn.Error);
                    break;

            }

            return trc;
        }

        public static TraceEvent Create(TraceEventClass eventClass, List<int> columns)
        {
            var trc = new TraceEvent(eventClass);
            foreach (var column in columns)
            {
                trc.Columns.Add((TraceColumn)column);
            }
            return trc;
        }

    }
}
