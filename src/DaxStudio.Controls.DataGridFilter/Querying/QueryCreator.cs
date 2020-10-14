using System;
using System.Collections.Generic;
using System.Text;
using DaxStudio.Controls.DataGridFilter.Support;

namespace DaxStudio.Controls.DataGridFilter.Querying
{
    public class QueryCreator
    {
        private List<object> Parameters { get; }

        private readonly Dictionary<string, FilterData> _filtersForColumns;
        private readonly ParameterCounter _paramCounter;

        public QueryCreator(
            Dictionary<string, FilterData> filtersForColumns)
        {
            this._filtersForColumns = filtersForColumns;

            _paramCounter = new ParameterCounter(0);
            Parameters = new List<object>();
        }

        public void CreateFilter(ref Query query)
        {
            if (query == null) throw  new ArgumentNullException(nameof(query));

            StringBuilder filter = new StringBuilder();

            foreach (KeyValuePair<string, FilterData> kvp in _filtersForColumns)
            {
                StringBuilder partialFilter = CreateSingleFilter(kvp.Value);

                if (filter.Length > 0 && partialFilter.Length > 0) filter.Append(" AND ");

                if (partialFilter.Length > 0)
                {
                    string valuePropertyBindingPath = String.Empty;
                    string[] paths = kvp.Value.ValuePropertyBindingPath.Split('.');

                    foreach (string p in paths)
                    {
                        if (!string.IsNullOrEmpty(valuePropertyBindingPath))
                        {
                            valuePropertyBindingPath += ".";
                        }

                        valuePropertyBindingPath += p;

                        filter.Append(valuePropertyBindingPath + " != null AND ");//eliminate: Nullable object must have a value and object reference not set to an object                        
                    }
                }

                filter.Append(partialFilter);
            }

            //init query
            query.FilterString    = filter.ToString();
            query.QueryParameters = Parameters;
        }

        private StringBuilder CreateSingleFilter(FilterData filterData)
        {
            StringBuilder filter = new StringBuilder();

            if (
                (filterData.Type == FilterType.NumericBetween || filterData.Type == FilterType.DateTimeBetween)
                &&
                (!string.IsNullOrEmpty(filterData.QueryString) || !string.IsNullOrEmpty(filterData.QueryStringTo))
                )
            {
                if (!string.IsNullOrEmpty(filterData.QueryString))
                {
                    CreateFilterExpression(
                        filterData, 
                        filterData.QueryString,
                        filter,
                        GetOperatorString(FilterOperator.GreaterThanOrEqual));
                }
                if (!string.IsNullOrEmpty(filterData.QueryStringTo))
                {
                    if (filter.Length > 0) filter.Append(" AND ");

                    CreateFilterExpression(
                        filterData, 
                        filterData.QueryStringTo, 
                        filter, 
                        GetOperatorString(FilterOperator.LessThanOrEqual));
                }
            }
            else if (!string.IsNullOrEmpty(filterData.QueryString)
                &&
                filterData.Operator != FilterOperator.Undefined)
            {
                if (filterData.Type == FilterType.Text)
                {
                    if (filterData.Operator == FilterOperator.Contains)
                        CreateContainsStringFilterExpression(filterData,filter);
                    else
                        CreateStringFilterExpression(filterData, filter);
                }
                else
                {
                    CreateFilterExpression(
                        filterData, filterData.QueryString, filter, GetOperatorString(filterData.Operator));
                }
            }

            return filter;
        }

        private void CreateFilterExpression(
            FilterData filterData, string queryString, StringBuilder filter, string operatorString)
        {
            filter.Append(filterData.ValuePropertyBindingPath);

            if (TrySetParameterValue(out var parameterValue, queryString, filterData.ValuePropertyType))
            {
                Parameters.Add(parameterValue);

                _paramCounter.Increment();

                filter.Append(" " + operatorString + " @" + _paramCounter.ParameterNumber);
            }
            else
            {
                filter = new StringBuilder();//do not use filter
            }
        }

        private bool TrySetParameterValue(
            out object parameterValue, string stringValue, Type type)
        {
            parameterValue = null;
            bool valueIsSet;

            try
            {
                if (type == typeof(DateTime?) || type == typeof(DateTime))
                {
                    parameterValue = DateTime.Parse(stringValue);
                }
                else if (type == typeof(Enum) || type.BaseType == typeof(Enum))
                {
                    Parameters.Add(Enum.Parse(type, stringValue, true));
                }
                else if (type == typeof(Boolean) || type.BaseType == typeof(Boolean))
                {
                    parameterValue = Convert.ChangeType(stringValue, typeof(Boolean));
                }
                else if (type == typeof(String))
                {
                    parameterValue = stringValue;
                }
                else
                {
                    parameterValue = Convert.ChangeType(stringValue, typeof(Double));//TODO use "real" number type
                }

                valueIsSet = true;
            }
            catch 
            {
                valueIsSet = false;
            }

            return valueIsSet;
        }

        private void CreateStringFilterExpression(
            FilterData filterData, StringBuilder filter)
        {
            StringFilterExpressionCreator
                creator = new StringFilterExpressionCreator(
                    _paramCounter, filterData, Parameters);

            string filterExpression = creator.Create();

            filter.Append(filterExpression);
        }

        private void CreateContainsStringFilterExpression(
            FilterData filterData, StringBuilder filter)
        {
            StringFilterExpressionCreator
                creator = new StringFilterExpressionCreator(
                    _paramCounter, filterData, Parameters);

            string filterExpression = creator.CreateContainsFilter();

            filter.Append(filterExpression);
        }

        private static string GetOperatorString(FilterOperator filterOperator)
        {
            string op;

            switch (filterOperator)
            {
                case FilterOperator.Undefined:
                    op = String.Empty;
                    break;
                case FilterOperator.LessThan:
                    op = "<";
                    break;
                case FilterOperator.LessThanOrEqual:
                    op = "<=";
                    break;
                case FilterOperator.GreaterThan:
                    op = ">";
                    break;
                case FilterOperator.GreaterThanOrEqual:
                    op = ">=";
                    break;
                case FilterOperator.Equals:
                    op = "=";
                    break;
                case FilterOperator.Like:
                    op = String.Empty;
                    break;
                default:
                    op = String.Empty;
                    break;
            }

            return op;
        }
    }
}