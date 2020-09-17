using ADOTabular;
using ADOTabular.Interfaces;
using System.Collections.Generic;

namespace DaxStudio.Interfaces
{
    public interface IDmvProvider
    {
        ADOTabularDynamicManagementViewCollection DynamicManagementViews { get; }
    }


}
