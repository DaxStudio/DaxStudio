extern alias ExcelAmo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using xlAmo = ExcelAmo.Microsoft.AnalysisServices;

namespace DaxStudio.QueryTrace
{
    public class DaxStudioTraceEventArgs
    {
        private string _eventClassName;
        private string _eventSubclassName;
        private DaxStudioTraceEventClass _eventClass = DaxStudioTraceEventClass.NotAvailable;
        private DaxStudioTraceEventSubclass _eventSubclass = DaxStudioTraceEventSubclass.NotAvailable;
        // TODO - clean up comments
        //private Microsoft.AnalysisServices.TraceEventArgs e;
        //private xlAmo.TraceEventArgs xe;
        public DaxStudioTraceEventArgs(Microsoft.AnalysisServices.TraceEventArgs e)
        {
            // not all events have CpuTime
            try {
                CpuTime = e.CpuTime;
            } catch (ArgumentNullException) {
                CpuTime = 0;
            }
            // not all events have a duration
            try { 
                Duration = e.Duration;
            } catch (ArgumentNullException) {
                Duration = 0;
            }

            TextData = e.TextData;
            EventClassName = e.EventClass.ToString();
            EventSubclassName = e.EventSubclass.ToString();
            Enum.TryParse<DaxStudioTraceEventClass>(EventClassName,out _eventClass);
            Enum.TryParse<DaxStudioTraceEventSubclass>(EventSubclassName, out _eventSubclass);
        }

        public DaxStudioTraceEventArgs() { }

        public DaxStudioTraceEventArgs(string eventClass, string eventSubclass, long duration, long cpuTime, string textData) {
            CpuTime = cpuTime;
            Duration = duration;
            TextData = textData;
            EventClassName = eventClass;
            EventSubclassName = eventSubclass;
        }
        /*
        public DaxStudioTraceEventArgs(ExcelAmo::Microsoft.AnalysisServices.TraceEventArgs e)
        {
            // TODO: Complete member initialization
            this.xe = e;

            throw new NotImplementedException("DaxStudioTraceEventArgs");
        }
        */

        //TODO - implement DaxStudioTraceEventArgs
        public string EventClassName { 
            get { return _eventClassName; } 
            set { _eventClassName = value;
            Enum.TryParse<DaxStudioTraceEventClass>(_eventClassName, out _eventClass);
            } }
        public string EventSubclassName {
            get { return _eventSubclassName; }
            set
            {
                _eventSubclassName = value;
                Enum.TryParse<DaxStudioTraceEventSubclass>(_eventSubclassName, out _eventSubclass);
            }
        }

        public string TextData { get; set; }
        public long Duration { get; set; }
        public long CpuTime { get; set; }

        public DaxStudioTraceEventClass EventClass { get { return _eventClass; } }
        public DaxStudioTraceEventSubclass EventSubclass { get { return _eventSubclass; } }
    }
}
