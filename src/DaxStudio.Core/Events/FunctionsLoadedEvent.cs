using ADOTabular;

namespace DaxStudio.Core.Events
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
