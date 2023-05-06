using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        public string MeasureName { get; set; }
        public string MeasureExpression { get; set; }
        public string MeasureFormatStringName { get; set; }
        public string FormatStringExpression { get; set; }
    }
}
