using ADOTabular;

namespace DaxStudio.Common.Interfaces
{
    public interface IDmvProvider
    {
        ADOTabularDynamicManagementViewCollection DynamicManagementViews { get; }
    }


}
