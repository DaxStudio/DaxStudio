using Caliburn.Micro;
using DaxStudio.UI.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Model
{
    public class QueryInfo
    {
        private string rawQuery;
        private Dictionary<string,QueryParameter> _parameters;
        public QueryInfo(string queryText, bool injectEvaluate, IEventAggregator eventAggregator)
        {
            NeedsParameterValues = true;
            InjectEvaluate = injectEvaluate;
            rawQuery = queryText;
            _parameters = new Dictionary<string, QueryParameter>(StringComparer.OrdinalIgnoreCase );
            DaxHelper.PreProcessQuery(this, rawQuery, eventAggregator);
        }
        public string ProcessedQuery { get {
                var baseQuery = InjectEvaluate ? "EVALUATE " : "";
                if (HasParameters) {
                    baseQuery += DaxHelper.replaceParamsInQuery(QueryText, Parameters);
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
        public bool InjectEvaluate { get; }

        public Dictionary<string,QueryParameter> Parameters { get { return _parameters; } }
    }
}
