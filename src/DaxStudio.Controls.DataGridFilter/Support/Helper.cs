using System.Collections.Generic;

namespace DaxStudio.Controls.DataGridFilter.Support
{
    public static class Helper
    {
        public static Dictionary<string, FilterData> CloneDictionaryHelper(Dictionary<string, FilterData> dict)
        {
            var dictNew = new Dictionary<string, FilterData>();

            foreach (KeyValuePair<string, FilterData> kvp in dict)
            {
                var data = new FilterData(
                    kvp.Value.Operator, kvp.Value.Type,
                    kvp.Value.ValuePropertyBindingPath, kvp.Value.ValuePropertyType,
                    kvp.Value.QueryString, kvp.Value.QueryStringTo,
                    kvp.Value.IsTypeInitialized, kvp.Value.IsCaseSensitiveSearch,
                    kvp.Value.IsContainsTextSearch);

                dictNew.Add(kvp.Key, data);
            }

            return dictNew;
        }
    }
}
