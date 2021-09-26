using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ADOTabular;
using ADOTabular.Interfaces;
using Caliburn.Micro;
using ControlzEx.Standard;
using DaxStudio.Interfaces;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DaxStudio.UI.JsonConverters
{
    class QueryBuilderConverter:JsonConverter
    {
        private readonly IGlobalOptions _options;
        private readonly DocumentViewModel _document;
        private readonly IEventAggregator _eventAggregator;

        public QueryBuilderConverter(IEventAggregator eventAggregator, DocumentViewModel document, IGlobalOptions options)
        {
            _eventAggregator = eventAggregator;
            _document = document;
            _options = options;
        }
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(QueryBuilderViewModel));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // Load the JSON for the Result into a JObject
            JObject jo = JObject.Load(reader);

            // Construct a dummy copy of the VM here so that we can populate the Columns, Filters and OrderBy collections
            var vm = new QueryBuilderViewModel(_eventAggregator,_document,_options);

            // Read the properties which will be used as constructor parameters
            foreach (var col in jo["Columns"].ToArray())
            {
                IADOTabularColumn obj = col["TabularObject"].ToObject<ADOTabularColumnStub>();
                bool isModelItem = (bool)col["IsModelItem"];
                bool isOverridden = (bool)col["IsOverriden"];
                SortDirection sortDirection = SortDirection.ASC;
                var _ = Enum.TryParse( col["SortDirection"].ToString(), out sortDirection);
                string measureExpression = col["MeasureExpression"].ToString();
                string measureCaption = col["Caption"].ToString();
                var queryBuilderCol = new QueryBuilderColumn(obj, isModelItem, _eventAggregator);
                queryBuilderCol.MeasureExpression = isOverridden?measureExpression:string.Empty;
                queryBuilderCol.SortDirection = sortDirection;
                if (isOverridden) queryBuilderCol.Caption = measureCaption;
                vm.Columns.Add(queryBuilderCol);
            }

            foreach (var filter in jo["Filters"]["Items"].ToArray())
            {
                IADOTabularColumn obj = filter["TabularObject"].ToObject<ADOTabularColumnStub>();
                FilterType filterType = filter["FilterType"].ToObject<FilterType>();
                string filterValue = filter["FilterValue"].ToString();

                var filterValueIsParameterToken = filter["FilterValueIsParameter"];
                var filterValue2IsParameterToken = filter["FilterValue2IsParameter"];

                string filterValue2 = filter["FilterValue2"].ToString();

                IModelCapabilities modelCapabilities = filter["ModelCapabilities"].ToObject<ADOTabularModelCapabilities>();
                var queryBuilderFilter = new QueryBuilderFilter(obj, modelCapabilities,_eventAggregator);
                queryBuilderFilter.FilterType = filterType;
                queryBuilderFilter.FilterValue = filterValue;
                if (filterValueIsParameterToken != null)
                {
                    bool filterValueIsParameter = Convert.ToBoolean(filterValueIsParameterToken.ToString());
                    queryBuilderFilter.FilterValueIsParameter = filterValueIsParameter;
                }
                queryBuilderFilter.FilterValue2 = filterValue2;
                if (filterValue2IsParameterToken != null)
                {
                    bool filterValue2IsParameter = Convert.ToBoolean(filterValue2IsParameterToken.ToString());
                    queryBuilderFilter.FilterValue2IsParameter = filterValue2IsParameter;
                }

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
