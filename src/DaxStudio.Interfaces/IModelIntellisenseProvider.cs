using ADOTabular;
using ADOTabular.Interfaces;
using System.Collections.Generic;

namespace DaxStudio.Interfaces
{
    public interface IModelIntellisenseProvider
    {
        IEnumerable<string> Keywords { get; }
        ADOTabularDynamicManagementViewCollection DynamicManagementViews { get; }
        ADOTabularFunctionGroupCollection FunctionGroups { get; }
        ADOTabularTableCollection GetTables();
    }
}
