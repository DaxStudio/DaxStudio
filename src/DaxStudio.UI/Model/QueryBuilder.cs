using ADOTabular;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Model
{
    public static class QueryBuilder
    {
        public static string BuildQuery(ICollection<IADOTabularColumn> columns, ICollection<TreeViewColumnFilter> filters)
        {
            var columnList = BuildColumns(columns);
            var filterList = BuildFilters(filters);
            var measureList = BuildMeasures(columns);
            var filterStart = filters.Count > 0 ? "\n    ," : string.Empty;
            var measureStart = columns.Count(c=> c.ObjectType == ADOTabularObjectType.Measure) > 0 ? "\n    ," : string.Empty;

            StringBuilder sbQuery = new StringBuilder();
            sbQuery.Append("// START QUERY BUILDER\n");
            sbQuery.Append("EVALUATE\nSUMMARIZECOLUMNS(\n    ");
            sbQuery.Append(columnList);
            sbQuery.Append(filterStart);
            sbQuery.Append(filterList);
            sbQuery.Append(measureStart);
            sbQuery.Append(measureList);

            sbQuery.Append("\n)\n// END QUERY BUILDER");
            return sbQuery.ToString();
        }

        private static string BuildMeasures(ICollection<IADOTabularColumn> columns)
        {
            // TODO - should I get KPIs also??
            var meas = columns.Where(c => c.ObjectType == ADOTabularObjectType.Measure);
            if (!meas.Any()) return string.Empty;
            // build a comma separated list of "Caption", [DaxName] values
            return meas.Select(c => $"\"{c.Caption}\", {c.DaxName}").Aggregate((i, j) => i + "\n    ," + j);
        }


        private static string BuildColumns(ICollection<IADOTabularColumn> columns)
        {
            // TODO - should I get Levels also??
            var cols = columns.Where(c => c.ObjectType == ADOTabularObjectType.Column);
            if (!cols.Any()) return string.Empty;

            // build a comma separated list of [DaxName] values
            return cols.Select(c => c.DaxName).Aggregate((i, j) => i + "\n    ," + j);
        }

        private static string BuildFilters(ICollection<TreeViewColumnFilter> filters)
        {
            //DEFINE VAR __DS0FilterTable =
            //FILTER(
            //  KEEPFILTERS(VALUES('DimCustomer'[EnglishEducation])),
            //  'DimCustomer'[EnglishEducation] = "Bachelors"
            //)
            if (filters.Count == 0) return string.Empty;
            return (string)filters.Select(f => f.FilterExpression).Aggregate((i, j) => i + "\n    ," + j);
        }
    }
}
