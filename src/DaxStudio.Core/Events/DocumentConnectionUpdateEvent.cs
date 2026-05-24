using DaxStudio.Interfaces;
using Caliburn.Micro;
using ADOTabular;
using DaxStudio.Core.Interfaces;

namespace DaxStudio.Core.Events
{
    public class DocumentConnectionUpdateEvent
    {
        public DocumentConnectionUpdateEvent(IConnection connection, BindableCollection<DatabaseDetails> databases, ITraceWatcher activeTrace)
        {
            Connection = connection;
            Databases = databases;
            ActiveTrace = activeTrace;
        }

        public IConnection Connection { get; set; }
        public BindableCollection<DatabaseDetails> Databases { get; set; }
        public ITraceWatcher ActiveTrace { get; set; }
    }
}
