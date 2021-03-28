using DaxStudio.Interfaces;
using DaxStudio.UI.Enums;

namespace DaxStudio.UI.Events
{
    public class SendColumnToQueryBuilderEvent
    {
        public SendColumnToQueryBuilderEvent(ITreeviewColumn column, QueryBuilderItemType itemType)
        {
            Column = column;
            ItemType = itemType;
        }

        public ITreeviewColumn Column { get; }
        public QueryBuilderItemType ItemType { get;  }
    }
}
