using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.ExceptionHandling;
using Serilog;

namespace DaxStudio.Xmla
{
    
        class TraceExceptionLogger : ExceptionLogger
        {
            public override void Log(ExceptionLoggerContext context)
            {
                Trace.TraceError(context.ExceptionContext.Exception.ToString());
                Serilog.Log.Error("Exception: {Exception}", context.ExceptionContext.Exception.ToString());
            }
            
        }
    
}
