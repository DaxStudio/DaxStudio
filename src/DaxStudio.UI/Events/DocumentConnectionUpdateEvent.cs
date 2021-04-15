using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Caliburn.Micro;
using DaxStudio.Common.Interfaces;
using DaxStudio.QueryTrace.Interfaces;
using DaxStudio.UI.Interfaces;

namespace DaxStudio.UI.Events
{
    public class DocumentConnectionUpdateEvent
    {
        public DocumentConnectionUpdateEvent(IConnection connection, BindableCollection<string> databases, ITraceWatcher activeTrace)
        {
            Connection = connection;
            Databases = databases;
            ActiveTrace = activeTrace;
        }

        public IConnection Connection { get; set; }
        public BindableCollection<string> Databases { get; set; }
        public ITraceWatcher ActiveTrace { get; set; }
    }
}
