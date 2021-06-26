using System;
using DaxStudio.Interfaces;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Caliburn.Micro;
using DaxStudio.UI.Interfaces;
using ADOTabular;

namespace DaxStudio.UI.Events
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
