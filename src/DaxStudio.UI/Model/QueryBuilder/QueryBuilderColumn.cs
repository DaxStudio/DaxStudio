using ADOTabular;
using ADOTabular.Interfaces;
using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using DaxStudio.Core.Events;
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

        [DataMember]
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

        public QueryBuilderColumn(string caption, IADOTabularObject table, IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _caption = caption;
            SelectedTable = table;
            IsModelItem = false;
            TabularObject = new ADOTabularColumnStub() { Caption = caption, ObjectType = ADOTabularObjectType.Measure };
        }

        public string MinValue => TabularObject?.MinValue;
        public string MaxValue => TabularObject?.MaxValue;
        public long DistinctValues => TabularObject?.DistinctValues??0;
        public Type SystemType => TabularObject?.SystemType;
        public string ImageResource => TabularObject?.ImageResource?? "new_measure_smallDrawingImage";
        public DataType DataType => TabularObject?.DataType??DataType.Unknown;
        public string TableName => TabularObject?.TableName??SelectedTable?.DaxName;
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
                _eventAggregator.PublishAsync(new QueryBuilderUpdateEvent());
            }
        }

        [DataMember]
        public string Caption { get => TabularObject?.Caption ?? _caption;
            set {
                _caption = value;
                if (TabularObject is ADOTabularColumnStub tabObj) 
                { 
                    tabObj.Caption = value;
                }
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
            _eventAggregator.PublishAsync(new DuplicateMeasureEvent(this));
        }

        private SortDirection _sortDirection = SortDirection.ASC;
        private SortDirection _previousSortDirection = SortDirection.ASC;
        [DataMember]
        public SortDirection SortDirection { get => _sortDirection; 
            set => SetSortDirection(value, true);
        }

        // allows bulk updates to change the sort direction of many columns
        // while only publishing a single QueryBuilderUpdateEvent
        internal void SetSortDirection(SortDirection value, bool publishUpdateEvent)
        {
            if (_sortDirection != SortDirection.None) _previousSortDirection = _sortDirection;
            _sortDirection = value;
            NotifyOfPropertyChange(nameof(SortDirection));
            NotifyOfPropertyChange(nameof(SortDescription));
            NotifyOfPropertyChange(nameof(SortDirectionImageResource));
            NotifyOfPropertyChange(nameof(IsSortDirectionEnabled));
            if (publishUpdateEvent) _eventAggregator.PublishAsync(new QueryBuilderUpdateEvent());
        }

        // the last direction this column was sorted by, used when re-enabling sorting
        internal SortDirection PreviousSortDirection => _previousSortDirection == SortDirection.None ? SortDirection.ASC : _previousSortDirection;

        public string SortDirectionImageResource
        {
            get
            {
                switch (_sortDirection)
                {
                    case SortDirection.ASC: return "sort_ascDrawingImage";
                    case SortDirection.DESC: return "sort_descDrawingImage";
                    default: return "";
                }
            }
        }

        public string SortDescription => SortDirection== SortDirection.None? $"Do not order by {DaxName}\n(Click to change)" : $"Order by {DaxName} {SortDirection}\n(Click to change)";

        public bool IsSortDirectionEnabled { get => SortDirection != SortDirection.None; 
            set { 
                if (value)
                {
                    if (SortDirection == SortDirection.None) SortDirection = PreviousSortDirection;
                }
                else SortDirection = SortDirection.None;
            } 
        }

        public bool IsSortBy { get; internal set; }
        public bool IsGroupBy { get; internal set; }
    }
}