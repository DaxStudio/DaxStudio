using DaxStudio.Interfaces;

namespace DaxStudio.UI.Events
{
    public class SendColumnToEditorEvent
    {
        public SendColumnToEditorEvent(ITreeviewColumn column, bool isFilter)
        {
            Column = column;
            IsFilter = isFilter;
        }

        public ITreeviewColumn Column { get; }
        public bool IsFilter { get;  }
    }
}
