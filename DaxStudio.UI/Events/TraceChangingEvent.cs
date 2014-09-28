using DaxStudio.UI.Model;
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
