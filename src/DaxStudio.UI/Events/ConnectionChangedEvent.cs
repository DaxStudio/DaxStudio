using ADOTabular;
using DaxStudio.UI.ViewModels;

namespace DaxStudio.UI.Events
{
    public class ConnectionChangedEvent
    { 
        public ConnectionChangedEvent( DocumentViewModel document, bool isPowerBIorSSDT)
        {
            Document = document;
            IsPowerBIorSSDT = isPowerBIorSSDT;
        }
        public DocumentViewModel Document { get; }
        public bool IsPowerBIorSSDT { get; }
    }
}
