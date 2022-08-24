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
        public QueryInfo(string queryText, IEventAggregator eventAggregator)
        {
            NeedsParameterValues = true;
            rawQuery = queryText;
            _parameters = new Dictionary<string, QueryParameter>(StringComparer.OrdinalIgnoreCase );
            DaxHelper.PreProcessQuery(this, rawQuery, eventAggregator);
        }
        public string ProcessedQuery { get {
                var baseQuery = string.Empty;
                if (HasParameters) {
                    baseQuery += QueryText; 
                } else {
                    baseQuery += rawQuery;
                }

                return baseQuery;
            }
        }

        public string QueryText { get; set; }
        public bool HasParameters { get { return Parameters.Count > 0; } }
        //public string ProcessedQuery { get; set; }
        public bool NeedsParameterValues { get; set; }

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
