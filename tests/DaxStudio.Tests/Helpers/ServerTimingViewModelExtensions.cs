using DaxStudio.QueryTrace;
using DaxStudio.UI.ViewModels;
using Microsoft.AnalysisServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests.Helpers
{
    public static class ServerTimingViewModelExtensions
    {
        private const long cpuTime = 10;

        public static void AddTestEvent(this ServerTimesViewModel vm, TraceEventClass traceClass, TraceEventSubclass traceSubclass, DateTime startTime, long durationMs)
        {
            AddTestEvent(vm, traceClass, traceSubclass, startTime, startTime.AddMilliseconds(durationMs));
        }

        public static void AddTestEvent(this ServerTimesViewModel vm, TraceEventClass traceClass, TraceEventSubclass traceSubclass,  DateTime startTime, DateTime endTime )
        {
            var sequence = vm.Events.Count;
            var evt = new DaxStudioTraceEventArgs(traceClass.ToString()
                , traceSubclass.ToString()
                , (long)(endTime - startTime).TotalMilliseconds
                , cpuTime
                , $"Test Event {sequence}"
                , ""
                , startTime)
            { EndTime = endTime,
            Duration = Convert.ToInt64((endTime - startTime).TotalMilliseconds)};

            if (traceClass == TraceEventClass.QueryEnd)
            {
                vm.QueryStartDateTime= startTime;
                vm.QueryEndDateTime= endTime;
            }

            vm.Events.Enqueue(evt);
        }
    }
}
