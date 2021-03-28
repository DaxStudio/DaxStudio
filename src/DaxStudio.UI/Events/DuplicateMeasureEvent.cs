using DaxStudio.UI.Model;

namespace DaxStudio.UI.Events
{
    public class DuplicateMeasureEvent
    {
        public DuplicateMeasureEvent(QueryBuilderColumn measure)
        {
            Measure = measure;

        }

        public QueryBuilderColumn Measure { get; }
    }
}
