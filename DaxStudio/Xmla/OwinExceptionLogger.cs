using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.ExceptionHandling;

namespace DaxStudio.Xmla
{
    
        class TraceExceptionLogger : ExceptionLogger
        {
            public override void Log(ExceptionLoggerContext context)
            {
                Trace.TraceError(context.ExceptionContext.Exception.ToString());
            }
            
        }
    
}
