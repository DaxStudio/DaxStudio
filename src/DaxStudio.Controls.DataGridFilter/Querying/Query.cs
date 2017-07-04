using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DaxStudio.Controls.DataGridFilter.Querying
{
    public class Query
    {
        public Query()
        {
            lastFilterString    = String.Empty;
            lastQueryParameters = new List<object>();
        }

        public string        FilterString { get; set; }
        public List<object>  QueryParameters { get; set; }

        private string       lastFilterString { get; set; }
        private List<object> lastQueryParameters { get; set; }

        public bool IsQueryChanged
        {
            get
            {
                bool queryChanged = false;

                if (FilterString != lastFilterString)
                {
                    queryChanged = true;
                }
                else
                {
                    if (QueryParameters.Count != lastQueryParameters.Count)
                    {
                        queryChanged = true;
                    }
                    else
                    {
                        for (int i = 0; i < QueryParameters.Count; i++)
                        {
                            if (!QueryParameters[i].Equals(lastQueryParameters[i]))
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
            lastFilterString    = FilterString;
            lastQueryParameters = QueryParameters;
        }
    }
}
