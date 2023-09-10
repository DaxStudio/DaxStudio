using DaxStudio.UI.Model;

namespace DaxStudio.UI.Events
{
    public class ShowMeasureExpressionEditor
    {
        public ShowMeasureExpressionEditor(QueryBuilderColumn column, bool isNewMeasure)
        {
            Column = column;
            IsNewMeasure = isNewMeasure;
        }

        public QueryBuilderColumn Column { get; }
        public bool IsNewMeasure { get; set; }
    }
}
