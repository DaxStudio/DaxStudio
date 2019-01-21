using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DaxStudio.UI.Events
{
    public class NewDocumentEvent
    {
        private readonly IResultsTarget _target;
        public NewDocumentEvent(IResultsTarget target)
        {
            _target = target;
        }

        public NewDocumentEvent(IResultsTarget target, DocumentViewModel activeDocument)
        {
            _target = target;
            ActiveDocument = activeDocument;
        }

        public IResultsTarget Target
        {
            get
            {
                return _target;
            }
        }
        public string FileName { get; set; }
        public DocumentViewModel ActiveDocument { get; private set; }

    }
}
