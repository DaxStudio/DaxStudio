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
        public FunctionsLoadedEvent( ADOTabularFunctionGroupCollection functionGroups)
        {

            FunctionGroups = functionGroups;
        }
        public ADOTabularFunctionGroupCollection FunctionGroups { get; private set; }
    }
}
