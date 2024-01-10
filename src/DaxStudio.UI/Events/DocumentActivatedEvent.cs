using DaxStudio.UI.ViewModels;

namespace DaxStudio.UI.Events
{
    public class DocumentActivatedEvent
    {
        public DocumentActivatedEvent(DocumentViewModel document) {
            Document = document;
        }
        public DocumentViewModel Document { get; }
    }
}
