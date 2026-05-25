using DaxStudio.Core.Exports;

namespace DaxStudio.Core.Events
{
    public class ExportStatusUpdateEvent
    {
        public ExportStatusUpdateEvent(SelectedTable table) : this(table, false) { }
        public ExportStatusUpdateEvent(SelectedTable table, bool completed)
        {
            SelectedTable = table;
            Completed = completed;
        }

        public SelectedTable SelectedTable {get;}
        public bool Completed { get; }
    }
}
