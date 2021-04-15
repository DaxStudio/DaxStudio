using ADOTabular;

namespace DaxStudio.Common.Interfaces
{
    public interface IModelIntellisenseProvider
    {
        ADOTabularDynamicManagementViewCollection DynamicManagementViews { get; }
        ADOTabularFunctionGroupCollection FunctionGroups { get; }
        ADOTabularTableCollection GetTables();
    }
}
