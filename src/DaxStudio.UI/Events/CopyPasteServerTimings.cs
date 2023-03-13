namespace DaxStudio.UI.Events
{
    public class CopyPasteServerTimingsEvent
    {
        public CopyPasteServerTimingsEvent( bool includeHeader )
        {
            IncludeHeader = includeHeader;
        }

        public bool IncludeHeader { get; set; }
    }
}
