using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.ExceptionHandling;
using Serilog;

namespace DaxStudio.ExcelAddin.Xmla
{
    
    class TraceExceptionLogger : ExceptionLogger
    {
        public override void Log(ExceptionLoggerContext context)
        {
            Trace.TraceError(context.ExceptionContext.Exception.ToString());
            Serilog.Log.Error("OWIN Trace Exception: {Exception}", context.ExceptionContext.Exception.ToString());
        }
            
    }
    
}
