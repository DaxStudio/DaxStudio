using Caliburn.Micro;
using DaxStudio.UI.Utils;
using System;
using System.Collections.Generic;

namespace DaxStudio.UI.Model
{
    public class QueryInfo
    {
        private string rawQuery;
        private Dictionary<string,QueryParameter> _parameters;
        public QueryInfo(string queryText, bool injectEvaluate, bool injectRowFunction, IEventAggregator eventAggregator)
        {
            NeedsParameterValues = true;
            InjectEvaluate = injectEvaluate;
            InjectRowFunction = injectRowFunction;
            rawQuery = queryText;
            _parameters = new Dictionary<string, QueryParameter>(StringComparer.OrdinalIgnoreCase );
            DaxHelper.PreProcessQuery(this, rawQuery, eventAggregator);
        }
        public string ProcessedQuery { get {
                var baseQuery = InjectEvaluate ? "EVALUATE " : "";
                baseQuery += InjectRowFunction ? "ROW(\"Value\", " : "";
                if (HasParameters) {
                    baseQuery += QueryText; 
                } else {
                    baseQuery += rawQuery;
                }
                baseQuery += InjectRowFunction ? " )" : "";

                return baseQuery;
            }
        }

        public string QueryText { get; set; }
        public bool HasParameters { get { return Parameters.Count > 0; } }
        //public string ProcessedQuery { get; set; }
        public bool NeedsParameterValues { get; set; }
        public bool InjectEvaluate { get; }
        public bool InjectRowFunction { get; }

        public string QueryWithMergedParameters
        {
            get
            {
                return DaxHelper.replaceParamsInQuery(ProcessedQuery, Parameters);
            }
        }
        public Dictionary<string,QueryParameter> Parameters { get { return _parameters; } }
    }
}
