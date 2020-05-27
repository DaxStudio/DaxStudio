using DaxStudio.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Events
{
    public class ShowMeasureExpressionEditor
    {
        public ShowMeasureExpressionEditor(QueryBuilderColumn column)
        {
            Column = column;
        }

        public QueryBuilderColumn Column { get; }
    }
}
