using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ADOTabular;
using ADOTabular.Interfaces;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DaxStudio.UI.JsonConverters
{
    class QueryBuilderConverter:JsonConverter
    {

        
            public override bool CanConvert(Type objectType)
            {
                return (objectType == typeof(QueryBuilderViewModel));
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                // Load the JSON for the Result into a JObject
                JObject jo = JObject.Load(reader);

                // Construct a dummy copy of the VM here so that we can populate the Columns, Filters and OrderBy collections
                var vm = new QueryBuilderViewModel(null,null,null);

                // Read the properties which will be used as constructor parameters
                foreach (var col in jo["Columns"].ToArray())
                {
                    IADOTabularColumn obj = col["TabularObject"].ToObject<ADOTabularColumnStub>();
                    bool isModelItem = (bool)col["IsModelItem"];
                    bool isOverridden = (bool)col["IsOverriden"];
                    string measureExpression = col["MeasureExpression"].ToString();
                    var queryBuilderCol = new QueryBuilderColumn(obj, isModelItem);
                    queryBuilderCol.MeasureExpression = isOverridden?measureExpression:string.Empty;
                    vm.Columns.Add(queryBuilderCol);
                }

                foreach (var filter in jo["Filters"]["Items"].ToArray())
                {
                    IADOTabularColumn obj = filter["TabularObject"].ToObject<ADOTabularColumnStub>();
                    FilterType filterType = filter["FilterType"].ToObject<FilterType>();
                    string filterValue = filter["FilterValue"].ToString();
                    string filterValue2 = filter["FilterValue2"].ToString();
                    IModelCapabilities modelCapabilities = filter["ModelCapabilities"].ToObject<ADOTabularModelCapabilities>();
                    var queryBuilderFilter = new QueryBuilderFilter(obj, modelCapabilities);
                    queryBuilderFilter.FilterType = filterType;
                    queryBuilderFilter.FilterValue = filterValue;
                    queryBuilderFilter.FilterValue2 = filterValue2;

                vm.Filters.Add(queryBuilderFilter);

                }


                // Return the result
                return vm;
            }

            public override bool CanWrite
            {
                get { return false; }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        

    }
}
