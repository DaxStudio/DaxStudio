using DaxStudio.QueryTrace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Model {
    
    public class DaxStudioTraceEventClassSubclass {
        public enum Language
        {
            Unknown = 0,
            xmSQL,
            SQL,
            DAX,
            DMX,
            MDX
        }
        public DaxStudioTraceEventClass Class { get; set; }
        public DaxStudioTraceEventSubclass Subclass { get; set; }

        public Language QueryLanguage { get; set; }

    }
}
