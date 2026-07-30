using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ADOTabular;
using DaxStudio.UI.Model;

namespace DaxStudio.UI.Extensions
{
    public static class QueryBuilderColumnExtensions
    {
        public static bool IsMeasure(this QueryBuilderColumn column)
        {
            switch (column.ObjectType)
            {
                case ADOTabularObjectType.Measure:
                case ADOTabularObjectType.MeasureFormatString:
                case ADOTabularObjectType.KPI:
                case ADOTabularObjectType.KPIGoal:
                case ADOTabularObjectType.KPIStatus:
                    return true;
                default:
                    return false;
            }

            
        }

        // this must match the filter used by the OrderBy collection view in the QueryBuilderViewModel
        public static bool IsSortable(this QueryBuilderColumn column)
        {
            return column.ObjectType == ADOTabularObjectType.Column
                || column.ObjectType == ADOTabularObjectType.Level;
        }
    }
}
