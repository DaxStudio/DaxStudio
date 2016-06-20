using ADOTabular;
using DaxStudio.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Events
{
    public class DmvsLoadedEvent
    {
        public DmvsLoadedEvent(DocumentViewModel document, ADOTabularDynamicManagementViewCollection dmvs)
        {
            Document = document;
            DmvCollection = dmvs;
        }

        public ADOTabularDynamicManagementViewCollection DmvCollection { get; private set; }
        public DocumentViewModel Document { get; private set; }
    }
}
