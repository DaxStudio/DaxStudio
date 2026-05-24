using ADOTabular;

namespace DaxStudio.Core.Events
{
    public class DmvsLoadedEvent
    {
        public DmvsLoadedEvent(ADOTabularDynamicManagementViewCollection dmvs)
        {
            DmvCollection = dmvs;
        }

        public ADOTabularDynamicManagementViewCollection DmvCollection { get; private set; }

    }
}
