using ADOTabular;

namespace DaxStudio.UI.Events
{
    public class DefineMeasureOnEditor
    {
        public DefineMeasureOnEditor(string measureName, string measureExpression, string measureFormatStringName, string formatStringExpression)
        {
            this.MeasureName = measureName;
            this.MeasureExpression = measureExpression;
            this.MeasureFormatStringName = measureFormatStringName;
            this.FormatStringExpression = formatStringExpression;
        }
        public DefineMeasureOnEditor(ADOTabularMeasure measure)
        {
            this.MeasureName = measure.Table.DaxName + measure.DaxName;
            this.MeasureExpression = measure.Expression;
            this.MeasureFormatStringName = measure.FormatStringDaxName;
            this.FormatStringExpression = measure.FormatStringExpression;
        }

        public string MeasureName { get; set; }
        public string MeasureExpression { get; set; }
        public string MeasureFormatStringName { get; set; }
        public string FormatStringExpression { get; set; }
    }
}
