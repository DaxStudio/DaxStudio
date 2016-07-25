using DaxStudio.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Events
{
    public class SelectedModelChangedEvent
    {
        public SelectedModelChangedEvent(DocumentViewModel document)
        {
            Document = document;
        }

        public DocumentViewModel Document { get; private set; }
    }
}
