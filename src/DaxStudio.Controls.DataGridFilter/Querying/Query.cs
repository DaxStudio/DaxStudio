using System;
using System.Collections.Generic;

namespace DaxStudio.Controls.DataGridFilter.Querying
{
    public class Query
    {
        public Query()
        {
            LastFilterString    = String.Empty;
            LastQueryParameters = new List<object>();
        }

        public string        FilterString { get; set; }
        public List<object>  QueryParameters { get; set; }

        private string       LastFilterString { get; set; }
        private List<object> LastQueryParameters { get; set; }

        public bool IsQueryChanged
        {
            get
            {
                bool queryChanged = false;

                if (FilterString != LastFilterString)
                {
                    queryChanged = true;
                }
                else
                {
                    if (QueryParameters.Count != LastQueryParameters.Count)
                    {
                        queryChanged = true;
                    }
                    else
                    {
                        for (int i = 0; i < QueryParameters.Count; i++)
                        {
                            if (!QueryParameters[i].Equals(LastQueryParameters[i]))
                            {
                                queryChanged = true;
                                break;
                            }
                        }
                    }
                }

                return queryChanged;
            }
        }

        public void StoreLastUsedValues()
        {
            LastFilterString    = FilterString;
            LastQueryParameters = QueryParameters;
        }
    }
}
