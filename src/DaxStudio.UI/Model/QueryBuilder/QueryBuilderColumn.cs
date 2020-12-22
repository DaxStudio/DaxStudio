using ADOTabular;
using ADOTabular.Interfaces;
using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace DaxStudio.UI.Model
{
    [DataContract]
    public class QueryBuilderColumn : PropertyChangedBase //, IADOTabularColumn
    {
        [DataMember]
        public IADOTabularColumn TabularObject;
        private string _caption = string.Empty;
        
        private IADOTabularObject _selectedTable;
    
        public IADOTabularObject SelectedTable { get => _selectedTable;
            set {
                _selectedTable = value;
                NotifyOfPropertyChange();
            }
        }

        private string _tableName = string.Empty;
        [DataMember]
        public bool IsModelItem { get; }

        public QueryBuilderColumn(IADOTabularColumn item, bool isModelItem)
        {
            this.TabularObject = item;
            this.IsModelItem = isModelItem;
        }

        public QueryBuilderColumn(string caption, ADOTabularTable table)
        {
            _caption = caption;
            SelectedTable = table;
            IsModelItem = false;
        }

        public string MinValue => TabularObject?.MinValue;

        public string MaxValue => TabularObject?.MaxValue;

        public long DistinctValues => TabularObject?.DistinctValues??0;
        
        public Type DataType => TabularObject?.DataType;
        public string TableName => TabularObject.TableName;
        public MetadataImages MetadataImage => TabularObject?.MetadataImage?? MetadataImages.Measure;

        private string _overridenMeasureExpression = string.Empty;
        [JsonProperty]
        public string MeasureExpression
        {
            get
            {
                if (string.IsNullOrEmpty(_overridenMeasureExpression) && IsModelItem) return TabularObject.MeasureExpression;
                return _overridenMeasureExpression;
             }
            set
            {
                _overridenMeasureExpression = value;
                NotifyOfPropertyChange();
            }
        }

        public string Caption { get => TabularObject?.Caption ?? _caption;
            set {
                _caption = value;
                NotifyOfPropertyChange();
            }
        }

        [DataMember]
        public bool IsOverriden => !string.IsNullOrWhiteSpace(_overridenMeasureExpression);

        public string DaxName => TabularObject?.DaxName?? "[" + Caption  +"]";

        public string Name => TabularObject?.Name;

        public bool IsVisible => TabularObject?.IsVisible ?? true;

        public ADOTabularObjectType ObjectType => TabularObject?.ObjectType?? ADOTabularObjectType.Measure;

        public List<string> GetSampleData(ADOTabularConnection connection, int sampleSize) => throw new NotImplementedException();

        public void UpdateBasicStats(ADOTabularConnection connection) => throw new NotImplementedException();

        public string Description => TabularObject.Description;
    }
}