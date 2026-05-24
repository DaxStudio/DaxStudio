namespace DaxStudio.Core.Events
{
    /// <summary>
    /// Raised when the active connection has changed.
    /// <see cref="Document"/> is typed as <see cref="object"/> so this event
    /// can live in the Core layer without depending on UI ViewModels.
    /// UI handlers should cast the value to their expected document type.
    /// </summary>
    public class ConnectionChangedEvent
    {
        public ConnectionChangedEvent(object document, bool isPowerBIorSSDT)
        {
            Document = document;
            IsPowerBIorSSDT = isPowerBIorSSDT;
        }
        public object Document { get; }
        public bool IsPowerBIorSSDT { get; }
    }
}
