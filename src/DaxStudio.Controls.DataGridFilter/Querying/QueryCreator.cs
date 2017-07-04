using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DaxStudio.Controls.DataGridFilter.Support;

namespace DaxStudio.Controls.DataGridFilter.Querying
{
    public class QueryCreator
    {
        private List<object> Parameters { get; set; }

        private readonly Dictionary<string, FilterData> filtersForColumns;
        private ParameterCounter paramCounter;

        public QueryCreator(
            Dictionary<string, FilterData> filtersForColumns)
        {
            this.filtersForColumns = filtersForColumns;

            paramCounter = new ParameterCounter(0);
            Parameters = new List<object>();
        }

        public void CreateFilter(ref Query query)
        {
            StringBuilder filter = new StringBuilder();

            foreach (KeyValuePair<string, FilterData> kvp in filtersForColumns)
            {
                StringBuilder partialFilter = createSingleFilter(kvp.Value);

                if (filter.Length > 0 && partialFilter.Length > 0) filter.Append(" AND ");

                if (partialFilter.Length > 0)
                {
                    string valuePropertyBindingPath = String.Empty;
                    string[] paths = kvp.Value.ValuePropertyBindingPath.Split(new Char[] { '.' });

                    foreach (string p in paths)
                    {
                        if (valuePropertyBindingPath != String.Empty)
                        {
                            valuePropertyBindingPath += ".";
                        }

                        valuePropertyBindingPath += p;

                        filter.Append(valuePropertyBindingPath + " != null AND ");//eliminate: Nullable object must have a value and object fererence not set to an object                        
                    }
                }

                filter.Append(partialFilter);
            }

            //init query
            query.FilterString    = filter.ToString();
            query.QueryParameters = Parameters;
        }

        private StringBuilder createSingleFilter(FilterData filterData)
        {
            StringBuilder filter = new StringBuilder();

            if (
                (filterData.Type == FilterType.NumericBetween || filterData.Type == FilterType.DateTimeBetween)
                &&
                (filterData.QueryString != String.Empty || filterData.QueryStringTo != String.Empty)
                )
            {
                if (filterData.QueryString != String.Empty)
                {
                    createFilterExpression(
                        filterData, 
                        filterData.QueryString,
                        filter,
                        getOperatorString(FilterOperator.GreaterThanOrEqual));
                }
                if (filterData.QueryStringTo != String.Empty)
                {
                    if (filter.Length > 0) filter.Append(" AND ");

                    createFilterExpression(
                        filterData, 
                        filterData.QueryStringTo, 
                        filter, 
                        getOperatorString(FilterOperator.LessThanOrEqual));
                }
            }
            else if (filterData.QueryString != String.Empty
                &&
                filterData.Operator != FilterOperator.Undefined)
            {
                if (filterData.Type == FilterType.Text)
                {
                    if (filterData.Operator == FilterOperator.Contains)
                        createContainsStringFilterExpression(filterData,filter);
                    else
                        createStringFilterExpression(filterData, filter);
                }
                else
                {
                    createFilterExpression(
                        filterData, filterData.QueryString, filter, getOperatorString(filterData.Operator));
                }
            }

            return filter;
        }

        private void createFilterExpression(
            FilterData filterData, string queryString, StringBuilder filter, string operatorString)
        {
            filter.Append(filterData.ValuePropertyBindingPath);

            object parameterValue = null;

            if (trySetParameterValue(out parameterValue, queryString, filterData.ValuePropertyType))
            {
                Parameters.Add(parameterValue);

                paramCounter.Increment();

                filter.Append(" " + operatorString + " @" + paramCounter.ParameterNumber);
            }
            else
            {
                filter = new StringBuilder();//do not use filter
            }
        }

        private bool trySetParameterValue(
            out object parameterValue, string stringValue, Type type)
        {
            parameterValue = null;
            bool valueIsSet;

            try
            {
                if (type == typeof(Nullable<DateTime>) || type == typeof(DateTime))
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
                else
                {
                    parameterValue = Convert.ChangeType(stringValue, typeof(Double));//TODO use "real" number type
                }

                valueIsSet = true;
            }
            catch (Exception)
            {
                valueIsSet = false;
            }

            return valueIsSet;
        }

        private void createStringFilterExpression(
            FilterData filterData, StringBuilder filter)
        {
            StringFilterExpressionCreator
                creator = new StringFilterExpressionCreator(
                    paramCounter, filterData, Parameters);

            string filterExpression = creator.Create();

            filter.Append(filterExpression);
        }

        private void createContainsStringFilterExpression(
            FilterData filterData, StringBuilder filter)
        {
            StringFilterExpressionCreator
                creator = new StringFilterExpressionCreator(
                    paramCounter, filterData, Parameters);

            string filterExpression = creator.CreateContainsFilter();

            filter.Append(filterExpression);
        }

        private string getOperatorString(FilterOperator filterOperator)
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