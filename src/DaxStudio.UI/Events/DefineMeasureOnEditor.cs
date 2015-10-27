using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DaxStudio.UI.Events
{
    public class DefineMeasureOnEditor
    {
        public DefineMeasureOnEditor(string measureName, string measureExpression)
        {
            this.MeasureName = measureName;
            this.MeasureExpression = measureExpression;
        }

        public string MeasureName { get; set; }
        public string MeasureExpression { get; set; }
    }
}
