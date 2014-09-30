using System;
using DaxStudio.Interfaces;

namespace DaxStudio.UI.Events
{
    public class DocumentConnectionUpdateEvent
    {
        public DocumentConnectionUpdateEvent(IConnection connection)
        {
            Connection = connection;
        }

        public IConnection Connection { get; set; }
    }
}
