
using Microsoft.AnalysisServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DaxStudio.QueryTrace
{
    public class DaxStudioTraceEventArgs
    {
        private string _eventClassName;
        private string _eventSubclassName;
        private DaxStudioTraceEventClass _eventClass = DaxStudioTraceEventClass.NotAvailable;
        private DaxStudioTraceEventSubclass _eventSubclass = DaxStudioTraceEventSubclass.NotAvailable;
        
        public DaxStudioTraceEventArgs(Microsoft.AnalysisServices.TraceEventArgs e)
        {
            EventClassName = e.EventClass.ToString();
            EventSubclassName = e.EventSubclass.ToString();
            Enum.TryParse<DaxStudioTraceEventClass>(EventClassName, out _eventClass);
            Enum.TryParse<DaxStudioTraceEventSubclass>(EventSubclassName, out _eventSubclass);

            TextData = e.TextData;
            /*
            switch (e.EventClass)
            {
                case TraceEventClass.QueryEnd:
                    Duration = e.Duration;
                    DatabaseName = e.DatabaseName;
                    StartTime = e.StartTime;
                    NTUserName = e.NTUserName;
                    
                    break;
                case TraceEventClass.VertiPaqSEQueryCacheMatch:
                    StartTime = e.StartTime;
                    break;
                case TraceEventClass.CommandBegin:

                default:
                    throw new ArgumentException($"No mapping for the event class {e.EventClass.ToString()} was found");

            }
            */
            // not all events have CpuTime
            try {
                CpuTime = e.CpuTime;
            } catch (ArgumentNullException) {
                CpuTime = 0;
            }
            // not all events have a duration
            try
            {
                Duration = e.Duration;
            }
            catch (ArgumentNullException)
            {
                Duration = 0;
            }
            if (e.NTUserName != null)
                NTUserName = e.NTUserName;
            
            if (e.DatabaseName != null) 
                DatabaseName = e.DatabaseName;

            StartTime = e.StartTime;
            //if (e.EndTime != null) 
            //    EndTime = e.EndTime;
            
        }

        public DaxStudioTraceEventArgs() { }

        public DaxStudioTraceEventArgs(string eventClass, string eventSubclass, long duration, long cpuTime, string textData) {
            CpuTime = cpuTime;
            Duration = duration;
            TextData = textData;
            EventClassName = eventClass;
            EventSubclassName = eventSubclass;
        }
        
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
        public string NTUserName { get; private set; }
        public DateTime EndTime { get; private set; }
        public DateTime StartTime { get; private set; }
        public string DatabaseName { get; private set; }
    }
}
