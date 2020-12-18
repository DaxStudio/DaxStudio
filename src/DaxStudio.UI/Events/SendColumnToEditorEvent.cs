using DaxStudio.Interfaces;
using DaxStudio.UI.Enums;

namespace DaxStudio.UI.Events
{
    public class SendColumnToEditorEvent
    {
        public SendColumnToEditorEvent(ITreeviewColumn column, QueryBuilderItemType itemType)
        {
            Column = column;
            ItemType = itemType;
        }

        public ITreeviewColumn Column { get; }
        public QueryBuilderItemType ItemType { get;  }
    }
}
