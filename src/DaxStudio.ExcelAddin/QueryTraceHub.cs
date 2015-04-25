using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DaxStudio.QueryTrace;
using Microsoft.AnalysisServices;
using DaxStudio.Interfaces;
using DaxStudio.ExcelAddin;
using DaxStudio.QueryTrace.Interfaces;

namespace DaxStudio
{
    [HubName("QueryTrace")]
    public class QueryTraceHub:Hub<IQueryTraceHub>
    {
        private static QueryTraceEngineExcel _engine;

        public void ConstructQueryTraceEngine( ADOTabular.AdomdClientWrappers.AdomdType connectionType, string sessionId, List<DaxStudioTraceEventClass> eventsToCapture)
        {
            string powerPivotConnStr = "";
            using (var xl = new ExcelHelper(Globals.ThisAddIn.Application)){
                powerPivotConnStr = xl.GetPowerPivotConnection().ConnectionString;
                // override command type if this is Excel 2013 or later
                if (xl.IsExcel2013OrLater)
                { connectionType = ADOTabular.AdomdClientWrappers.AdomdType.Excel; }
            }
            
            _engine = new QueryTraceEngineExcel(powerPivotConnStr, connectionType,sessionId, eventsToCapture);
            _engine.TraceError += ((o, e) => { Clients.Caller.OnTraceError(e); });
            _engine.TraceCompleted += ((o, e) => { OnTraceCompleted(e); });
            _engine.TraceStarted += ((o, e) => { Clients.Caller.OnTraceStarted(); });   
        }

        private void OnTraceCompleted(IList<DaxStudioTraceEventArgs> e)
        {
            try
            {
                Clients.Caller.OnTraceComplete(e.ToArray<DaxStudioTraceEventArgs>());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        public void StartAsync() {
            if (_engine != null)
            {
                _engine.StartAsync().ContinueWith((x) =>
                {
                    Clients.Caller.OnTraceStarting();
                });
            }
            else
            {
                Clients.Caller.OnTraceError("QueryTraceEngine not constructed");
            }
        }


        public void Stop() {
            if (_engine != null) _engine.Stop();
            Clients.Caller.OnTraceStopped();
        }


    }
}
