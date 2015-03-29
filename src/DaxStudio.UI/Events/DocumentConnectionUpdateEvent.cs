using System;
using DaxStudio.Interfaces;
using System.Collections.Generic;

namespace DaxStudio.UI.Events
{
    public class DocumentConnectionUpdateEvent
    {
        public DocumentConnectionUpdateEvent(IConnection connection, SortedSet<string> databases)
        {
            Connection = connection;
            Databases = databases;
        }

        public IConnection Connection { get; set; }
        public SortedSet<string> Databases { get; set; }
    }
}
