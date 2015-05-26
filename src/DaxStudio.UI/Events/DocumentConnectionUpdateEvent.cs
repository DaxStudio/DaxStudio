using System;
using DaxStudio.Interfaces;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Caliburn.Micro;

namespace DaxStudio.UI.Events
{
    public class DocumentConnectionUpdateEvent
    {
        public DocumentConnectionUpdateEvent(IConnection connection, BindableCollection<string> databases)
        {
            Connection = connection;
            Databases = databases;
        }

        public IConnection Connection { get; set; }
        public BindableCollection<string> Databases { get; set; }
    }
}
