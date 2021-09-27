namespace DaxStudio.UI.Events
{
    public class LoadQueryBuilderEvent
    {
        public LoadQueryBuilderEvent(string json)
        {
            Json = json;
        }

        public string Json { get; }
    }
}
