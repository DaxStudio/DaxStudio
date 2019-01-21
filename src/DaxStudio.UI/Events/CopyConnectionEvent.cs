using DaxStudio.UI.ViewModels;

namespace DaxStudio.UI.Events
{
    public class CopyConnectionEvent
    {
        public CopyConnectionEvent(DocumentViewModel sourceDocument)
        {
            SourceDocument = sourceDocument;
        }

        public DocumentViewModel SourceDocument { get; private set; }
    }
}
