extern alias ExcelAmo;

//using Microsoft.AnalysisServices;
using System;
using System.Collections.Generic;
using System.Globalization;
using xlAmo = ExcelAmo.Microsoft.AnalysisServices;

namespace DaxStudio.QueryTrace.Excel
{
    internal class DaxStudioTraceEventArgsFactory
    {
        public static Dictionary<xlAmo.TraceColumn, Action<DaxStudioTraceEventArgs, xlAmo.TraceEventArgs>> ColumnMap = new Dictionary<xlAmo.TraceColumn, Action<DaxStudioTraceEventArgs, xlAmo.TraceEventArgs>>()
        {
            { xlAmo.TraceColumn.StartTime, (e, a) => {
                    string s = a[xlAmo.TraceColumn.StartTime] ?? a[xlAmo.TraceColumn.CurrentTime] ?? string.Empty;
                    DateTime.TryParse(s, CultureInfo.CurrentUICulture, DateTimeStyles.AssumeUniversal, out var startTime);
                    e.StartTime = startTime;
                }
            },


            
            { xlAmo.TraceColumn.EventSubclass, (e,a ) => {e.EventSubclassName =  a.EventSubclass.ToString(); } },
            { xlAmo.TraceColumn.TextData, (e,a) => { e.TextData = a.TextData; } },
            //{ xlAmo.TraceColumn.RequestID, (e,a) => {e.RequestId = a[xlAmo.TraceColumn.RequestID]; } },
            { xlAmo.TraceColumn.DatabaseName, (e,a) => {e.DatabaseName= a.DatabaseName; } },
            
            //{ xlAmo.TraceColumn.ActivityID, (e,a) => {e.ActivityId = a[xlAmo.TraceColumn.ActivityID]; } },
            { xlAmo.TraceColumn.SessionID, (e,a) => {e.SessionId = a.SessionID; } },
            { xlAmo.TraceColumn.CurrentTime, (e,a) => {e.CurrentTime = a.CurrentTime; } },
            { xlAmo.TraceColumn.RequestProperties, (e,a) => {e.RequestProperties = a.RequestProperties; } },
            { xlAmo.TraceColumn.RequestParameters, (e,a) => {e.RequestParameters = a.RequestParameters; } },
            { xlAmo.TraceColumn.NTUserName, (e,a) => {e.NTUserName = a.NTUserName; } },
            { xlAmo.TraceColumn.Duration, (e,a) => {e.Duration = a.Duration; } },
            { xlAmo.TraceColumn.CpuTime, (e,a) => {e.CpuTime = a.CpuTime; } },
            { xlAmo.TraceColumn.EndTime, (e,a) => {e.EndTime = a.EndTime; } },
            { xlAmo.TraceColumn.Spid, (e,a) => {e.SPID = a.Spid; } },
            { xlAmo.TraceColumn.ObjectID, (e,a) => {e.ObjectId = a.ObjectID; } },
            { xlAmo.TraceColumn.ObjectName, (e,a) => {e.ObjectName = a.ObjectName; } },
            { xlAmo.TraceColumn.ObjectPath, (e,a) => {e.ObjectPath = a.ObjectPath; } },
            { xlAmo.TraceColumn.ObjectReference, (e,a) => {e.ObjectReference = a.ObjectReference; } },
            { xlAmo.TraceColumn.IntegerData, (e,a) => { try {e.IntegerData = a.IntegerData; } catch { } } },
            { xlAmo.TraceColumn.ProgressTotal, (e,a) => { try {e.ProgressTotal = a.ProgressTotal; } catch { } } },
        };

        public static DaxStudioTraceEventArgs Create(xlAmo.TraceEventArgs e, string powerBiFileName, List<int> eventColumns)
        {  
            var a = new DaxStudioTraceEventArgs();

            a.StartTime = DateTime.Now;
            a.EventClassName = e.EventClass.ToString();
            a.DatabaseName = e.DatabaseName;

            //a.ActivityId = e[xlAmo.TraceColumn.ActivityID];
            //a.RequestId = e[xlAmo.TraceColumn.RequestID];
            a.SessionId = e.SessionID;
            a.CurrentTime = e.CurrentTime;

            foreach (var col in eventColumns)
            {
                if (ColumnMap.TryGetValue((xlAmo.TraceColumn)col, out var mappingFunc))
                {

                    try
                    {
                        mappingFunc(a, e);
                    }
                    catch
                    {
                        // skip over any failed mappings
                    }
                }
            }
            a.DatabaseFriendlyName = !string.IsNullOrEmpty(powerBiFileName) ? powerBiFileName : a.DatabaseName;

            return a;

        }
    }
}
