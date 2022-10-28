namespace DaxStudio.UI.Events
{
    public class QueryFinishedEvent
    {
        public QueryFinishedEvent(bool successful = true)
        {
            Successful = successful;
        }

        public bool Successful { get; }
    }
}
