using ADOTabular;
using ADOTabular.Interfaces;
using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using DaxStudio.UI.Events;
using Microsoft.AnalysisServices.Tabular;
using Newtonsoft.Json;
using System.ComponentModel;

namespace DaxStudio.UI.Model
{

    public enum SortDirection
    {
        [Description("Ascending")]
        ASC,
        [Description("Descending")]
        DESC,
        [Description("None")]
        None
    }

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

        public QueryBuilderColumn(IADOTabularColumn item, bool isModelItem, IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            TabularObject = item;
            IsModelItem = isModelItem;
        }

        public QueryBuilderColumn(string caption, ADOTabularTable table, IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _caption = caption;
            SelectedTable = table;
            IsModelItem = false;
        }

        public string MinValue => TabularObject?.MinValue;

        public string MaxValue => TabularObject?.MaxValue;

        public long DistinctValues => TabularObject?.DistinctValues??0;

        public Type SystemType => TabularObject?.SystemType;
        
        public DataType DataType => TabularObject?.DataType??DataType.Unknown;
        public string TableName => TabularObject.TableName;
        public MetadataImages MetadataImage => TabularObject?.MetadataImage?? MetadataImages.Measure;

        private string _overridenMeasureExpression = string.Empty;
        private IEventAggregator _eventAggregator;

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
                NotifyOfPropertyChange(nameof(IsModelItem));
                _eventAggregator.PublishOnUIThread(new QueryBuilderUpdateEvent());
            }
        }

        [DataMember]
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

        //public bool IsVisible => TabularObject?.IsVisible ?? true;

        public ADOTabularObjectType ObjectType => TabularObject?.ObjectType?? ADOTabularObjectType.Measure;

        //public List<string> GetSampleData(ADOTabularConnection connection, int sampleSize) => throw new NotImplementedException();

        //public void UpdateBasicStats(ADOTabularConnection connection) => throw new NotImplementedException();

        public string Description => TabularObject.Description;

        public void DuplicateMeasure()
        {
            _eventAggregator.PublishOnUIThread(new DuplicateMeasureEvent(this));
            
        }

        private SortDirection _sortDirection = SortDirection.ASC;
        [DataMember]
        public SortDirection SortDirection { get => _sortDirection; 
            set
            {
                _sortDirection = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(SortDescription));
                _eventAggregator.PublishOnUIThread(new QueryBuilderUpdateEvent());
            }
        }

        public string SortDescription => SortDirection== SortDirection.None? $"Do not order by {DaxName}\n(Click to change)" : $"Order by {DaxName} {SortDirection}\n(Click to change)";

        public bool IsSortBy { get; internal set; }
    }
}