using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DaxStudio.UI.ViewModels;

namespace DaxStudio.UI.Events
{
    public class ActivateDocumentEvent
    {
        public ActivateDocumentEvent(DocumentViewModel document)
        {
            Document = document;
        }

        public DocumentViewModel Document { get ; set; }
    }
}
