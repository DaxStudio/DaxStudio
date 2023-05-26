using ADOTabular.Interfaces;
using DaxStudio.UI.Enums;

namespace DaxStudio.UI.Events
{
    public class SendTabularObjectToQueryBuilderEvent
    {
        public SendTabularObjectToQueryBuilderEvent(IADOTabularObject item, QueryBuilderItemType itemType)
        {
            TabularObject = item;
            ItemType = itemType;
        }

        public IADOTabularObject TabularObject { get; }
        public QueryBuilderItemType ItemType { get;  }
    }
}
