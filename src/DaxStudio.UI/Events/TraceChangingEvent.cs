using DaxStudio.Interfaces;
using DaxStudio.QueryTrace.Interfaces;
using DaxStudio.UI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Events
{
    
    public class TraceChangingEvent
    {
        public TraceChangingEvent(QueryTraceStatus traceStatus)
        {
            TraceStatus = traceStatus;
        }
        public QueryTraceStatus TraceStatus { get; set; }
    }
}
