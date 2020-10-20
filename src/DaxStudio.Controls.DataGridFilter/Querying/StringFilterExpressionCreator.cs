using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DaxStudio.Controls.DataGridFilter.Support;

namespace DaxStudio.Controls.DataGridFilter.Querying
{
    internal class StringFilterExpressionCreator
    {
        const string WildcardAnyString = "%";

        private enum StringExpressionFunction
        {
            Undefined = 0,
            StartsWith = 1,
            IndexOf = 2,
            EndsWith = 3
        }

        FilterData filterData;
        List<object> paramseters;
        ParameterCounter paramCounter;

        internal int ParametarsCrated { get { return paramseters.Count; } }

        internal StringFilterExpressionCreator(
            ParameterCounter paramCounter, FilterData filterData, List<object> paramseters)
        {
            this.paramCounter = paramCounter;
            this.filterData = filterData;
            this.paramseters = paramseters;
        }

        internal string Create()
        {
            StringBuilder filter = new StringBuilder();
            
            List<string> filterList = parse(this.filterData.QueryString);

            for (int i = 0; i < filterList.Count; i++)
            {
                if (i > 0) filter.Append(" and ");

                filter.Append(filterList[i]);
            }

            return filter.ToString();
        }

        private List<string> parse(string filterString)
        {
            string token = null;
            int i = 0;
            bool expressionCompleted = false;
            List<string> filter = new List<string>();
            string expressionValue = String.Empty;
            StringExpressionFunction function = StringExpressionFunction.Undefined;

            do
            {
                token = i < filterString.Length ? filterString[i].ToString() : null;

                if (token == WildcardAnyString || token == null)
                {
                    if (expressionValue.StartsWith(WildcardAnyString) && token != null)
                    {
                        function = StringExpressionFunction.IndexOf;

                        expressionCompleted = true;
                    }
                    else if (expressionValue.StartsWith(WildcardAnyString) && token == null)
                    {
                        function = StringExpressionFunction.EndsWith;

                        expressionCompleted = false;
                    }
                    else
                    {
                        function = StringExpressionFunction.StartsWith;

                        if (filterString.Length - 1 > i) expressionCompleted = true;
                    }
                }

                if (token == null)
                {
                    expressionCompleted = true;
                }

                expressionValue += token;

                if (expressionCompleted
                    && function != StringExpressionFunction.Undefined
                    && !string.IsNullOrEmpty(expressionValue))
                {
                    string expressionValueCopy = String.Copy(expressionValue);

                    expressionValueCopy = expressionValueCopy.Replace(WildcardAnyString, String.Empty);

                    if (!string.IsNullOrEmpty(expressionValueCopy))
                    {
                        filter.Add(createFunction(function, expressionValueCopy));
                    }

                    function = StringExpressionFunction.Undefined;

                    expressionValue = expressionValue.EndsWith(WildcardAnyString) ? WildcardAnyString : String.Empty;

                    expressionCompleted = false;
                }

                i++;

            } while (token != null);

           return filter;
        }

        private string createFunction(
            StringExpressionFunction function, string value)
        {
            StringBuilder filter = new StringBuilder();

            paramseters.Add(value);

            filter.Append(filterData.ValuePropertyBindingPath);

            if (filterData.ValuePropertyType.IsGenericType
                && filterData.ValuePropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                filter.Append(".Value");
            }

            paramCounter.Increment();
            paramCounter.Increment();

            filter.Append(".ToString()." + function.ToString() + "(@" + (paramCounter.ParameterNumber - 1) + ", @" + (paramCounter.ParameterNumber) + ")");

            if (function == StringExpressionFunction.IndexOf)
            {
                filter.Append(" != -1 ");
            }

            paramseters.Add(filterData.IsCaseSensitiveSearch 
                ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase);

            return filter.ToString();
        }

        internal string CreateContainsFilter()
        {
            return createFunction(StringExpressionFunction.IndexOf, filterData.QueryString);   
        }
    }
}
