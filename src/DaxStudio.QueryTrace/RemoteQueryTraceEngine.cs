using ADOTabular.AdomdClientWrappers;
using DaxStudio.QueryTrace.Interfaces;
using Microsoft.AspNet.SignalR.Client;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DaxStudio.QueryTrace
{
    public class RemoteQueryTraceEngine: IQueryTrace
    {
        HubConnection hubConnection;
        IHubProxy queryTraceHubProxy;
        QueryTraceStatus _status = QueryTraceStatus.Stopped;

        public RemoteQueryTraceEngine(string connectionString, ADOTabular.AdomdClientWrappers.AdomdType connectionType, string sessionId, List<DaxStudioTraceEventClass> events, int port)
        {
            Log.Debug("{{class} {method} {message}","RemoteQueryTraceEngine","constructor", "entered");
            // connect to hub
            hubConnection = new HubConnection(string.Format("http://localhost:{0}/",port));
            queryTraceHubProxy = hubConnection.CreateHubProxy("QueryTrace");
            
            // ==== DEBUG LOGGING =====
            //var writer = new System.IO.StreamWriter(@"d:\temp\SignalR_ClientLog.txt");
            //writer.AutoFlush = true;
            //hubConnection.TraceLevel = TraceLevels.All;
            //hubConnection.TraceWriter = writer;
            
            queryTraceHubProxy.On("OnTraceStarted", () => {OnTraceStarted();});
            queryTraceHubProxy.On("OnTraceComplete", (e) => { OnTraceComplete(e); });
            queryTraceHubProxy.On<string>("OnTraceError", (msg) => { OnTraceError(msg); });
            hubConnection.Start().Wait();
            // configure trace
            Log.Debug("{class} {method} {message} connectionType: {connectionType} sessionId: {sessionId} eventCount: {eventCount}", "RemoteQUeryTraceEngine", "<constructor>", "about to create remote engine", connectionType.ToString(), sessionId, events.Count);
            queryTraceHubProxy.Invoke("ConstructQueryTraceEngine", connectionType, sessionId, events).Wait();
            // wire up hub events

        }
        public async Task StartAsync()
        {
            Log.Debug("{class} {method} {message}", "RemoteQueryTraceEngine", "StartAsync", "entered");
            await queryTraceHubProxy.Invoke("StartAsync");
        }

        public void Stop()
        {
            _status = QueryTraceStatus.Stopping;
            queryTraceHubProxy.Invoke("Stop").Wait();
            _status = QueryTraceStatus.Stopped;
        }

        public void OnTraceStarted() {
            _status = QueryTraceStatus.Started;
            if (TraceStarted != null)
            { TraceStarted(this, null); }
        }
        
        public void OnTraceError(string errorMessage) { 
            if (TraceError != null) {
                TraceError(this, errorMessage);
            }
        }

        public void OnTraceComplete(DaxStudioTraceEventArgs[] capturedEvents)
        {
            if (TraceCompleted != null)
            { TraceCompleted(this, capturedEvents.ToList<DaxStudioTraceEventArgs>()); }
        }

        
        public void OnTraceComplete(JArray myArray)
        {            
            // HACK: not sure why we have to explicitly cast the argument from a JArray, I thought Signalr should do this for us
            var e = myArray.ToObject<DaxStudioTraceEventArgs[]>();
            
            if (TraceCompleted != null)
            { TraceCompleted(this, e); }
        }

        public void OnTraceCompleted(IList<DaxStudioTraceEventArgs> capturedEvents) { 
            if (TraceCompleted != null)
            { TraceCompleted(this, capturedEvents); }
        }


        //public event Microsoft.AnalysisServices.TraceEventHandler TraceEvent;

        public event EventHandler<IList<DaxStudioTraceEventArgs>> TraceCompleted;

        public event EventHandler TraceStarted;

        public event EventHandler<string> TraceError;

        public QueryTraceStatus Status
        {
            get
            {
                return _status;
            }
        }

        public void ConfigureTrace(string connectionString, AdomdType connectionType, string applicationName, string sessionId, List<DaxStudioTraceEventClass> events)
        {
            throw new InvalidOperationException("ConfigureTrace should not be called directly on the SignalR hub");
        }


        public void Dispose()
        {
            queryTraceHubProxy.Invoke("Dispose");
        }
    }
}
