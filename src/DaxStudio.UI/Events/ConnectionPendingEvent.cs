
using DaxStudio.UI.ViewModels;

namespace DaxStudio.UI.Events
{
    public class ConnectionPendingEvent
    {
        public ConnectionPendingEvent(DocumentViewModel document)
        {
            Document = document;
        }

        public DocumentViewModel Document { get; private set; }
    }
}
