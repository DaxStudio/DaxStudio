using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Interfaces
{

    public enum QueryStatus
    {
        Successful,
        Cancelled,
        Error,
        Running
    }

    public interface IQueryHistoryEvent
    {
        
         string QueryText { get; }
         DateTime StartTime { get; }
         long ClientDurationMs { get; set; }
         long ServerDurationMs { get;  set; }
         long SEDurationMs { get;  set; }
         long FEDurationMs { get; set; }
         string ServerName { get;   }
         string DatabaseName { get;   }
         string FileName { get;   }

         string RowCount { get; set; }

         QueryStatus Status { get; set; }
    }
}
