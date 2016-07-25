using DaxStudio.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Events
{
    public class MetadataLoadedEvent
    {
        public MetadataLoadedEvent(DocumentViewModel document, ADOTabular.ADOTabularModel model)
        {
            Document = document;
            Model = model;
        }
        public ADOTabular.ADOTabularModel Model { get; private set; }
        public DocumentViewModel Document { get; private set; }
    }
}
