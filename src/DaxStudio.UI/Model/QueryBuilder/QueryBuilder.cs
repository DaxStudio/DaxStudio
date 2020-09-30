using ADOTabular;
using ADOTabular.AdomdClientWrappers;
using DaxStudio.UI.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Model
{
    public static class QueryBuilder
    {
        public static string BuildQuery(ADOTabular.Interfaces.IModelCapabilities modelCaps, ICollection<QueryBuilderColumn> columns, ICollection<QueryBuilderFilter> filters)
        {
            var measureDefines = BuildMeasureDefines(columns);

            var defineStart = measureDefines.Length > 0 ? "DEFINE\n    " : string.Empty;

            // using filter variables will not work against older data sources...
            //var filterDefines = BuildFilterDefines(filters);

            var columnList = BuildColumns(columns);
            var filterList = BuildFilters(modelCaps, filters);
            var measureList = BuildMeasures(columns);
            var filterStart = filters.Count > 0 ? ",\n    " : string.Empty;
            var measureStart = columns.Count(c => c.ObjectType == ADOTabularObjectType.Measure) > 0
                ? columns.Count(c => c.ObjectType == ADOTabularObjectType.Column) > 0 
                ? ",\n    " 
                : "\n    "
                : string.Empty;  


            if (columnList.Length == 0) return BuildQueryWithOnlyMeasures(measureDefines,filterList, measureList, filterStart, measureStart);
            return BuildQueryWithColumns(measureDefines, columnList, filterList, measureList, filterStart, measureStart);
        }

        private static string BuildQueryWithColumns(string measureDefines, string columnList, string filterList, string measureList, string filterStart, string measureStart)
        {
            StringBuilder sbQuery = new StringBuilder();
            sbQuery.Append("// START QUERY BUILDER\n");
            sbQuery.Append(measureDefines.Length > 0 ? "DEFINE\n" : string.Empty);
            sbQuery.Append(measureDefines);
            sbQuery.Append(measureDefines.Length > 0 ? "\n" : string.Empty);
            sbQuery.Append("EVALUATE\n");
            sbQuery.Append("SUMMARIZECOLUMNS(\n    "); // table function start
            sbQuery.Append(columnList);
            sbQuery.Append(filterStart);
            sbQuery.Append(filterList);
            sbQuery.Append(measureStart);
            sbQuery.Append(measureList);
            sbQuery.Append("\n)");                     // query function end
            sbQuery.Append("\n// END QUERY BUILDER");
            return sbQuery.ToString();
        }

        private static string BuildQueryWithOnlyMeasures(string measureDefines, string filterList, string measureList, string filterStart, string measureStart)
        {
            StringBuilder sbQuery = new StringBuilder();
            sbQuery.Append("// START QUERY BUILDER\n");
            sbQuery.Append(measureDefines.Length > 0 ? "DEFINE\n" : string.Empty);
            sbQuery.Append(measureDefines);
            sbQuery.Append(measureDefines.Length > 0 ? "\n" : string.Empty);
            sbQuery.Append("EVALUATE\n");
            sbQuery.Append("CALCULATETABLE(\n    ");  // table function start
            sbQuery.Append("ROW(");                   // ROW function start
            sbQuery.Append(measureStart);
            sbQuery.Append(measureList);
            sbQuery.Append("\n    )");                // end of ROW() function
            sbQuery.Append(filterStart);
            sbQuery.Append(filterList);

            sbQuery.Append("\n)");                    // query function end
            sbQuery.Append("\n// END QUERY BUILDER");
            return sbQuery.ToString();
        }

        private static string BuildMeasures(ICollection<QueryBuilderColumn> columns)
        {
            // TODO - should I get KPIs also??
            var meas = columns.Where(c => c.ObjectType == ADOTabularObjectType.Measure);
            if (!meas.Any()) return string.Empty;
            // build a comma separated list of "Caption", [DaxName] values
            return meas.Select(c => $"\"{c.Caption}\", {c.DaxName}").Aggregate((i, j) => i + ",\n    " + j);
        }

        private static string BuildMeasureDefines(ICollection<QueryBuilderColumn> columns)
        {
            // TODO - should I get KPIs also??
            var meas = columns.Where(c => c.ObjectType == ADOTabularObjectType.Measure && c.IsOverriden);
            if (!meas.Any()) return string.Empty;
            // build a comma separated list of "Caption", [DaxName] values
            return meas.Select(c => $"MEASURE {c.SelectedTable.DaxName}[{c.Caption}] = {c.MeasureExpression}" ).Aggregate((current, next) => current + "\n" + next);
        }

        private static string BuildColumns(ICollection<QueryBuilderColumn> columns)
        {
            // TODO - should I get Levels also??
            var cols = columns.Where(c => c.ObjectType == ADOTabularObjectType.Column);
            if (!cols.Any()) return string.Empty;

            // build a comma separated list of [DaxName] values
            return cols.Select(c => c.DaxName).Aggregate((current, next) => current + ",\n    " + next);
        }

        private static string BuildFilters(ADOTabular.Interfaces.IModelCapabilities modelCaps, ICollection<QueryBuilderFilter> filters)
        {
            //DEFINE VAR __DS0FilterTable =
            //FILTER(
            //  KEEPFILTERS(VALUES('DimCustomer'[EnglishEducation])),
            //  'DimCustomer'[EnglishEducation] = "Bachelors"
            //)

            Func<QueryBuilderFilter,string> filterExpressionFunc = FilterExpressionBasic;
            if (modelCaps.DAXFunctions.TreatAs) filterExpressionFunc = FilterExpressionTreatAs;

            if (filters.Count == 0) return string.Empty;
            return (string)filters.Select(f => filterExpressionFunc(f)).Aggregate((i, j) => i + ",\n    " + j);
        }

        public static string FilterExpressionTreatAs(QueryBuilderFilter filter)
        {
            var quotes = filter.TabularObject.DataType == typeof(string) ? "\"" : string.Empty;
            var formattedVal = FormattedValue(filter, () => { return filter.FilterValue; });
            var colName = filter.TabularObject.DaxName;
            switch (filter.FilterType)
            {
                case FilterType.Is:
                    return $@"KEEPFILTERS( TREATAS( {{{formattedVal}}}, {colName} ))";
                default:
                    return FilterExpressionBasic(filter);
            }
        }

        public static string FilterExpressionBasic(QueryBuilderFilter filter)
        {
            var quotes = filter.TabularObject.DataType == typeof(string) ? "\"" : string.Empty;
            var formattedVal = FormattedValue(filter, () => { return filter.FilterValue; });
            var formattedVal2 = FormattedValue(filter, () => { return filter.FilterValue2; });
            var colName = filter.TabularObject.DaxName;
            switch (filter.FilterType)
            {
                case FilterType.Is:
                    return $@"KEEPFILTERS( FILTER( ALL( {colName} ), {colName} = {formattedVal} ))";
                case Enums.FilterType.IsNot:
                    return $@"KEEPFILTERS( FILTER( ALL( {colName} ), {colName} <> {formattedVal} ))";
                case FilterType.StartsWith:
                    return $@"KEEPFILTERS( FILTER( ALL( {colName} ), SEARCH( {formattedVal}, {colName}, 1, 0 ) = 1 ))";
                case FilterType.Contains:
                    return $@"KEEPFILTERS( FILTER( ALL( {colName} ), SEARCH( {formattedVal}, {colName}, 1, 0 ) >= 1 ))";
                case FilterType.DoesNotStartWith:
                    return $@"KEEPFILTERS( FILTER( ALL( {colName} ), NOT( SEARCH( {formattedVal}, {colName}, 1, 0 ) = 1 )))";
                case FilterType.DoesNotContain:
                    return $@"KEEPFILTERS( FILTER( ALL( {colName} ), NOT( SEARCH( {formattedVal}, {colName}, 1, 0 ) >= 1 )))";
                case FilterType.IsBlank:
                    return $@"KEEPFILTERS( FILTER( ALL( {colName} ), ISBLANK( {colName} )))";
                case FilterType.IsNotBlank:
                    return $@"KEEPFILTERS( FILTER( ALL( {colName} ), NOT( ISBLANK( {colName} ))))";
                case FilterType.GreaterThan:
                    return $@"KEEPFILTERS( FILTER( ALL( {colName} ), {colName} > {formattedVal} ))";
                case FilterType.GreaterThanOrEqual:
                    return $@"KEEPFILTERS( FILTER( ALL( {colName} ), {colName} >= {formattedVal} ))";
                case FilterType.LessThan:
                    return $@"KEEPFILTERS( FILTER( ALL( {colName} ), {colName} < {formattedVal} ))";
                case FilterType.LessThanOrEqual:
                    return $@"KEEPFILTERS( FILTER( ALL( {colName} ), {colName} <= {formattedVal} ))";
                    break;
                case FilterType.Between:
                    return $@"KEEPFILTERS( FILTER( ALL( {colName} ), {colName} >= {formattedVal} && {colName} <= {formattedVal2} ))";
                    break;
                default:
                    throw new NotSupportedException($"The filter type '{filter.FilterType.ToString()}' is not supported");
            }

            
            
        }

        public static string FormattedValue(QueryBuilderFilter filter, Func<string> valueFunc)
        {
            if (filter.TabularObject.DataType == typeof(DateTime)) {
                DateTime parsedDate = DateTime.MinValue;
                DateTime.TryParse(valueFunc(), out parsedDate);
                if (parsedDate > DateTime.MinValue)
                {
                    return $"DATE({parsedDate.Year},{parsedDate.Month},{parsedDate.Day})";
                }
                else
                {
                    throw new ArgumentException($"Unable to parse the value '{valueFunc()}' as a DateTime value");
                }
            }
            var quotes = filter.TabularObject.DataType == typeof(string) ? "\"" : string.Empty;
            return $"{quotes}{valueFunc()}{quotes}";
        }
    }
}
