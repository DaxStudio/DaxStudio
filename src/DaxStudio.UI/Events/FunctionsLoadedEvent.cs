using ADOTabular;
using DaxStudio.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Events
{
    public class FunctionsLoadedEvent
    {
        public FunctionsLoadedEvent(DocumentViewModel document, ADOTabularFunctionGroupCollection functionGroups)
        {
            Document = document;
            FunctionGroups = functionGroups;
        }

        public DocumentViewModel Document { get; private set; }
        public ADOTabularFunctionGroupCollection FunctionGroups { get; private set; }
    }
}
